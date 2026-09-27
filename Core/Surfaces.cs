using System.Numerics;

namespace LithophaneGUI.Core;

/// <summary>
/// Maps unrolled coordinates (x along the width, z up, both in mm) to a point on the model's
/// back surface plus the direction in which the relief grows.
/// </summary>
public abstract class Surface
{
    public abstract bool ClosedU { get; }
    public abstract (Vector3 Pos, Vector3 Normal) Eval(double x, double z);

    public static Surface Create(LithophaneSettings s, double totalW, double totalH)
    {
        double angle = Math.Clamp(s.CurveAngle, 1, 359) * Math.PI / 180;
        return s.Shape switch
        {
            ShapeType.Flat => new ProfileSurface(t => new Vector2((float)t, 0), false, totalW),
            ShapeType.OuterCurve => new ProfileSurface(t =>
            {
                double p = (t - 0.5) * angle;
                return new Vector2((float)Math.Sin(p), (float)(1 - Math.Cos(p)));
            }, false, totalW),
            ShapeType.InnerCurve => new ProfileSurface(t =>
            {
                double p = (t - 0.5) * angle;
                return new Vector2((float)Math.Sin(p), (float)(Math.Cos(p) - 1));
            }, false, totalW),
            // Closed profiles run counter-clockwise (seen from above), starting at the back,
            // so the middle of the image faces the -Y side.
            ShapeType.Cylinder => new ProfileSurface(t =>
            {
                double p = Math.PI / 2 + 2 * Math.PI * t;
                return new Vector2((float)Math.Cos(p), (float)Math.Sin(p));
            }, true, totalW),
            ShapeType.Heart => new ProfileSurface(t =>
            {
                double p = 2 * Math.PI * t;
                double sn = Math.Sin(p);
                double x = -16 * sn * sn * sn;
                double y = 13 * Math.Cos(p) - 5 * Math.Cos(2 * p) - 2 * Math.Cos(3 * p) - Math.Cos(4 * p);
                return new Vector2((float)x, (float)y);
            }, true, totalW),
            ShapeType.Dome => new DomeSurface(totalW, totalH),
            _ => throw new ArgumentOutOfRangeException(nameof(s)),
        };
    }

    /// <summary>The curve along which the bottom edge of the lithophane runs (Z = 0).</summary>
    public static ProfileSurface Footprint(LithophaneSettings s, double totalW, double totalH) =>
        (ProfileSurface)Create(s.Shape == ShapeType.Dome ? s with { Shape = ShapeType.Cylinder } : s, totalW, totalH);
}

/// <summary>
/// A vertical extrusion of a 2D curve, reparameterised by arc length and scaled so that
/// its length equals the unrolled width. The relief grows to the right of the travel direction.
/// </summary>
public sealed class ProfileSurface : Surface
{
    private const int Samples = 8192;
    private readonly Vector2[] _points;
    private readonly double[] _cumLength;
    private readonly bool _closed;
    private readonly double _length;

    public override bool ClosedU => _closed;
    public double Length => _length;

    public ProfileSurface(Func<double, Vector2> curve, bool closed, double length)
    {
        _closed = closed;
        _length = length;
        int n = closed ? Samples + 1 : Samples; // closed: last point repeats the first
        var pts = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            double t = closed ? (double)k / Samples : (double)k / (Samples - 1);
            pts[k] = curve(closed && k == Samples ? 0 : t);
        }

        var cum = new double[n];
        for (int k = 1; k < n; k++)
            cum[k] = cum[k - 1] + Vector2.Distance(pts[k], pts[k - 1]);

        double scale = length / cum[n - 1];
        for (int k = 0; k < n; k++)
        {
            pts[k] *= (float)scale;
            cum[k] *= scale;
        }
        _points = pts;
        _cumLength = cum;
    }

    public Vector2 PointAt(double s)
    {
        if (_closed)
        {
            s %= _length;
            if (s < 0) s += _length;
        }
        else
        {
            s = Math.Clamp(s, 0, _length);
        }

        int idx = Array.BinarySearch(_cumLength, s);
        if (idx >= 0) return _points[idx];
        idx = ~idx;
        if (idx <= 0) return _points[0];
        if (idx >= _points.Length) return _points[^1];
        double a = _cumLength[idx - 1], b = _cumLength[idx];
        float f = b > a ? (float)((s - a) / (b - a)) : 0f;
        return Vector2.Lerp(_points[idx - 1], _points[idx], f);
    }

    public Vector2 NormalAt(double s)
    {
        double h = Math.Max(_length / Samples * 2, 1e-4);
        double s0 = s - h, s1 = s + h;
        if (!_closed)
        {
            s0 = Math.Max(0, s0);
            s1 = Math.Min(_length, s1);
        }
        var tangent = PointAt(s1) - PointAt(s0);
        if (tangent.LengthSquared() < 1e-12) tangent = new Vector2(1, 0);
        tangent = Vector2.Normalize(tangent);
        return new Vector2(tangent.Y, -tangent.X);
    }

    public override (Vector3 Pos, Vector3 Normal) Eval(double x, double z)
    {
        var p = PointAt(x);
        var n = NormalAt(x);
        return (new Vector3(p.X, p.Y, (float)z), new Vector3(n.X, n.Y, 0));
    }
}

/// <summary>
/// A spherical cap open at the top: the image wraps around the equator and climbs towards the pole.
/// </summary>
public sealed class DomeSurface : Surface
{
    private readonly double _radius, _width, _height, _maxLat;

    public override bool ClosedU => true;

    public DomeSurface(double width, double height)
    {
        _width = width;
        _height = height;
        _radius = width / (2 * Math.PI);
        _maxLat = Math.Min(height / _radius, 80 * Math.PI / 180);
    }

    public override (Vector3 Pos, Vector3 Normal) Eval(double x, double z)
    {
        double lon = Math.PI / 2 + 2 * Math.PI * x / _width;
        double lat = z / _height * _maxLat;
        var n = new Vector3(
            (float)(Math.Cos(lat) * Math.Cos(lon)),
            (float)(Math.Cos(lat) * Math.Sin(lon)),
            (float)Math.Sin(lat));
        return (n * (float)_radius, n);
    }
}
