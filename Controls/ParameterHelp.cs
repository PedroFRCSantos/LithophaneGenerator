using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace LithophaneGUI.Controls;

/// <summary>
/// Attach <c>c:Help.Topic="Thickness"</c> to a control to give it a tooltip with a small sketch
/// and an explanation of the parameter. Topics are defined in <see cref="HelpTopics"/>.
/// </summary>
public sealed class Help : AvaloniaObject
{
    public static readonly AttachedProperty<string?> TopicProperty =
        AvaloniaProperty.RegisterAttached<Help, Control, string?>("Topic");

    static Help()
    {
        TopicProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.NewValue is not string topic || !HelpTopics.All.TryGetValue(topic, out var entry)) return;
            ToolTip.SetTip(control, new StackPanel
            {
                Spacing = 6,
                MaxWidth = 260,
                Children =
                {
                    new Sketch { Topic = topic, Width = 240, Height = 130 },
                    new TextBlock { Text = entry.Title, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap },
                },
            });
            ToolTip.SetShowDelay(control, 400);
        });
    }

    public static string? GetTopic(Control c) => c.GetValue(TopicProperty);
    public static void SetTopic(Control c, string? value) => c.SetValue(TopicProperty, value);
}

public static class HelpTopics
{
    public static readonly IReadOnlyDictionary<string, (string Title, string Text)> All = new Dictionary<string, (string, string)>
    {
        // Image settings
        ["Positive"] = ("Positive", "Dark parts of the picture become thick, bright parts thin. The usual choice: the lit print looks like the photo."),
        ["Negative"] = ("Negative", "Bright parts become thick and dark parts thin, so the lit print looks like a negative. Useful for white-on-black artwork."),
        ["Mirror"] = ("Mirror", "Flips the picture left ↔ right. Use it when the lithophane will be viewed from the other side, e.g. from inside a lamp."),
        ["Flip"] = ("Flip", "Turns the picture upside down."),
        ["RepeatX"] = ("Repeat X", "Repeats the picture side by side this many times across the width. Handy for patterns around a lamp."),
        ["RepeatY"] = ("Repeat Y", "Repeats the picture this many times from bottom to top."),
        ["Gamma"] = ("Contrast (gamma)", "Curve applied to the brightness before it becomes thickness. 1 = unchanged. Above 1 darkens the mid-tones (more contrast in bright areas); below 1 brightens them."),
        ["Rotate"] = ("Rotate image 90°", "Rotates the picture a quarter turn clockwise each click. The rotation is saved in the project."),

        // Model settings
        ["Shape"] = ("Shape", "The overall form. Flat and the two curves are panels. Cylinder, heart and dome wrap the picture all the way around and are meant to go over a light."),
        ["MaxSize"] = ("Maximum size", "Length of the picture's longest side in mm, without the border. For curved and closed shapes it is measured along the curve: for a cylinder the width becomes the circumference."),
        ["Thickness"] = ("Thickness", "Wall thickness of the darkest areas and of the border. Thicker gives darker darks and a sturdier print. 3 mm is typical."),
        ["MinThickness"] = ("Thinnest layer", "Wall thickness of the brightest areas. Thinner gives brighter highlights but a fragile print; keep at least two nozzle widths (about 0.8 mm for a 0.4 mm nozzle)."),
        ["Border"] = ("Border", "Solid frame around the picture at full thickness. It gives the print strength and is what sits in the slot of a light support. On closed shapes it is only added at the top and bottom."),
        ["Resolution"] = ("Resolution", "Mesh points per mm in each direction. 5 points/mm (0.2 mm spacing) matches a 0.4 mm nozzle; more only makes the STL file larger."),
        ["CurveAngle"] = ("Curve angle", "How far the outer or inner curve bends, in degrees. 90° is a gentle quarter circle, 180° a half circle."),
        ["StandDepth"] = ("Stand depth", "Front-to-back depth of a foot under the bottom edge so the panel stands on its own. 0 = no stand. Not used together with a light support."),
        ["StandHeight"] = ("Stand height", "Height of that foot."),

        // Light support
        ["LightSource"] = ("Light source", "Generates a base that holds the lithophane in a slot and houses the light: a channel or tray for an LED strip / string, or a round recess or hole for a lamp."),
        ["SupportHeight"] = ("Base height", "Total height of the base. It is raised automatically if the cable groove would not fit underneath."),
        ["SlotDepth"] = ("Slot depth", "How deep the lithophane sits in the base. Keep the border at least this tall so no part of the picture is hidden."),
        ["SlotClearance"] = ("Slot clearance", "Extra gap on each side of the lithophane inside the slot. Printers vary: increase it if the print does not fit, reduce it if it wobbles."),
        ["SupportWall"] = ("Wall thickness", "Thickness of the base walls, including the wall between the slot and the light."),
        ["SupportMinDepth"] = ("Min. base depth", "Minimum front-to-back depth of the base so a panel cannot tip over. Only for flat and curved panels; closed shapes use their own footprint."),
        ["CableDiameter"] = ("Cable hole", "Diameter of the hole for the power cable. It leads down to a groove under the base, so the base still sits flat with the cable in place."),
        ["LedChannelWidth"] = ("LED channel width", "Width of the channel behind a panel that the LED strip lies in. Closed shapes get a tray over the whole inside instead."),
        ["LedChannelDepth"] = ("LED channel depth", "Depth of the channel (or tray) below the top of the base. Deeper hides the LEDs better; it is limited by the base height."),
        ["LampPreset"] = ("Lamp type", "Common light sources with their sizes. Tea light and LED puck sit in a recess; E14 / E27 sockets and the M10 tube go through the base. Custom keeps your own values."),
        ["LampDiameter"] = ("Lamp hole diameter", "Diameter of the round recess or hole for the lamp, including a little play. For closed shapes it is reduced automatically if it would not fit inside the lithophane."),
        ["LampDepth"] = ("Recess depth", "How deep the recess is, measured from the top of the base. Ignored for through-holes."),
        ["LampThrough"] = ("Hole through the base", "Makes the hole go all the way through, for lamp sockets and threaded tubes that are fixed from below."),
        ["ShowLight"] = ("Show the light source", "Draws the light itself in the 3D preview (LED strip, tea light or bulb) so you can see where it sits. It is never part of the exported STL."),
        ["Layout"] = ("Export as", "How the two parts are written: separate STL files (each with its own print settings), one file with both parts side by side, or one file with the lithophane already standing in the base, printed as one piece."),

        // Preview
        ["Backlit"] = ("Backlit view", "Simulates light shining through the print instead of normal shading: thin areas glow, thick areas stay dark."),
        ["LightColor"] = ("Light colour", "Colour of the light behind the print: warm or cool white, a colour, or any custom colour from the swatch."),
        ["LightPower"] = ("Light power", "Brightness of the light. 100 % is a typical LED strip or lamp; raise it to see what a stronger light would do."),
        ["Filament"] = ("Filament colour", "Colour of the filament you will print with, for a solid (100 % infill) print. Coloured filament blocks the other colours, so thick areas turn into a deep version of its colour."),
    };
}

/// <summary>A small schematic drawing for one help topic. Drawn in code so it follows the theme.</summary>
public sealed class Sketch : Control
{
    public string Topic { get; set; } = "";

    private IBrush _ink = Brushes.Black, _faint = Brushes.Gray, _accent = Brushes.Orange, _plastic = Brushes.LightGray,
        _basefill = Brushes.SteelBlue, _glow = Brushes.Gold;
    private IPen _pen = new Pen(Brushes.Black), _thin = new Pen(Brushes.Gray), _accentPen = new Pen(Brushes.Orange);

    public override void Render(DrawingContext ctx)
    {
        bool dark = ActualThemeVariant == ThemeVariant.Dark;
        _ink = new SolidColorBrush(dark ? Color.Parse("#E6E6E6") : Color.Parse("#202020"));
        _faint = new SolidColorBrush(dark ? Color.Parse("#8A8A8A") : Color.Parse("#9A9A9A"));
        _accent = new SolidColorBrush(dark ? Color.Parse("#FFB347") : Color.Parse("#D06F00"));
        _plastic = new SolidColorBrush(dark ? Color.Parse("#AEB4BE") : Color.Parse("#C9CED6"));
        _basefill = new SolidColorBrush(dark ? Color.Parse("#4F77B3") : Color.Parse("#8FAEDC"));
        _glow = new SolidColorBrush(Color.Parse("#FFD24D"));
        _pen = new Pen(_ink, 1.3, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _thin = new Pen(_faint, 1, lineCap: PenLineCap.Round);
        _accentPen = new Pen(_accent, 1.5, lineCap: PenLineCap.Round);

        switch (Topic)
        {
            case "Positive": Polarity(ctx, false); break;
            case "Negative": Polarity(ctx, true); break;
            case "Mirror": Transform(ctx, Matrix.CreateScale(-1, 1)); break;
            case "Flip": Transform(ctx, Matrix.CreateScale(1, -1)); break;
            case "Rotate": Transform(ctx, Matrix.CreateRotation(Math.PI / 2)); break;
            case "RepeatX": Front(ctx, "repeat", 3, 1); break;
            case "RepeatY": Front(ctx, "repeat", 1, 2); break;
            case "Gamma": Gamma(ctx); break;
            case "Shape": Shapes(ctx); break;
            case "MaxSize": Front(ctx, "size"); break;
            case "Border": Front(ctx, "border"); break;
            case "Thickness": Section(ctx, "thickness"); break;
            case "MinThickness": Section(ctx, "min"); break;
            case "Resolution": Section(ctx, "resolution"); break;
            case "Backlit": Section(ctx, "light"); break;
            case "CurveAngle": CurveAngle(ctx); break;
            case "StandDepth": Stand(ctx, true); break;
            case "StandHeight": Stand(ctx, false); break;
            case "LightSource": Base(ctx, "led", "source"); break;
            case "SupportHeight": Base(ctx, "led", "height"); break;
            case "SlotDepth": Base(ctx, "led", "slot"); break;
            case "SlotClearance": Base(ctx, "led", "clearance"); break;
            case "SupportWall": Base(ctx, "led", "wall"); break;
            case "SupportMinDepth": Base(ctx, "led", "depth"); break;
            case "CableDiameter": Base(ctx, "led", "cable"); break;
            case "LedChannelWidth": Base(ctx, "led", "ledwidth"); break;
            case "LedChannelDepth": Base(ctx, "led", "leddepth"); break;
            case "LampPreset": LampTypes(ctx); break;
            case "LampDiameter": Base(ctx, "lamp", "lampdia"); break;
            case "LampDepth": Base(ctx, "lamp", "lampdepth"); break;
            case "LampThrough": Base(ctx, "through", "through"); break;
            case "ShowLight": Base(ctx, "led", "show"); break;
            case "Layout": Layouts(ctx); break;
            case "LightColor": Lights(ctx, "color"); break;
            case "LightPower": Lights(ctx, "power"); break;
            case "Filament": Lights(ctx, "filament"); break;
        }
    }

    // ---------------------------------------------------------------- helpers

    private void Line(DrawingContext ctx, double x1, double y1, double x2, double y2, IPen? pen = null) =>
        ctx.DrawLine(pen ?? _pen, new Point(x1, y1), new Point(x2, y2));

    private static StreamGeometry Path(IEnumerable<Point> points, bool closed)
    {
        var g = new StreamGeometry();
        using var c = g.Open();
        bool first = true;
        foreach (var p in points)
        {
            if (first) { c.BeginFigure(p, closed); first = false; }
            else c.LineTo(p);
        }
        c.EndFigure(closed);
        return g;
    }

    private void Poly(DrawingContext ctx, IBrush? fill, IPen? pen, params Point[] pts) =>
        ctx.DrawGeometry(fill, pen, Path(pts, true));

    private void Polyline(DrawingContext ctx, IPen pen, IEnumerable<Point> pts) =>
        ctx.DrawGeometry(null, pen, Path(pts, false));

    private void Text(DrawingContext ctx, string text, double x, double y, IBrush? brush = null, double size = 10,
        bool centre = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush ?? _ink);
        if (centre) x -= ft.Width / 2;
        else if (right) x -= ft.Width;
        ctx.DrawText(ft, new Point(x, y));
    }

    /// <summary>Dimension line with arrowheads and a label beside it.</summary>
    private void Dim(DrawingContext ctx, double x1, double y1, double x2, double y2, string label, double labelDx = 0, double labelDy = 0)
    {
        var a = new Point(x1, y1);
        var b = new Point(x2, y2);
        ctx.DrawLine(_accentPen, a, b);
        var d = b - a;
        double len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (len > 8)
        {
            var u = d / len;
            var n = new Vector(-u.Y, u.X);
            foreach (var (tip, dir) in new[] { (a, u), (b, -u) })
            {
                ctx.DrawLine(_accentPen, tip, tip + dir * 5 + n * 2.5);
                ctx.DrawLine(_accentPen, tip, tip + dir * 5 - n * 2.5);
            }
        }
        bool vertical = Math.Abs(d.Y) > Math.Abs(d.X);
        var mid = a + d / 2;
        if (vertical) Text(ctx, label, mid.X + 5 + labelDx, mid.Y - 7 + labelDy, _accent);
        else Text(ctx, label, mid.X + labelDx, mid.Y + 3 + labelDy, _accent, centre: true);
    }

    private void Arrow(DrawingContext ctx, double x1, double y1, double x2, double y2, IPen? pen = null)
    {
        pen ??= _pen;
        var a = new Point(x1, y1);
        var b = new Point(x2, y2);
        ctx.DrawLine(pen, a, b);
        var d = b - a;
        double len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (len < 1) return;
        var u = d / len;
        var n = new Vector(-u.Y, u.X);
        ctx.DrawLine(pen, b, b - u * 6 + n * 3);
        ctx.DrawLine(pen, b, b - u * 6 - n * 3);
    }

    private void Sun(DrawingContext ctx, double x, double y, double r = 6)
    {
        ctx.DrawEllipse(_glow, null, new Point(x, y), r, r);
        var p = new Pen(_glow, 1.5, lineCap: PenLineCap.Round);
        for (int k = 0; k < 8; k++)
        {
            double a = k * Math.PI / 4;
            Line(ctx, x + Math.Cos(a) * (r + 3), y + Math.Sin(a) * (r + 3), x + Math.Cos(a) * (r + 7), y + Math.Sin(a) * (r + 7), p);
        }
    }

    private void Mountains(DrawingContext ctx, Rect r)
    {
        var fill = new SolidColorBrush(Color.Parse("#60808080"));
        Poly(ctx, fill, _thin, new Point(r.Left, r.Bottom), new Point(r.Left + r.Width * 0.3, r.Top + r.Height * 0.35),
            new Point(r.Left + r.Width * 0.5, r.Top + r.Height * 0.7), new Point(r.Left + r.Width * 0.72, r.Top + r.Height * 0.25),
            new Point(r.Right, r.Bottom));
        ctx.DrawEllipse(_faint, null, new Point(r.Left + r.Width * 0.8, r.Top + r.Height * 0.3), r.Width * 0.06, r.Width * 0.06);
    }

    // ---------------------------------------------------------------- scenes

    /// <summary>Side cut through a flat lithophane: picture brightness on the left, relief on the right.</summary>
    private void Section(DrawingContext ctx, string highlight)
    {
        const double back = 90, tMin = 14, tMax = 46, top = 12, bottom = 118, imgTop = 28, imgBottom = 102;
        double Thick(double y) => tMin + (tMax - tMin) * (y - imgTop) / (imgBottom - imgTop);

        var pts = new List<Point> { new(back, top), new(back + tMax, top), new(back + tMax, imgTop) };
        for (double y = imgTop; y <= imgBottom; y += 4) pts.Add(new Point(back + Thick(y), y));
        pts.Add(new Point(back + tMax, imgBottom)); pts.Add(new Point(back + tMax, bottom)); pts.Add(new Point(back, bottom));
        ctx.DrawGeometry(_plastic, _pen, Path(pts, true));

        // The picture that produced it.
        ctx.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Black, 1) },
        }, _thin, new Rect(44, imgTop, 18, imgBottom - imgTop));
        Text(ctx, "picture", 53, imgBottom + 4, _faint, 9, centre: true);
        Text(ctx, "bright", 40, imgTop - 4, _faint, 9, right: true);
        Text(ctx, "dark", 40, imgBottom - 10, _faint, 9, right: true);
        Text(ctx, "light", 12, 56, _faint, 9);
        Arrow(ctx, 10, 70, 38, 70, _thin);

        switch (highlight)
        {
            case "thickness":
                Dim(ctx, back, 110, back + tMax, 110, "Thickness", 0, 4);
                Text(ctx, "border & darkest", back + tMax + 6, 104, _accent, 9);
                break;
            case "min":
                Dim(ctx, back, 36, back + tMin, 36, "", 0, 0);
                Text(ctx, "Thinnest layer", back + tMin + 8, 30, _accent, 9);
                Text(ctx, "(brightest)", back + tMin + 8, 41, _accent, 9);
                break;
            case "resolution":
                for (double y = imgTop; y <= imgBottom; y += 8)
                    ctx.DrawEllipse(_accent, null, new Point(back + Thick(y), y), 1.6, 1.6);
                Dim(ctx, back + Thick(52) + 10, 52, back + Thick(60) + 10, 60, "1 / resolution", 6, -4);
                Text(ctx, "one mesh point", 150, 100, _accent, 9);
                Text(ctx, "every 0.2 mm at 5/mm", 150, 111, _accent, 9);
                break;
            case "light":
                Sun(ctx, 20, 70);
                var bright = new Pen(_glow, 2.2, lineCap: PenLineCap.Round);
                var dim = new Pen(_glow, 0.8, lineCap: PenLineCap.Round);
                for (double y = imgTop + 8; y < imgBottom; y += 14)
                {
                    double f = 1 - (y - imgTop) / (imgBottom - imgTop); // thin = bright
                    var p = new Pen(_glow, 0.6 + 2 * f, lineCap: PenLineCap.Round);
                    Arrow(ctx, back + Thick(y) + 2, y, back + Thick(y) + 10 + 36 * f, y, p);
                }
                Text(ctx, "thin glows", 150, 30, _accent, 9);
                Text(ctx, "thick stays dark", 150, 96, _accent, 9);
                _ = bright; _ = dim;
                break;
        }
    }

    private void Polarity(DrawingContext ctx, bool negative)
    {
        const double back = 90, tMin = 14, tMax = 46, top = 12, bottom = 118, imgTop = 28, imgBottom = 102;
        double Thick(double y)
        {
            double f = (y - imgTop) / (imgBottom - imgTop);
            if (negative) f = 1 - f;
            return tMin + (tMax - tMin) * f;
        }
        var pts = new List<Point> { new(back, top), new(back + tMax, top), new(back + tMax, imgTop) };
        for (double y = imgTop; y <= imgBottom; y += 4) pts.Add(new Point(back + Thick(y), y));
        pts.Add(new Point(back + tMax, imgBottom)); pts.Add(new Point(back + tMax, bottom)); pts.Add(new Point(back, bottom));
        ctx.DrawGeometry(_plastic, _pen, Path(pts, true));
        ctx.DrawRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Colors.Black, 1) },
        }, _thin, new Rect(44, imgTop, 18, imgBottom - imgTop));
        Text(ctx, "picture", 53, imgBottom + 4, _faint, 9, centre: true);
        Text(ctx, "bright", 40, imgTop - 4, _faint, 9, right: true);
        Text(ctx, "dark", 40, imgBottom - 10, _faint, 9, right: true);
        Text(ctx, negative ? "bright = thick" : "dark = thick", 150, negative ? 30 : 92, _accent, 9);
        Text(ctx, negative ? "dark = thin" : "bright = thin", 150, negative ? 92 : 30, _accent, 9);
        Arrow(ctx, 66, negative ? 36 : 94, back + (negative ? tMax : tMax) - 4, negative ? 36 : 94, _thin);
    }

    /// <summary>Before → after with a letter transformed.</summary>
    private void Transform(DrawingContext ctx, Matrix m)
    {
        void Frame(double x, Matrix t)
        {
            var r = new Rect(x, 25, 80, 80);
            ctx.DrawRectangle(_plastic, _pen, r);
            var c = r.Center;
            using (ctx.PushTransform(Matrix.CreateTranslation(-c.X, -c.Y) * t * Matrix.CreateTranslation(c.X, c.Y)))
            {
                var ft = new FormattedText("R", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(Typeface.Default.FontFamily, weight: FontWeight.Bold), 44, _ink);
                ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
            }
        }
        Frame(20, Matrix.Identity);
        Arrow(ctx, 108, 65, 132, 65, _accentPen);
        Frame(140, m);
    }

    /// <summary>Front view of a flat lithophane with border; image area optionally tiled.</summary>
    private void Front(DrawingContext ctx, string highlight, int repX = 1, int repY = 1)
    {
        var outer = new Rect(40, 8, 160, 98);
        var inner = outer.Deflate(16);
        ctx.DrawRectangle(_plastic, _pen, outer);
        ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#30000000")), _thin, inner);
        double tw = inner.Width / repX, th = inner.Height / repY;
        for (int j = 0; j < repY; j++)
            for (int i = 0; i < repX; i++)
                Mountains(ctx, new Rect(inner.X + i * tw, inner.Y + j * th, tw, th).Deflate(2));
        switch (highlight)
        {
            case "border":
                Dim(ctx, 48, outer.Top, 48, inner.Top, "", 0, 0);
                Text(ctx, "Border", 54, 10, _accent, 9);
                Dim(ctx, inner.Right, 60, outer.Right, 60, "", 0, 0);
                Text(ctx, "solid frame at full thickness, all round", 120, 112, _accent, 9, centre: true);
                break;
            case "size":
                Dim(ctx, inner.Left, 113, inner.Right, 113, "Maximum size = longest side of the picture");
                Dim(ctx, 210, inner.Top, 210, inner.Bottom, "", 0, 0);
                Text(ctx, "(border", 214, 52, _faint, 8); Text(ctx, "not incl.)", 214, 62, _faint, 8);
                break;
            case "repeat":
                Text(ctx, repX > 1 ? "Repeat X = 3" : "Repeat Y = 2", 120, 113, _accent, 9, centre: true);
                break;
        }
    }

    private void Gamma(DrawingContext ctx)
    {
        var o = new Point(50, 110);
        Arrow(ctx, o.X, o.Y, o.X + 140, o.Y, _thin);
        Arrow(ctx, o.X, o.Y, o.X, o.Y - 95, _thin);
        Text(ctx, "brightness in picture →", o.X + 70, 113, _faint, 9, centre: true);
        Text(ctx, "thin", 46, 10, _faint, 9, right: true);
        Text(ctx, "thick", 46, 100, _faint, 9, right: true);
        foreach (var (g, pen, label, lx) in new[] { (0.5, _thin, "γ 0.5", 0.33), (1.0, _pen, "γ 1", 0.6), (2.2, _accentPen, "γ 2", 0.85) })
        {
            var pts = new List<Point>();
            for (double x = 0; x <= 1.0001; x += 0.05)
                pts.Add(new Point(o.X + x * 130, o.Y - Math.Pow(x, g) * 85));
            Polyline(ctx, pen, pts);
            Text(ctx, label, o.X + lx * 130 + 4, o.Y - Math.Pow(lx, g) * 85 - 4, pen.Brush, 9);
        }
    }

    private void Shapes(DrawingContext ctx)
    {
        string[] names = { "Flat", "Outer", "Inner", "Cylinder", "Heart", "Dome" };
        for (int k = 0; k < 6; k++)
        {
            double cx = 25 + k * 38, cy = 55;
            switch (k)
            {
                case 0: Line(ctx, cx - 14, cy, cx + 14, cy); break;
                case 1: Polyline(ctx, _pen, Arc(cx, cy + 10, 16, 220, 320)); break;
                case 2: Polyline(ctx, _pen, Arc(cx, cy - 10, 16, 40, 140)); break;
                case 3: ctx.DrawEllipse(null, _pen, new Point(cx, cy), 14, 14); break;
                case 4: Polyline(ctx, _pen, Heart(cx, cy, 0.9)); break;
                case 5:
                    Polyline(ctx, _pen, Arc(cx, cy + 10, 15, 180, 360));
                    Line(ctx, cx - 15, cy + 10, cx + 15, cy + 10);
                    break;
            }
            Text(ctx, names[k], cx, 80, _faint, 8, centre: true);
        }
        Text(ctx, "viewed from above (dome: from the side)", 120, 100, _faint, 9, centre: true);
        Text(ctx, "picture faces outwards; cylinder, heart and dome go over a light", 120, 113, _accent, 8, centre: true);
    }

    private static IEnumerable<Point> Arc(double cx, double cy, double r, double fromDeg, double toDeg)
    {
        int n = 24;
        for (int i = 0; i <= n; i++)
        {
            double a = (fromDeg + (toDeg - fromDeg) * i / n) * Math.PI / 180;
            yield return new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
    }

    private static IEnumerable<Point> Heart(double cx, double cy, double s)
    {
        for (int i = 0; i <= 40; i++)
        {
            double t = 2 * Math.PI * i / 40;
            double x = 16 * Math.Pow(Math.Sin(t), 3);
            double y = 13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t);
            yield return new Point(cx + x * s, cy - y * s + 2);
        }
    }

    private void CurveAngle(DrawingContext ctx)
    {
        double cx = 120, cy = 118, r = 85, a0 = 210, a1 = 330; // 120° arc
        Polyline(ctx, new Pen(_plastic, 7), Arc(cx, cy, r, a0, a1));
        Polyline(ctx, _pen, Arc(cx, cy, r, a0, a1));
        Line(ctx, cx, cy, cx + r * Math.Cos(a0 * Math.PI / 180), cy + r * Math.Sin(a0 * Math.PI / 180), _thin);
        Line(ctx, cx, cy, cx + r * Math.Cos(a1 * Math.PI / 180), cy + r * Math.Sin(a1 * Math.PI / 180), _thin);
        Polyline(ctx, _accentPen, Arc(cx, cy, 30, a0, a1));
        Text(ctx, "Curve angle", cx, cy - 55, _accent, 10, centre: true);
        Text(ctx, "top view", 12, 116, _faint, 9);
        Text(ctx, "picture length stays the same; the angle sets how tight it bends", 120, 2, _faint, 8, centre: true);
    }

    private void Stand(DrawingContext ctx, bool depth)
    {
        ctx.DrawRectangle(_plastic, _pen, new Rect(112, 12, 9, 84));
        ctx.DrawRectangle(_plastic, _pen, new Rect(88, 96, 56, 14));
        Line(ctx, 30, 110, 210, 110, _thin);
        Text(ctx, "side view", 12, 8, _faint, 9);
        if (depth) Dim(ctx, 88, 122, 144, 122, "Stand depth", 0, -16);
        else Dim(ctx, 156, 96, 156, 110, "Stand height", 0, 0);
    }

    /// <summary>Side cut through a light support with the lithophane in its slot. Light on the left (behind), viewer on the right.</summary>
    private void Base(DrawingContext ctx, string mode, string highlight)
    {
        const double top = 70, bottom = 120, left = 40, right = 200, slotL = 147, slotR = 161, slotBottom = 88;
        var slotRight = new[] { new Point(slotL, top), new Point(slotL, slotBottom), new Point(slotR, slotBottom), new Point(slotR, top), new Point(right, top), new Point(right, bottom) };

        switch (mode)
        {
            case "led":
                Poly(ctx, _basefill, _pen, new Point(left, top), new Point(70, top), new Point(70, 95), new Point(90, 95), new Point(90, 112), new Point(left, 112));
                Poly(ctx, _basefill, _pen, new[] { new Point(98, 95), new Point(122, 95), new Point(122, top) }.Concat(slotRight).Concat(new[] { new Point(98, bottom) }).ToArray());
                ctx.DrawRectangle(_faint, null, new Rect(74, 91, 44, 4));
                for (double x = 80; x < 118; x += 14) ctx.DrawRectangle(_glow, null, new Rect(x, 88, 6, 3));
                break;
            case "lamp":
                Poly(ctx, _basefill, _pen, new Point(left, top), new Point(60, top), new Point(60, 100), new Point(92, 100), new Point(92, 112), new Point(left, 112));
                Poly(ctx, _basefill, _pen, new[] { new Point(100, 100), new Point(134, 100), new Point(134, top) }.Concat(slotRight).Concat(new[] { new Point(100, bottom) }).ToArray());
                ctx.DrawRectangle(_faint, _thin, new Rect(70, 84, 54, 16));   // tea light
                ctx.DrawRectangle(_glow, null, new Rect(72, 82, 50, 3));
                break;
            case "through":
                Poly(ctx, _basefill, _pen, new Point(left, top), new Point(82, top), new Point(82, bottom), new Point(left, bottom));
                Poly(ctx, _basefill, _pen, new[] { new Point(112, top) }.Concat(slotRight).Concat(new[] { new Point(112, bottom) }).ToArray());
                ctx.DrawRectangle(_faint, _thin, new Rect(85, 74, 24, 44));   // socket
                ctx.DrawRectangle(_faint, _thin, new Rect(91, 62, 12, 12));   // neck
                ctx.DrawEllipse(_glow, _thin, new Point(97, 46), 16, 16);     // bulb
                Line(ctx, 97, 118, 97, 128, _thin);
                break;
        }
        // The lithophane standing in the slot.
        ctx.DrawRectangle(_plastic, _pen, new Rect(150, 18, 8, slotBottom - 18));
        Text(ctx, "back", left, 58, _faint, 9);
        Text(ctx, "front", right, 58, _faint, 9, right: true);
        if (highlight != "depth") Line(ctx, 20, bottom, 220, bottom, _thin);

        switch (highlight)
        {
            case "source": Text(ctx, "LED strip in a channel behind", 78, 30, _accent, 9, centre: true); Arrow(ctx, 96, 42, 96, 84, _accentPen); break;
            case "height": Dim(ctx, 212, top, 212, bottom, "Base height", -2, 0); break;
            case "slot": Dim(ctx, 172, top, 172, slotBottom, "Slot depth", 0, 0); Text(ctx, "border ≥ slot depth", 150, 4, _faint, 9); break;
            case "clearance":
                Dim(ctx, slotL, 80, 150, 80, "", 0, 0); Dim(ctx, 158, 80, slotR, 80, "", 0, 0);
                Text(ctx, "gap on each side", 154, 100, _accent, 9, centre: true);
                Arrow(ctx, 154, 99, 149, 84, _accentPen); Arrow(ctx, 156, 99, 159, 84, _accentPen);
                break;
            case "wall": Dim(ctx, 122, 80, slotL, 80, "", 0, 0); Text(ctx, "Wall thickness", 134, 100, _accent, 9, centre: true); Arrow(ctx, 134, 99, 134, 84, _accentPen); break;
            case "depth": Dim(ctx, left, 127, right, 127, "Min. base depth (front to back)", 0, -16); break;
            case "cable":
                Dim(ctx, 90, 104, 98, 104, "", 0, 0); Text(ctx, "Cable hole Ø", 94, 36, _accent, 9, centre: true);
                Arrow(ctx, 94, 48, 94, 90, _accentPen);
                Text(ctx, "groove under the base", 44, 122, _accent, 8);
                break;
            case "ledwidth": Dim(ctx, 70, 64, 122, 64, "LED channel width", 0, -26); break;
            case "leddepth": Dim(ctx, 132, top, 132, 95, ""); Text(ctx, "LED channel depth", 110, 40, _accent, 9, centre: true); Arrow(ctx, 126, 52, 131, 68, _accentPen); break;
            case "lampdia": Dim(ctx, 60, 64, 134, 64, "Lamp hole Ø", 0, -26); break;
            case "lampdepth": Dim(ctx, 140, top, 140, 100, ""); Text(ctx, "Recess depth", 110, 40, _accent, 9, centre: true); Arrow(ctx, 130, 52, 139, 68, _accentPen); break;
            case "through": Arrow(ctx, 97, 20, 97, 36, _accentPen); Text(ctx, "socket fixed from below", 120, 2, _accent, 9, centre: true); break;
            case "show": Text(ctx, "drawn in the preview only", 120, 30, _accent, 9, centre: true); Arrow(ctx, 110, 42, 98, 84, _accentPen); break;
        }
    }

    private void LampTypes(DrawingContext ctx)
    {
        // Recess: tea light.
        ctx.DrawRectangle(_basefill, _pen, new Rect(14, 70, 92, 46));
        Poly(ctx, _plastic, null, new Point(36, 70), new Point(84, 70), new Point(84, 96), new Point(36, 96));
        ctx.DrawRectangle(_faint, _thin, new Rect(40, 82, 40, 14));
        ctx.DrawEllipse(_glow, null, new Point(60, 76), 3, 5);
        Text(ctx, "Tea light / puck", 60, 20, _ink, 9, centre: true);
        Text(ctx, "sit in a recess", 60, 32, _faint, 9, centre: true);
        // Through: socket + bulb.
        ctx.DrawRectangle(_basefill, _pen, new Rect(134, 70, 92, 46));
        ctx.DrawRectangle(_plastic, null, new Rect(170, 70, 20, 46));
        ctx.DrawRectangle(_faint, _thin, new Rect(172, 74, 16, 42));
        ctx.DrawRectangle(_faint, _thin, new Rect(175, 62, 10, 12));
        ctx.DrawEllipse(_glow, _thin, new Point(180, 48), 13, 13);
        Text(ctx, "E14 / E27 / M10", 180, 20, _ink, 9, centre: true);
        Text(ctx, "go through the base", 180, 32, _faint, 9, centre: true);
    }

    private void Layouts(DrawingContext ctx)
    {
        void Panel(double x, double y) => ctx.DrawRectangle(_plastic, _pen, new Rect(x, y, 6, 40));
        void Box(double x, double y) => ctx.DrawRectangle(_basefill, _pen, new Rect(x, y, 40, 14));
        // Separate files
        Panel(30, 30); Box(50, 56);
        Text(ctx, "2 files", 50, 76, _faint, 9, centre: true);
        Text(ctx, "Separate", 50, 100, _ink, 9, centre: true);
        // Side by side
        Panel(96, 36); Box(110, 62);
        Text(ctx, "1 file", 120, 80, _faint, 9, centre: true);
        Text(ctx, "Side by side", 120, 100, _ink, 9, centre: true);
        // Assembled
        Box(170, 62); Panel(187, 26);
        Text(ctx, "1 piece", 190, 80, _faint, 9, centre: true);
        Text(ctx, "Assembled", 190, 100, _ink, 9, centre: true);
        Line(ctx, 10, 118, 230, 118, _thin);
    }

    private void Lights(DrawingContext ctx, string what)
    {
        void Bulb(double x, double y, double r, IBrush fill, double rays)
        {
            ctx.DrawEllipse(fill, _thin, new Point(x, y), r, r);
            ctx.DrawRectangle(_faint, _thin, new Rect(x - r * 0.4, y + r - 2, r * 0.8, r * 0.6));
            if (rays <= 0) return;
            var p = new Pen(fill, 1.5, lineCap: PenLineCap.Round);
            for (int k = 0; k < 7; k++)
            {
                double a = Math.PI + k * Math.PI / 6;
                Line(ctx, x + Math.Cos(a) * (r + 3), y + Math.Sin(a) * (r + 3), x + Math.Cos(a) * (r + 3 + rays), y + Math.Sin(a) * (r + 3 + rays), p);
            }
        }
        switch (what)
        {
            case "color":
                var cols = new[] { "#FFA957", "#FFD1A3", "#FFFEFA", "#3060FF", "#FF2A14" };
                var names = new[] { "2700 K", "4000 K", "6500 K", "blue", "red" };
                for (int k = 0; k < cols.Length; k++)
                {
                    Bulb(28 + k * 46, 58, 12, new SolidColorBrush(Color.Parse(cols[k])), 6);
                    Text(ctx, names[k], 28 + k * 46, 92, _faint, 9, centre: true);
                }
                Text(ctx, "warm ← → cool, or any colour", 120, 112, _accent, 9, centre: true);
                break;
            case "power":
                for (int k = 0; k < 3; k++)
                {
                    Bulb(50 + k * 70, 62, 12, _glow, 4 + k * 9);
                    Text(ctx, new[] { "50 %", "100 %", "250 %" }[k], 50 + k * 70, 98, _faint, 9, centre: true);
                }
                Text(ctx, "same print, stronger light = brighter picture", 120, 114, _accent, 9, centre: true);
                break;
            case "filament":
                // A spool with the filament colour and three thickness samples.
                ctx.DrawEllipse(_faint, _pen, new Point(50, 60), 30, 30);
                ctx.DrawEllipse(new SolidColorBrush(Color.Parse("#E03030")), _thin, new Point(50, 60), 22, 22);
                ctx.DrawEllipse(_plastic, _thin, new Point(50, 60), 7, 7);
                Text(ctx, "filament colour", 50, 98, _faint, 9, centre: true);
                var reds = new[] { "#FF9080", "#D03020", "#601008" };
                for (int k = 0; k < 3; k++)
                {
                    ctx.DrawRectangle(new SolidColorBrush(Color.Parse(reds[k])), _thin, new Rect(110 + k * 38, 72 - k * 10, 26, 10 + k * 20));
                    Text(ctx, new[] { "thin", "mid", "thick" }[k], 123 + k * 38, 108, _faint, 9, centre: true);
                }
                Text(ctx, "lit: thin glows, thick is deep colour", 160, 30, _accent, 9, centre: true);
                break;
        }
    }
}
