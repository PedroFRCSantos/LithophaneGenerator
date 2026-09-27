using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LithophaneGUI.Core;

namespace LithophaneGUI;

public partial class MainWindow : Window
{
    /// <summary>Node budget for the interactive preview; exports always use the full resolution.</summary>
    private const int PreviewMaxNodes = 150_000;
    private const int PreviewSupportCells = 500_000;

    private static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".wbmp", ".heic", ".heif", ".avif" };

    private readonly ObservableCollection<ImageItem> _images = new();
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _previewCts;
    private bool _resetCameraOnNextPreview = true;
    private bool _busy;

    private static readonly string StateDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LithophaneGUI");
    private static readonly string SessionFile = Path.Combine(StateDir, "session" + LithophaneProject.Extension);
    private static readonly string StateFile = Path.Combine(StateDir, "state.json");

    private readonly string[] _startupArgs;
    private string? _projectPath;
    private bool _dirty;
    private bool _loadingProject;

    public MainWindow() : this(Array.Empty<string>())
    {
    }

    public MainWindow(string[] args)
    {
        _startupArgs = args;
        InitializeComponent();
        ImageList.ItemsSource = _images;

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        UpdateShapeDependentControls();
        Opened += OnOpened;
        Closing += OnClosing;
        UpdateTitle();
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
            LightSupport = (LightSupportType)Math.Max(0, LightCombo.SelectedIndex),
            SupportHeight = V(SupportHeightBox, 20),
            SlotDepth = V(SlotDepthBox, 4),
            SlotClearance = V(ClearanceBox, 0.3),
            SupportWall = V(SupportWallBox, 2),
            SupportMinDepth = V(MinDepthBox, 30),
            CableDiameter = V(CableBox, 6),
            LedChannelWidth = V(LedWidthBox, 12),
            LedChannelDepth = V(LedDepthBox, 6),
            LampDiameter = V(LampDiameterBox, 40),
            LampDepth = V(LampDepthBox, 12),
            LampThroughHole = ThroughCheck.IsChecked == true,
        };
    }

    private void ApplySettings(LithophaneSettings s)
    {
        NegativeRadio.IsChecked = s.Negative;
        PositiveRadio.IsChecked = !s.Negative;
        MirrorCheck.IsChecked = s.MirrorX;
        FlipCheck.IsChecked = s.FlipY;
        RepeatXBox.Value = s.RepeatX;
        RepeatYBox.Value = s.RepeatY;
        GammaBox.Value = (decimal)s.Gamma;
        ShapeCombo.SelectedIndex = (int)s.Shape;
        SizeBox.Value = (decimal)s.MaxSize;
        ThicknessBox.Value = (decimal)s.Thickness;
        MinThicknessBox.Value = (decimal)s.MinThickness;
        BorderBox.Value = (decimal)s.Border;
        ResolutionBox.Value = (decimal)s.Resolution;
        CurveBox.Value = (decimal)s.CurveAngle;
        StandDepthBox.Value = (decimal)s.StandDepth;
        StandHeightBox.Value = (decimal)s.StandHeight;
        LightCombo.SelectedIndex = (int)s.LightSupport;
        SupportHeightBox.Value = (decimal)s.SupportHeight;
        SlotDepthBox.Value = (decimal)s.SlotDepth;
        ClearanceBox.Value = (decimal)s.SlotClearance;
        SupportWallBox.Value = (decimal)s.SupportWall;
        MinDepthBox.Value = (decimal)s.SupportMinDepth;
        CableBox.Value = (decimal)s.CableDiameter;
        LedWidthBox.Value = (decimal)s.LedChannelWidth;
        LedDepthBox.Value = (decimal)s.LedChannelDepth;
        LampDiameterBox.Value = (decimal)s.LampDiameter;
        LampDepthBox.Value = (decimal)s.LampDepth;
        ThroughCheck.IsChecked = s.LampThroughHole;
        UpdateShapeDependentControls();
    }

    private void OnSettingChanged(object? sender, RoutedEventArgs e)
    {
        MarkDirty();
        SchedulePreview();
    }

    private void OnLayoutChanged(object? sender, SelectionChangedEventArgs e) => MarkDirty();

    private void OnShapeChanged(object? sender, SelectionChangedEventArgs e)
    {
        MarkDirty();
        UpdateShapeDependentControls();
        _resetCameraOnNextPreview = true;
        SchedulePreview();
    }

    private void OnLightChanged(object? sender, SelectionChangedEventArgs e)
    {
        MarkDirty();
        UpdateShapeDependentControls();
        _resetCameraOnNextPreview = true;
        SchedulePreview();
    }

    private static readonly (double Diameter, double Depth, bool Through)[] LampPresets =
    {
        (40, 12, false),   // tea light, Ø38 + clearance
        (62, 12, false),   // LED puck light
        (29, 0, true),     // E14 lamp holder
        (40.5, 0, true),   // E27 lamp holder
        (10.5, 0, true),   // M10 threaded lamp tube
    };

    private void OnLampPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LampDiameterBox == null) return;
        int i = LampPresetCombo.SelectedIndex;
        MarkDirty();
        if (i < 0 || i >= LampPresets.Length) return; // custom: keep values
        var (dia, depth, through) = LampPresets[i];
        LampDiameterBox.Value = (decimal)dia;
        if (depth > 0) LampDepthBox.Value = (decimal)depth;
        ThroughCheck.IsChecked = through;
        SchedulePreview();
    }

    private void UpdateShapeDependentControls()
    {
        if (CurveBox == null || LightCombo == null) return;
        var shape = (ShapeType)Math.Max(0, ShapeCombo.SelectedIndex);
        bool curved = shape is ShapeType.OuterCurve or ShapeType.InnerCurve;
        CurveBox.IsEnabled = CurveLabel.IsEnabled = curved;
        CurveLabel.Opacity = curved ? 1 : 0.5;

        var light = (LightSupportType)Math.Max(0, LightCombo.SelectedIndex);
        bool closed = shape is ShapeType.Cylinder or ShapeType.Heart or ShapeType.Dome;
        LedPanel.IsVisible = light == LightSupportType.LedStrip;
        LampPanel.IsVisible = light == LightSupportType.Lamp;
        LightHint.IsVisible = light != LightSupportType.None;
        foreach (var c in new Control[] { SupportHeightBox, SlotDepthBox, ClearanceBox, SupportWallBox, CableBox })
            c.IsEnabled = light != LightSupportType.None;
        // Closed shapes get a tray over the whole inside instead of a channel, and have no front/back depth.
        MinDepthBox.IsEnabled = MinDepthLabel.IsEnabled = light != LightSupportType.None && !closed;
        MinDepthLabel.Opacity = MinDepthBox.IsEnabled ? 1 : 0.5;
        LedWidthBox.IsEnabled = !closed;
        LedWidthLabel.Opacity = closed ? 0.5 : 1;
        LayoutRow.IsVisible = light != LightSupportType.None;

        bool stand = shape != ShapeType.Dome && light == LightSupportType.None;
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
        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        var project = paths.FirstOrDefault(IsProjectFile);
        if (project != null)
        {
            if (await ConfirmDiscardChangesAsync())
                await OpenProjectFileAsync(project);
            return;
        }
        await AddFilesAsync(paths);
    }

    private async Task AddFilesAsync(IEnumerable<string> paths)
    {
        ImageItem? last = null;
        foreach (var path in paths)
        {
            try
            {
                SetStatus($"Loading {Path.GetFileName(path)}…");
                var (data, img) = await Task.Run(() =>
                {
                    var bytes = File.ReadAllBytes(path);
                    return (bytes, GrayImage.Load(bytes, Path.GetFileName(path)));
                });
                last = new ImageItem(path, data, img);
                _images.Add(last);
                MarkDirty();
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
        MarkDirty();
        if (_images.Count > 0)
            ImageList.SelectedIndex = Math.Min(index, _images.Count - 1);
    }

    private void OnRotateClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedItem is not { } item) return;
        item.Rotate90();
        MarkDirty();
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
        _debounceCts?.Cancel();
        if (immediate)
        {
            _ = UpdatePreviewAsync();
            return;
        }

        // Wait for the user to stop changing values before regenerating.
        var cts = _debounceCts = new CancellationTokenSource();
        _ = DebouncedPreviewAsync(cts.Token);
    }

    private async Task DebouncedPreviewAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(250, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await UpdatePreviewAsync();
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
            var (mesh, notes) = await Task.Run(() =>
            {
                var parts = LithophaneGenerator.GenerateParts(image, settings, PreviewMaxNodes, PreviewSupportCells, cts.Token);
                return (LithophaneGenerator.Assemble(parts), parts.Notes);
            }, cts.Token);
            if (cts.IsCancellationRequested) return;

            Preview.SetMesh(mesh, _resetCameraOnNextPreview);
            _resetCameraOnNextPreview = false;
            InfoText.Text = DescribeModel(mesh, image, settings) +
                string.Concat(notes.Select(n => "\n⚠ " + n));
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
               $"Lithophane: ~{triangles / 1e6:0.00} M triangles, ~{mb:0} MB STL";
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
        var layout = (ExportLayout)Math.Max(0, LayoutCombo.SelectedIndex);
        var sw = Stopwatch.StartNew();
        var written = new List<string>();
        try
        {
            for (int n = 0; n < jobs.Count; n++)
            {
                var (item, path) = jobs[n];
                string prefix = jobs.Count > 1 ? $"[{n + 1}/{jobs.Count}] " : "";
                SetStatus($"{prefix}Generating {item.Name}…");
                var image = item.Image;
                var files = await Task.Run(() =>
                    LithophaneGenerator.Arrange(LithophaneGenerator.GenerateParts(image, settings), layout));

                string dir = Path.GetDirectoryName(path) ?? "";
                string stem = Path.GetFileNameWithoutExtension(path);
                foreach (var (suffix, mesh) in files)
                {
                    string target = Path.Combine(dir, stem + suffix + ".stl");
                    SetStatus($"{prefix}Writing {Path.GetFileName(target)} ({mesh.TriangleCount:N0} triangles)…");
                    await Task.Run(() => StlWriter.WriteBinary(mesh, target, stem + suffix));
                    written.Add(target);
                }
            }
            SetStatus(written.Count == 1
                ? $"Saved {written[0]} in {sw.Elapsed.TotalSeconds:0.0} s"
                : $"Saved {written.Count} STL files ({string.Join(", ", written.Take(3).Select(Path.GetFileName))}{(written.Count > 3 ? ", …" : "")}) in {sw.Elapsed.TotalSeconds:0.0} s");
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

    // ---------------------------------------------------------------- project

    private static bool IsProjectFile(string path) =>
        string.Equals(Path.GetExtension(path), LithophaneProject.Extension, StringComparison.OrdinalIgnoreCase);

    private static readonly FilePickerFileType ProjectFileType =
        new("Lithophane project") { Patterns = new[] { "*" + LithophaneProject.Extension } };

    private void MarkDirty()
    {
        if (_loadingProject || !IsInitialized || _dirty) return;
        _dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string name = _projectPath != null ? Path.GetFileName(_projectPath) : "Untitled";
        Title = $"{name}{(_dirty ? " *" : "")} — Lithophane Maker";
    }

    private LithophaneProject BuildProject() => new()
    {
        Settings = ReadSettings(),
        Layout = (ExportLayout)Math.Max(0, LayoutCombo.SelectedIndex),
        LampPreset = LampPresetCombo.SelectedIndex,
        Backlit = BacklitSwitch.IsChecked == true,
        SelectedImage = ImageList.SelectedIndex,
        Images = _images.Select(i => new ProjectImage { SourcePath = i.Path, Data = i.FileData, Rotation = i.Rotation })
            .ToList(),
    };

    private async Task LoadProjectAsync(LithophaneProject project, string? path, bool dirty)
    {
        _loadingProject = true;
        int failed = 0;
        try
        {
            _images.Clear();
            // The preset first: selecting it overwrites the lamp values, which ApplySettings then restores.
            LampPresetCombo.SelectedIndex = Math.Clamp(project.LampPreset, 0, LampPresets.Length);
            ApplySettings(project.Settings);
            LayoutCombo.SelectedIndex = Math.Clamp((int)project.Layout, 0, LayoutCombo.ItemCount - 1);
            BacklitSwitch.IsChecked = project.Backlit;

            SetStatus("Loading project images…");
            var decoded = await Task.Run(() => project.Images.Select(pi =>
            {
                try
                {
                    return (pi, ImageItem.Decode(pi.Data, Path.GetFileName(pi.SourcePath), pi.Rotation));
                }
                catch (Exception)
                {
                    return (pi, (GrayImage?)null);
                }
            }).ToList());

            foreach (var (pi, image) in decoded)
            {
                if (image == null) failed++;
                else _images.Add(new ImageItem(pi.SourcePath, pi.Data, image, pi.Rotation % 4));
            }
        }
        finally
        {
            _loadingProject = false;
        }

        _projectPath = path;
        _dirty = dirty;
        UpdateTitle();
        _resetCameraOnNextPreview = true;
        if (_images.Count > 0)
            ImageList.SelectedIndex = Math.Clamp(project.SelectedImage, 0, _images.Count - 1);
        SetStatus(failed > 0
            ? $"{failed} image(s) in the project could not be loaded."
            : "Left-drag: rotate · Right-drag: pan · Wheel: zoom");
    }

    private async Task<bool> OpenProjectFileAsync(string path)
    {
        try
        {
            var project = await Task.Run(() => LithophaneProject.Load(path));
            await LoadProjectAsync(project, path, dirty: false);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Saves the project, asking for a file name when needed. Returns false if cancelled or failed.</summary>
    private async Task<bool> SaveProjectAsync(bool saveAs)
    {
        string? path = saveAs ? null : _projectPath;
        if (path == null)
        {
            string suggested = _projectPath != null
                ? Path.GetFileNameWithoutExtension(_projectPath)
                : _images.Count > 0 ? Path.GetFileNameWithoutExtension(_images[0].Name) : "lithophane";
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save project",
                SuggestedFileName = suggested + LithophaneProject.Extension,
                DefaultExtension = LithophaneProject.Extension.TrimStart('.'),
                FileTypeChoices = new[] { ProjectFileType },
            });
            path = file?.TryGetLocalPath();
            if (path == null) return false;
            if (!IsProjectFile(path)) path += LithophaneProject.Extension;
        }

        try
        {
            var project = BuildProject();
            await Task.Run(() => project.Save(path));
            _projectPath = path;
            _dirty = false;
            UpdateTitle();
            SetStatus($"Saved project {path}");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("Save failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>Asks whether to save unsaved changes. Returns false if the user cancelled.</summary>
    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!_dirty) return true;

        string? choice = null;
        var dialog = new Window
        {
            Title = "Unsaved changes",
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        Button MakeButton(string text, string value, bool accent = false)
        {
            var b = new Button { Content = text, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (accent) b.Classes.Add("accent");
            b.Click += (_, _) =>
            {
                choice = value;
                dialog.Close();
            };
            return b;
        }
        string name = _projectPath != null ? Path.GetFileName(_projectPath) : "the current project";
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = $"Save changes to {name}?" },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { MakeButton("Save", "save", true), MakeButton("Don't save", "discard"), MakeButton("Cancel", "cancel") },
                },
            },
        };
        await dialog.ShowDialog(this);

        return choice switch
        {
            "save" => await SaveProjectAsync(saveAs: false),
            "discard" => true,
            _ => false,
        };
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmDiscardChangesAsync())
            await LoadProjectAsync(new LithophaneProject(), null, dirty: false);
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardChangesAsync()) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open project",
            FileTypeFilter = new[] { ProjectFileType, FilePickerFileTypes.All },
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
            await OpenProjectFileAsync(path);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e) => await SaveProjectAsync(saveAs: false);

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await SaveProjectAsync(saveAs: true);

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.S:
                e.Handled = true;
                await SaveProjectAsync(saveAs: shift);
                break;
            case Key.O:
                e.Handled = true;
                OnOpenClick(this, e);
                break;
            case Key.N:
                e.Handled = true;
                OnNewClick(this, e);
                break;
        }
    }

    // ---------------------------------------------------------------- session (reopen where you left off)

    private sealed record SessionState(string? ProjectPath, bool Modified);

    private async void OnOpened(object? sender, EventArgs e)
    {
        // A project given on the command line (e.g. double-clicking a .lipo file) wins over the last session.
        var argProject = _startupArgs.FirstOrDefault(a => IsProjectFile(a) && File.Exists(a));
        if (argProject != null)
        {
            await OpenProjectFileAsync(argProject);
        }
        else
        {
            try
            {
                if (File.Exists(StateFile))
                {
                    var state = JsonSerializer.Deserialize<SessionState>(await File.ReadAllTextAsync(StateFile));
                    string? path = state?.ProjectPath;
                    bool projectExists = path != null && File.Exists(path);
                    if (state is { Modified: false } && projectExists)
                        await OpenProjectFileAsync(path!);
                    else if (File.Exists(SessionFile))
                        await LoadProjectAsync(LithophaneProject.Load(SessionFile), path, dirty: state?.Modified == true);
                    else if (projectExists)
                        await OpenProjectFileAsync(path!);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Could not restore the last session: " + ex.Message);
            }
        }

        // Images given on the command line are added to whatever was restored.
        var argImages = _startupArgs.Where(a => !IsProjectFile(a) && File.Exists(a)).ToList();
        if (argImages.Count > 0)
            await AddFilesAsync(argImages);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_loadingProject) return;
        try
        {
            Directory.CreateDirectory(StateDir);
            // Unsaved work is kept in a session file so it comes back on the next start; a saved project is
            // simply reopened from its own file.
            if (_dirty || _projectPath == null)
                BuildProject().Save(SessionFile);
            else if (File.Exists(SessionFile))
                File.Delete(SessionFile);
            File.WriteAllText(StateFile, JsonSerializer.Serialize(new SessionState(_projectPath, _dirty)));
        }
        catch (Exception)
        {
            // Never block closing the app because the session could not be stored.
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
