using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LithophaneGUI.Core;

namespace LithophaneGUI;

public partial class MainWindow : Window
{
    /// <summary>Node budget for the interactive preview; exports always use the full resolution.</summary>
    private const int PreviewMaxNodes = 150_000;

    private static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".wbmp", ".heic", ".heif", ".avif" };

    private readonly ObservableCollection<ImageItem> _images = new();
    private readonly DispatcherTimer _previewTimer;
    private CancellationTokenSource? _previewCts;
    private bool _resetCameraOnNextPreview = true;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        ImageList.ItemsSource = _images;

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            _ = UpdatePreviewAsync();
        };

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        UpdateShapeDependentControls();
    }

    private ImageItem? SelectedItem => ImageList?.SelectedItem as ImageItem;

    // ---------------------------------------------------------------- settings

    private LithophaneSettings ReadSettings()
    {
        static double V(NumericUpDown box, double fallback) => box.Value is { } v ? (double)v : fallback;

        return new LithophaneSettings
        {
            Negative = NegativeRadio.IsChecked == true,
            MirrorX = MirrorCheck.IsChecked == true,
            FlipY = FlipCheck.IsChecked == true,
            RepeatX = (int)V(RepeatXBox, 1),
            RepeatY = (int)V(RepeatYBox, 1),
            Gamma = V(GammaBox, 1),
            Shape = (ShapeType)Math.Max(0, ShapeCombo.SelectedIndex),
            MaxSize = V(SizeBox, 100),
            Thickness = V(ThicknessBox, 3),
            MinThickness = V(MinThicknessBox, 0.8),
            Border = V(BorderBox, 3),
            Resolution = V(ResolutionBox, 5),
            CurveAngle = V(CurveBox, 120),
            StandDepth = V(StandDepthBox, 0),
            StandHeight = V(StandHeightBox, 3),
        };
    }

    private void OnSettingChanged(object? sender, RoutedEventArgs e) => SchedulePreview();

    private void OnShapeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateShapeDependentControls();
        _resetCameraOnNextPreview = true;
        SchedulePreview();
    }

    private void UpdateShapeDependentControls()
    {
        if (CurveBox == null) return;
        var shape = (ShapeType)Math.Max(0, ShapeCombo.SelectedIndex);
        bool curved = shape is ShapeType.OuterCurve or ShapeType.InnerCurve;
        CurveBox.IsEnabled = CurveLabel.IsEnabled = curved;
        CurveLabel.Opacity = curved ? 1 : 0.5;

        bool stand = shape != ShapeType.Dome;
        StandDepthBox.IsEnabled = StandHeightBox.IsEnabled = stand;
        StandDepthLabel.Opacity = StandHeightLabel.Opacity = stand ? 1 : 0.5;
    }

    // ---------------------------------------------------------------- images

    private async void OnAddImagesClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose images",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = ImageExtensions.Select(x => "*" + x).ToArray() },
                FilePickerFileTypes.All,
            },
        });
        await AddFilesAsync(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;
        await AddFilesAsync(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    }

    private async Task AddFilesAsync(IEnumerable<string> paths)
    {
        ImageItem? last = null;
        foreach (var path in paths)
        {
            try
            {
                SetStatus($"Loading {Path.GetFileName(path)}…");
                var img = await Task.Run(() => GrayImage.Load(path));
                last = new ImageItem(path, img);
                _images.Add(last);
            }
            catch (Exception ex)
            {
                SetStatus($"Could not load {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (last != null)
        {
            ImageList.SelectedItem = last;
            SetStatus("Left-drag: rotate · Right-drag: pan · Wheel: zoom");
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedItem is not { } item) return;
        int index = _images.IndexOf(item);
        _images.Remove(item);
        if (_images.Count > 0)
            ImageList.SelectedIndex = Math.Min(index, _images.Count - 1);
    }

    private void OnRotateClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedItem is not { } item) return;
        item.Image = item.Image.Rotate90();
        _resetCameraOnNextPreview = true;
        SchedulePreview();
    }

    private void OnImageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool has = SelectedItem != null;
        RemoveButton.IsEnabled = RotateButton.IsEnabled = has;
        UpdateExportButtons();
        EmptyHint.IsVisible = !has;
        _resetCameraOnNextPreview = true;
        if (has)
        {
            SchedulePreview(immediate: true);
        }
        else
        {
            _previewCts?.Cancel();
            Preview.SetMesh(null, true);
            InfoText.Text = "Add an image to begin.";
        }
    }

    // ---------------------------------------------------------------- preview

    private void SchedulePreview(bool immediate = false)
    {
        if (!IsInitialized || SelectedItem == null) return;
        _previewTimer.Stop();
        if (immediate)
            _ = UpdatePreviewAsync();
        else
            _previewTimer.Start();
    }

    private async Task UpdatePreviewAsync()
    {
        if (SelectedItem is not { } item) return;
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        var settings = ReadSettings();
        var image = item.Image;
        try
        {
            var mesh = await Task.Run(() => LithophaneGenerator.Generate(image, settings, PreviewMaxNodes, cts.Token), cts.Token);
            if (cts.IsCancellationRequested) return;

            Preview.SetMesh(mesh, _resetCameraOnNextPreview);
            _resetCameraOnNextPreview = false;
            InfoText.Text = DescribeModel(mesh, image, settings);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            InfoText.Text = "Preview failed: " + ex.Message;
        }
    }

    private static string DescribeModel(Mesh previewMesh, GrayImage image, LithophaneSettings s)
    {
        var (min, max) = previewMesh.Bounds();
        var size = max - min;
        var (imgW, imgH) = HeightMap.ImageSize(image, s);
        double totalW = imgW + (s.IsClosed ? 0 : 2 * s.Border);
        double totalH = imgH + 2 * s.Border;
        double nodes = totalW * s.Resolution * totalH * s.Resolution;
        double triangles = nodes * 4;
        double mb = (84 + triangles * 50) / (1024.0 * 1024.0);
        return $"Model: {size.X:0.#} × {size.Y:0.#} × {size.Z:0.#} mm\n" +
               $"Export: ~{triangles / 1e6:0.00} M triangles, ~{mb:0} MB STL";
    }

    private void OnBacklitChanged(object? sender, RoutedEventArgs e) =>
        Preview.Backlit = BacklitSwitch.IsChecked == true;

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => Preview.ResetView();

    // ---------------------------------------------------------------- export

    private void UpdateExportButtons()
    {
        ExportButton.IsEnabled = !_busy && SelectedItem != null;
        ExportAllButton.IsEnabled = !_busy && _images.Count > 1;
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedItem is not { } item) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save STL",
            SuggestedFileName = Path.GetFileNameWithoutExtension(item.Name) + ".stl",
            DefaultExtension = "stl",
            FileTypeChoices = new[] { new FilePickerFileType("STL model") { Patterns = new[] { "*.stl" } } },
        });
        var path = file?.TryGetLocalPath();
        if (path == null) return;
        await ExportAsync(new[] { (item, path) });
    }

    private async void OnExportAllClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder for the STL files",
        });
        var folder = folders.FirstOrDefault()?.TryGetLocalPath();
        if (folder == null) return;

        var jobs = _images.Select(item =>
            (item, Path.Combine(folder, Path.GetFileNameWithoutExtension(item.Name) + ".stl"))).ToArray();
        await ExportAsync(jobs);
    }

    private async Task ExportAsync(IReadOnlyList<(ImageItem Item, string Path)> jobs)
    {
        _busy = true;
        UpdateExportButtons();
        var settings = ReadSettings();
        var sw = Stopwatch.StartNew();
        try
        {
            for (int n = 0; n < jobs.Count; n++)
            {
                var (item, path) = jobs[n];
                string prefix = jobs.Count > 1 ? $"[{n + 1}/{jobs.Count}] " : "";
                SetStatus($"{prefix}Generating {item.Name}…");
                var image = item.Image;
                var mesh = await Task.Run(() => LithophaneGenerator.Generate(image, settings));
                SetStatus($"{prefix}Writing {Path.GetFileName(path)} ({mesh.TriangleCount:N0} triangles)…");
                await Task.Run(() => StlWriter.WriteBinary(mesh, path, Path.GetFileNameWithoutExtension(path)));
            }
            SetStatus(jobs.Count == 1
                ? $"Saved {jobs[0].Path} in {sw.Elapsed.TotalSeconds:0.0} s"
                : $"Saved {jobs.Count} STL files in {sw.Elapsed.TotalSeconds:0.0} s");
        }
        catch (Exception ex)
        {
            SetStatus("Export failed: " + ex.Message);
        }
        finally
        {
            _busy = false;
            UpdateExportButtons();
            GC.Collect();
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
