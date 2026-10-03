using System.Numerics;

namespace LithophaneGUI.Core;

/// <summary>
/// Small closed meshes for the preview-only light fixture. Every builder returns a mesh with
/// outward-facing triangles and the given glow factor stored per vertex in <see cref="Mesh.Illumination"/>.
/// </summary>
public static class Primitives
{
    /// <summary>
    /// A rectangle (offsets <paramref name="d0"/>..<paramref name="d1"/> along the profile normal,
    /// heights <paramref name="z0"/>..<paramref name="z1"/>) swept along the profile from s0 to s1.
    /// </summary>
    public static Mesh Sweep(ProfileSurface profile, double s0, double s1, bool closed,
        double d0, double d1, double z0, double z1, float glow)
    {
        var m = new Mesh();
        int steps = Math.Max(2, (int)Math.Ceiling((s1 - s0) / 1.0));
        int stations = closed ? steps : steps + 1;
        for (int k = 0; k < stations; k++)
        {
            double s = s0 + (s1 - s0) * k / steps;
            var p = profile.PointAt(s);
            var n = profile.NormalAt(s);
            foreach (var (d, z) in new[] { (d0, z0), (d1, z0), (d1, z1), (d0, z1) })
            {
                var q = p + n * (float)d;
                m.Vertices.Add(new Vector3(q.X, q.Y, (float)z));
            }
        }

        int V(int station, int corner) => (station % stations) * 4 + corner;
        for (int k = 0; k < steps; k++)
            for (int c = 0; c < 4; c++)
                Quad(m, V(k, c), V(k + 1, c), V(k + 1, (c + 1) % 4), V(k, (c + 1) % 4));
        if (!closed)
        {
            Quad(m, V(0, 3), V(0, 2), V(0, 1), V(0, 0));
            Quad(m, V(steps, 0), V(steps, 1), V(steps, 2), V(steps, 3));
        }
        return Finish(m, glow);
    }

    /// <summary>A vertical cylinder; the top cap can glow more than the sides (an LED puck, a flame).</summary>
    public static Mesh Cylinder(Vector2 center, float radius, float z0, float z1, float sideGlow, float topGlow,
        int segments = 48)
    {
        var m = new Mesh();
        var glow = m.Illumination;
        // Sides and caps use separate vertices so each can have its own glow.
        for (int k = 0; k < segments; k++)
        {
            float a = 2 * MathF.PI * k / segments;
            var p = center + radius * new Vector2(MathF.Cos(a), MathF.Sin(a));
            m.Vertices.Add(new Vector3(p, z0)); glow.Add(sideGlow);
            m.Vertices.Add(new Vector3(p, z1)); glow.Add(sideGlow);
        }
        for (int k = 0; k < segments; k++)
        {
            int n = (k + 1) % segments;
            Quad(m, 2 * k, 2 * n, 2 * n + 1, 2 * k + 1);
        }
        AddCap(m, center, radius, z0, segments, false, sideGlow);
        AddCap(m, center, radius, z1, segments, true, topGlow);
        return Finish(m, null);
    }

    public static Mesh Sphere(Vector3 center, float radius, float glow, int rings = 16, int segments = 32)
    {
        var m = new Mesh();
        for (int j = 0; j <= rings; j++)
        {
            float lat = MathF.PI * j / rings;
            float z = MathF.Cos(lat), r = MathF.Sin(lat);
            for (int i = 0; i < segments; i++)
            {
                float a = 2 * MathF.PI * i / segments;
                m.Vertices.Add(center + radius * new Vector3(r * MathF.Cos(a), r * MathF.Sin(a), z));
            }
        }
        int V(int j, int i) => j * segments + (i % segments);
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < segments; i++)
                Quad(m, V(j, i), V(j, i + 1), V(j + 1, i + 1), V(j + 1, i));
        return Finish(m, glow);
    }

    private static void AddCap(Mesh m, Vector2 center, float radius, float z, int segments, bool up, float glow)
    {
        int c = m.Vertices.Count;
        m.Vertices.Add(new Vector3(center, z)); m.Illumination.Add(glow);
        for (int k = 0; k < segments; k++)
        {
            float a = 2 * MathF.PI * k / segments;
            m.Vertices.Add(new Vector3(center + radius * new Vector2(MathF.Cos(a), MathF.Sin(a)), z));
            m.Illumination.Add(glow);
        }
        for (int k = 0; k < segments; k++)
        {
            int a = c + 1 + k, b = c + 1 + (k + 1) % segments;
            if (up) { m.Indices.Add(c); m.Indices.Add(a); m.Indices.Add(b); }
            else { m.Indices.Add(c); m.Indices.Add(b); m.Indices.Add(a); }
        }
    }

    private static void Quad(Mesh m, int a, int b, int c, int d)
    {
        m.Indices.Add(a); m.Indices.Add(b); m.Indices.Add(c);
        m.Indices.Add(a); m.Indices.Add(c); m.Indices.Add(d);
    }

    /// <summary>Fills the per-vertex lists and makes the triangles face outwards.</summary>
    private static Mesh Finish(Mesh m, float? glow)
    {
        while (m.Thickness.Count < m.Vertices.Count) m.Thickness.Add(0);
        if (glow is { } g)
            while (m.Illumination.Count < m.Vertices.Count) m.Illumination.Add(g);
        if (m.SignedVolume() < 0) m.FlipWinding();
        return m;
    }
}
