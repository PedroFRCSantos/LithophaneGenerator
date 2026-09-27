using System.Numerics;

namespace LithophaneGUI.Core;

/// <summary>
/// Builds a base that holds the lithophane in a slot and houses the light source: a channel
/// (or tray, for closed shapes) for an LED strip / string, or a round recess / hole for a lamp.
/// A cable hole leads from the light down to a groove under the base.
///
/// Coordinates match the lithophane mesh before it is placed on the bed: the lithophane's bottom
/// edge is at Z = 0 and occupies offsets 0..Thickness along the footprint normal. The light sits
/// on the negative-offset side (behind a flat/curved lithophane, inside a closed one).
/// </summary>
public static class LightSupportGenerator
{
    private sealed record Geometry(
        double T, double C, double W, double ZBot, double ZTop, double L,
        double DBack, double DFront, double LightFloor, Vector2 HolePos, Vector2 GrooveDir,
        Vector2 LampCenter, double LampRadius);

    /// <param name="maxCells">Voxel budget; the grid spacing is chosen to stay within it.</param>
    public static Mesh Generate(LithophaneSettings s, ProfileSurface profile, int maxCells,
        List<string> notes, CancellationToken ct = default)
    {
        bool closed = profile.ClosedU;
        double L = profile.Length;
        double T = Math.Max(0.1, s.Thickness);
        double c = Math.Clamp(s.SlotClearance, 0, 5);
        double w = Math.Max(0.8, s.SupportWall);
        double cable = Math.Max(2, s.CableDiameter);
        double slot = Math.Max(1, s.SlotDepth);

        // Enough material under the slot for the cable groove.
        double height = Math.Max(s.SupportHeight, slot + cable + 2);
        if (height > s.SupportHeight + 1e-6) notes.Add($"Base height raised to {height:0.#} mm to fit the cable groove");
        double zTop = slot, zBot = slot - height;
        double maxRecess = height - cable - 2;

        bool led = s.LightSupport == LightSupportType.LedStrip;
        bool through = !led && s.LampThroughHole;
        double lightDepth = led ? s.LedChannelDepth : s.LampDepth;
        if (!through && lightDepth > maxRecess)
        {
            lightDepth = maxRecess;
            notes.Add($"Light recess limited to {lightDepth:0.#} mm deep (increase base height for more)");
        }
        double lightFloor = through ? zBot - 1 : zTop - Math.Max(1, lightDepth);

        double dFront = T + c + w;
        double dBack;
        Vector2 holePos, grooveDir, lampCenter = Vector2.Zero;
        double lampRadius = Math.Max(1, s.LampDiameter / 2);

        if (closed)
        {
            dBack = double.NegativeInfinity;
            var (center, minDist) = InscribedCircle(profile);
            double maxLamp = minDist - c - w;
            if (!led && lampRadius > maxLamp)
            {
                lampRadius = Math.Max(1, maxLamp);
                notes.Add($"Lamp hole reduced to Ø{2 * lampRadius:0.#} mm to fit inside the lithophane");
            }
            lampCenter = center;
            holePos = center;
            grooveDir = profile.NormalAt(0); // towards the back (the image seam)
        }
        else
        {
            double mid = L / 2;
            var p = profile.PointAt(mid);
            var n = profile.NormalAt(mid);
            double lightBack, holeOffset;
            if (led)
            {
                double width = Math.Max(2, s.LedChannelWidth);
                lightBack = -c - w - width;
                holeOffset = -c - w - width / 2;
            }
            else
            {
                lightBack = -c - w - 2 * lampRadius;
                holeOffset = -c - w - lampRadius;
            }
            dBack = Math.Min(lightBack - w, dFront - Math.Max(s.SupportMinDepth, 0));
            lampCenter = p + n * (float)holeOffset;
            holePos = lampCenter;
            grooveDir = -n;
        }

        var g = new Geometry(T, c, w, zBot, zTop, L, dBack, dFront, lightFloor, holePos,
            Vector2.Normalize(grooveDir), lampCenter, lampRadius);

        // ---- sampling grid
        var (min, max) = FootprintBounds(profile, g, closed);
        double volume = (max.X - min.X) * (max.Y - min.Y) * (zTop - zBot);
        double h = Math.Max(0.2, Math.Cbrt(volume / Math.Max(1000, maxCells)));
        float pad = (float)(3 * h);
        var origin = new Vector3(min.X - pad, min.Y - pad, (float)zBot - pad);
        int nx = (int)Math.Ceiling((max.X - min.X + 2 * pad) / h) + 1;
        int ny = (int)Math.Ceiling((max.Y - min.Y + 2 * pad) / h) + 1;
        int nz = (int)Math.Ceiling((zTop - zBot + 2 * pad) / h) + 1;

        var field = new ProfileField(profile, closed ? 0 : c + w + 6 * h);
        var f = new float[nx * ny * nz];

        Parallel.For(0, ny, new ParallelOptions { CancellationToken = ct }, j =>
        {
            for (int i = 0; i < nx; i++)
            {
                var q = new Vector2((float)(origin.X + i * h), (float)(origin.Y + j * h));
                var (d, sPos) = field.Query(q);
                for (int k = 0; k < nz; k++)
                {
                    double z = origin.Z + k * h;
                    bool border = i == 0 || j == 0 || k == 0 || i == nx - 1 || j == ny - 1 || k == nz - 1;
                    f[(k * ny + j) * nx + i] = border ? 1f : (float)Sdf(s, g, closed, led, through, q, d, sPos, z);
                }
            }
        });
        ct.ThrowIfCancellationRequested();

        var mesh = SurfaceNets.Extract(f, nx, ny, nz, origin, (float)h);
        if (mesh.SignedVolume() < 0) mesh.FlipWinding();
        return mesh;
    }

    /// <summary>Approximate centre and radius of the largest circle inside a closed footprint.</summary>
    private static (Vector2 Center, double Radius) InscribedCircle(ProfileSurface profile)
    {
        const int samples = 720;
        var pts = new Vector2[samples];
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        for (int k = 0; k < samples; k++)
        {
            pts[k] = profile.PointAt(profile.Length * k / samples);
            min = Vector2.Min(min, pts[k]);
            max = Vector2.Max(max, pts[k]);
        }

        bool Inside(Vector2 q)
        {
            bool inside = false;
            for (int k = 0, m = samples - 1; k < samples; m = k++)
            {
                var a = pts[m];
                var b = pts[k];
                if ((a.Y > q.Y) != (b.Y > q.Y) && q.X < (b.X - a.X) * (q.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        var best = (min + max) / 2;
        double bestR = 0;
        const int grid = 64;
        for (int j = 1; j < grid; j++)
            for (int i = 1; i < grid; i++)
            {
                var q = min + (max - min) * new Vector2((float)i / grid, (float)j / grid);
                if (!Inside(q)) continue;
                double r = pts.Min(p => Vector2.Distance(p, q));
                if (r > bestR) { bestR = r; best = q; }
            }
        return (best, bestR);
    }

    private static double Slab(double v, double lo, double hi) => Math.Max(lo - v, v - hi);

    private static double Sdf(LithophaneSettings s, Geometry g, bool closed, bool led, bool through,
        Vector2 q, double d, double sPos, double z)
    {
        double c = g.C, w = g.W;
        double above = g.ZTop + 10;

        double body, slot, light;
        if (closed)
        {
            body = Math.Max(d - g.DFront, Slab(z, g.ZBot, g.ZTop));
            slot = Math.Max(Slab(d, -c, g.T + c), Slab(z, 0, above));
            light = led
                ? Math.Max(d - (-c - w), Slab(z, g.LightFloor, above))            // tray inside the ring
                : Math.Max(Vector2.Distance(q, g.LampCenter) - g.LampRadius, Slab(z, g.LightFloor, above));
        }
        else
        {
            double sLo = -c - w, sHi = g.L + c + w;
            body = Math.Max(Math.Max(Slab(d, g.DBack, g.DFront), Slab(sPos, sLo, sHi)), Slab(z, g.ZBot, g.ZTop));
            slot = Math.Max(Math.Max(Slab(d, -c, g.T + c), Slab(sPos, -c, g.L + c)), Slab(z, 0, above));
            if (led)
            {
                double width = Math.Max(2, s.LedChannelWidth);
                light = Math.Max(Math.Max(Slab(d, -c - w - width, -c - w), Slab(sPos, -c, g.L + c)),
                    Slab(z, g.LightFloor, above));
            }
            else
            {
                light = Math.Max(Vector2.Distance(q, g.LampCenter) - g.LampRadius, Slab(z, g.LightFloor, above));
            }
        }

        // Cable: vertical hole from the light down to a groove that runs out under the base.
        double r = Math.Max(1, s.CableDiameter / 2);
        double hole = Math.Max(Vector2.Distance(q, g.HolePos) - r, Slab(z, g.ZBot - 10, g.LightFloor + 1));
        var rel = q - g.HolePos;
        double along = Vector2.Dot(rel, g.GrooveDir);
        double lateral = Math.Abs(g.GrooveDir.X * rel.Y - g.GrooveDir.Y * rel.X);
        double groove = Math.Max(Math.Max(-r - along, lateral - r), Slab(z, g.ZBot - 10, g.ZBot + 2 * r));

        double cut = Math.Min(Math.Min(slot, light), Math.Min(hole, groove));
        return Math.Max(body, -cut);
    }

    private static (Vector2 Min, Vector2 Max) FootprintBounds(ProfileSurface profile, Geometry g, bool closed)
    {
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        const int samples = 1024;
        double ext = closed ? 0 : g.C + g.W;
        for (int k = 0; k <= samples; k++)
        {
            double sPos = -ext + (g.L + 2 * ext) * k / samples;
            var p = ProfileField.Extrapolate(profile, sPos);
            var n = profile.NormalAt(Math.Clamp(sPos, 0, g.L));
            foreach (double d in closed ? new[] { g.DFront } : new[] { g.DBack, g.DFront })
            {
                var v = p + n * (float)d;
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
        }
        return (min, max);
    }
}

/// <summary>
/// For a point in the XY plane: signed offset from the footprint curve (positive on the relief side)
/// and the arc-length position of the nearest point along it. Open curves are extended straight
/// past their ends so positions slightly beyond the ends are well defined.
/// </summary>
public sealed class ProfileField
{
    private readonly Vector2[] _pts;
    private readonly double[] _s;
    private readonly Vector2[] _segNormal;
    private readonly bool _closed;

    public ProfileField(ProfileSurface profile, double extension)
    {
        _closed = profile.ClosedU;
        double L = profile.Length;
        double start = _closed ? 0 : -extension;
        double span = _closed ? L : L + 2 * extension;
        int n = Math.Clamp((int)(span / 0.5), 64, 2000);

        _pts = new Vector2[n + 1];
        _s = new double[n + 1];
        for (int k = 0; k <= n; k++)
        {
            _s[k] = start + span * k / n;
            _pts[k] = _closed ? profile.PointAt(k == n ? 0 : _s[k]) : Extrapolate(profile, _s[k]);
        }

        _segNormal = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            var t = _pts[k + 1] - _pts[k];
            _segNormal[k] = t.LengthSquared() > 0 ? Vector2.Normalize(new Vector2(t.Y, -t.X)) : Vector2.Zero;
        }
    }

    public static Vector2 Extrapolate(ProfileSurface profile, double s)
    {
        double L = profile.Length;
        if (s >= 0 && s <= L) return profile.PointAt(s);
        double edge = s < 0 ? 0 : L;
        var n = profile.NormalAt(edge);
        var tangent = new Vector2(-n.Y, n.X);
        return profile.PointAt(edge) + tangent * (float)(s - edge);
    }

    public (double D, double S) Query(Vector2 q)
    {
        float best = float.MaxValue;
        int bestSeg = 0;
        float bestT = 0;
        bool inside = false;

        for (int k = 0; k < _segNormal.Length; k++)
        {
            var a = _pts[k];
            var b = _pts[k + 1];
            var ab = b - a;
            float len2 = ab.LengthSquared();
            float t = len2 > 0 ? Math.Clamp(Vector2.Dot(q - a, ab) / len2, 0, 1) : 0;
            float d2 = Vector2.DistanceSquared(q, a + ab * t);
            if (d2 < best) { best = d2; bestSeg = k; bestT = t; }

            if (_closed && (a.Y > q.Y) != (b.Y > q.Y) && q.X < (b.X - a.X) * (q.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }

        double dist = Math.Sqrt(best);
        double s = _s[bestSeg] + bestT * (_s[bestSeg + 1] - _s[bestSeg]);
        if (_closed)
            return (inside ? -dist : dist, s);

        Vector2 normal = _segNormal[bestSeg];
        if (bestT <= 1e-5f && bestSeg > 0) normal += _segNormal[bestSeg - 1];
        else if (bestT >= 1 - 1e-5f && bestSeg < _segNormal.Length - 1) normal += _segNormal[bestSeg + 1];
        var closest = _pts[bestSeg] + (_pts[bestSeg + 1] - _pts[bestSeg]) * bestT;
        return (Vector2.Dot(q - closest, normal) >= 0 ? dist : -dist, s);
    }
}
