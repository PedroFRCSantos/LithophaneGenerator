using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LithophaneGUI.Core;
using LithophaneGUI.Rendering;

namespace LithophaneGUI.Controls;

/// <summary>Interactive 3D preview: left-drag rotates, right/middle-drag pans, wheel zooms.</summary>
public sealed class PreviewControl : Control
{
    private readonly SoftwareRenderer _renderer = new();
    private readonly OrbitCamera _camera = new();
    private WriteableBitmap? _bitmap;
    private int[] _pixels = Array.Empty<int>();
    private Mesh? _mesh;
    private bool _backlit;
    private LightSimulation _simulation = LightSimulation.Default;
    private bool _dirty = true;
    private Point _lastPointer;
    private bool _rotating, _panning;

    public PreviewControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>The 3D view is drawn by hand and must not be mirrored for right-to-left languages.</summary>
    protected override bool BypassFlowDirectionPolicies => true;

    public bool Backlit
    {
        get => _backlit;
        set { _backlit = value; Redraw(); }
    }

    /// <summary>Light and filament used by the backlit view.</summary>
    public LightSimulation Simulation
    {
        get => _simulation;
        set { _simulation = value; Redraw(); }
    }

    public void SetMesh(Mesh? mesh, bool resetCamera)
    {
        bool first = _mesh == null;
        _mesh = mesh;
        if (mesh != null && (resetCamera || first))
            _camera.Frame(mesh);
        Redraw();
    }

    public void ResetView()
    {
        if (_mesh != null) _camera.Frame(_mesh);
        Redraw();
    }

    private void Redraw()
    {
        _dirty = true;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        int w = Math.Max(1, (int)Bounds.Width);
        int h = Math.Max(1, (int)Bounds.Height);

        if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _pixels = new int[w * h];
            _dirty = true;
        }

        if (_dirty)
        {
            _renderer.Render(_mesh, _camera, w, h, _backlit, _simulation, _pixels);
            using var fb = _bitmap.Lock();
            if (fb.RowBytes == w * 4)
            {
                Marshal.Copy(_pixels, 0, fb.Address, w * h);
            }
            else
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(_pixels, y * w, fb.Address + y * fb.RowBytes, w);
            }
            _dirty = false;
        }

        context.DrawImage(_bitmap, new Rect(0, 0, w, h));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;
        _rotating = props.IsLeftButtonPressed;
        _panning = props.IsRightButtonPressed || props.IsMiddleButtonPressed;
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_rotating && !_panning) return;
        var p = e.GetPosition(this);
        var d = p - _lastPointer;
        _lastPointer = p;

        if (_rotating)
        {
            _camera.Yaw -= (float)d.X * 0.01f;
            _camera.Pitch = Math.Clamp(_camera.Pitch + (float)d.Y * 0.01f, -1.55f, 1.55f);
        }
        else
        {
            _camera.Pan((float)d.X, (float)d.Y, (int)Bounds.Height);
        }
        Redraw();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _rotating = _panning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _camera.Distance *= MathF.Pow(0.9f, (float)e.Delta.Y);
        _camera.Distance = Math.Clamp(_camera.Distance, 1f, 100000f);
        Redraw();
        e.Handled = true;
    }
}
