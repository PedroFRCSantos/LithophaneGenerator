using System.Numerics;
using LithophaneGUI.Core;

namespace LithophaneGUI.Rendering;

public sealed class OrbitCamera
{
    public Vector3 Target { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; } = 0.25f;
    public float Distance { get; set; } = 200;
    public float FieldOfView { get; set; } = MathF.PI / 5;

    public Vector3 Position => Target + Distance * new Vector3(
        MathF.Sin(Yaw) * MathF.Cos(Pitch),
        -MathF.Cos(Yaw) * MathF.Cos(Pitch),
        MathF.Sin(Pitch));

    /// <summary>Frames the mesh from the front (looking along +Y, Z up).</summary>
    public void Frame(Mesh mesh)
    {
        var (min, max) = mesh.Bounds();
        Target = (min + max) / 2;
        float radius = MathF.Max(1f, (max - min).Length() / 2);
        Distance = radius / MathF.Tan(FieldOfView / 2) * 1.1f;
        Yaw = 0;
        Pitch = 0.25f;
    }

    public void Pan(float dxPixels, float dyPixels, int viewHeight)
    {
        float worldPerPixel = 2 * Distance * MathF.Tan(FieldOfView / 2) / Math.Max(1, viewHeight);
        var forward = Vector3.Normalize(Target - Position);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        var up = Vector3.Cross(right, forward);
        Target += (-dxPixels * right + dyPixels * up) * worldPerPixel;
    }
}

/// <summary>
/// Small multi-threaded z-buffer rasteriser. Draws a mesh either with simple lighting or as a
/// simulated backlit lithophane (brightness from the wall thickness).
/// </summary>
public sealed class SoftwareRenderer
{
    private float[] _depth = Array.Empty<float>();
    private Vector3[] _screen = Array.Empty<Vector3>();
    private float[] _vertexShade = Array.Empty<float>();
    private float[] _triShade = Array.Empty<float>();

    public void Render(Mesh? mesh, OrbitCamera cam, int width, int height, bool backlit, int[] pixels)
    {
        if (_depth.Length < width * height) _depth = new float[width * height];
        FillBackground(pixels, width, height, backlit);
        Array.Fill(_depth, float.MaxValue, 0, width * height);
        if (mesh == null || mesh.Vertices.Count == 0) return;

        var verts = mesh.Vertices;
        var idx = mesh.Indices;
        int nv = verts.Count, nt = idx.Count / 3;
        if (_screen.Length < nv) { _screen = new Vector3[nv]; _vertexShade = new float[nv]; }
        if (_triShade.Length < nt) _triShade = new float[nt];

        var camPos = cam.Position;
        float near = Math.Max(0.01f, cam.Distance * 0.001f);
        var view = Matrix4x4.CreateLookAt(camPos, cam.Target, Vector3.UnitZ);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(cam.FieldOfView, (float)width / height, near, cam.Distance * 10 + 1000);
        var viewProj = view * proj;

        int litVertices = Math.Min(nv, mesh.SecondaryVertexStart);
        int secondaryTri = mesh.SecondaryTriangleStart;
        float tMin = float.MaxValue, tMax = float.MinValue;
        for (int i = 0; i < litVertices; i++) { tMin = Math.Min(tMin, mesh.Thickness[i]); tMax = Math.Max(tMax, mesh.Thickness[i]); }
        // Transmission falls roughly exponentially with thickness.
        float absorb = tMax > tMin ? MathF.Log(25f) / (tMax - tMin) : 0;

        var screen = _screen;
        var vshade = _vertexShade;
        Parallel.For(0, nv, i =>
        {
            var c = Vector4.Transform(new Vector4(verts[i], 1), viewProj);
            screen[i] = c.W <= near
                ? new Vector3(float.NaN)
                : new Vector3((c.X / c.W + 1) * 0.5f * width, (1 - c.Y / c.W) * 0.5f * height, c.Z / c.W);
            if (backlit && i < litVertices)
                vshade[i] = MathF.Exp(-absorb * (mesh.Thickness[i] - tMin));
        });

        // Headlight slightly above and to the left of the camera.
        var forward = Vector3.Normalize(cam.Target - camPos);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        var up = Vector3.Cross(right, forward);
        var light = Vector3.Normalize(-forward + 0.4f * up - 0.3f * right);

        var tshade = _triShade;
        Parallel.For(0, nt, t =>
        {
            var a = verts[idx[3 * t]];
            var b = verts[idx[3 * t + 1]];
            var c = verts[idx[3 * t + 2]];
            var n = Vector3.Cross(b - a, c - a);
            float len = n.Length();
            float d = len > 0 ? Vector3.Dot(n / len, light) : 0;
            tshade[t] = 0.18f + 0.82f * Math.Max(0, d);
        });

        int bands = Math.Clamp(Environment.ProcessorCount * 2, 1, height);
        int bandHeight = (height + bands - 1) / bands;
        var depth = _depth;

        Parallel.For(0, bands, band =>
        {
            int y0 = band * bandHeight;
            int y1 = Math.Min(height, y0 + bandHeight);
            for (int t = 0; t < nt; t++)
            {
                int i0 = idx[3 * t], i1 = idx[3 * t + 1], i2 = idx[3 * t + 2];
                var p0 = screen[i0]; var p1 = screen[i1]; var p2 = screen[i2];
                if (float.IsNaN(p0.X) || float.IsNaN(p1.X) || float.IsNaN(p2.X)) continue;

                float minY = MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y));
                float maxY = MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y));
                int yStart = Math.Max(y0, (int)MathF.Ceiling(minY - 0.5f));
                int yEnd = Math.Min(y1 - 1, (int)MathF.Floor(maxY - 0.5f));
                if (yStart > yEnd) continue;

                // Screen Y points down, so outward-facing triangles have negative area.
                float area = (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
                if (area >= 0) continue;

                float minX = MathF.Min(p0.X, MathF.Min(p1.X, p2.X));
                float maxX = MathF.Max(p0.X, MathF.Max(p1.X, p2.X));
                int xStart = Math.Max(0, (int)MathF.Ceiling(minX - 0.5f));
                int xEnd = Math.Min(width - 1, (int)MathF.Floor(maxX - 0.5f));
                if (xStart > xEnd) continue;

                float inv = 1f / area;
                float s0 = 0, s1 = 0, s2 = 0, flat = tshade[t];
                bool secondary = t >= secondaryTri;
                bool lit = backlit && !secondary;
                if (lit) { s0 = vshade[i0]; s1 = vshade[i1]; s2 = vshade[i2]; }

                for (int y = yStart; y <= yEnd; y++)
                {
                    float py = y + 0.5f;
                    int row = y * width;
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        float px = x + 0.5f;
                        float w0 = ((p2.X - p1.X) * (py - p1.Y) - (p2.Y - p1.Y) * (px - p1.X)) * inv;
                        if (w0 < 0) continue;
                        float w1 = ((p0.X - p2.X) * (py - p2.Y) - (p0.Y - p2.Y) * (px - p2.X)) * inv;
                        if (w1 < 0) continue;
                        float w2 = 1 - w0 - w1;
                        if (w2 < 0) continue;

                        float z = w0 * p0.Z + w1 * p1.Z + w2 * p2.Z;
                        int k = row + x;
                        if (z >= depth[k]) continue;
                        depth[k] = z;

                        pixels[k] = secondary ? Support(flat, backlit)
                            : lit ? Backlit(w0 * s0 + w1 * s1 + w2 * s2, flat)
                            : Shaded(flat);
                    }
                }
            }
        });
    }

    private static int Shaded(float i)
    {
        int r = (int)(235 * i), g = (int)(228 * i), b = (int)(215 * i);
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }

    private static int Support(float i, bool dim)
    {
        if (dim) i *= 0.35f;
        int r = (int)(120 * i), g = (int)(160 * i), b = (int)(215 * i);
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }

    private static int Backlit(float transmission, float light)
    {
        float v = Math.Clamp(transmission * (0.85f + 0.15f * light), 0, 1);
        int r = (int)(255 * MathF.Pow(v, 0.8f));
        int g = (int)(235 * v);
        int b = (int)(190 * v * v);
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }

    private static void FillBackground(int[] pixels, int width, int height, bool backlit)
    {
        for (int y = 0; y < height; y++)
        {
            float f = (float)y / Math.Max(1, height - 1);
            int c = backlit
                ? Lerp(0x151518, 0x050506, f)
                : Lerp(0x3A4150, 0x1C1F26, f);
            Array.Fill(pixels, unchecked((int)0xFF000000) | c, y * width, width);
        }
    }

    private static int Lerp(int a, int b, float f)
    {
        int r = (int)(((a >> 16) & 255) + ((((b >> 16) & 255) - ((a >> 16) & 255)) * f));
        int g = (int)(((a >> 8) & 255) + ((((b >> 8) & 255) - ((a >> 8) & 255)) * f));
        int bl = (int)((a & 255) + (((b & 255) - (a & 255)) * f));
        return (r << 16) | (g << 8) | bl;
    }
}
