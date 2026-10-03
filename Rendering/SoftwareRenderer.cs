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

/// <summary>What the backlit preview simulates: the light and the filament, as sRGB colours.</summary>
/// <param name="Power">Relative brightness of the light; 1 = normal.</param>
public sealed record LightSimulation(Vector3 LightColor, float Power, Vector3 FilamentColor)
{
    public static readonly LightSimulation Default = new(FromHex("#FFA957"), 1f, FromHex("#FFFFFF"));

    public static Vector3 FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 8) hex = hex[2..];
        return new Vector3(
            Convert.ToInt32(hex[..2], 16) / 255f,
            Convert.ToInt32(hex[2..4], 16) / 255f,
            Convert.ToInt32(hex[4..6], 16) / 255f);
    }

    public static Vector3 ToLinear(Vector3 srgb) => new(ToLinear(srgb.X), ToLinear(srgb.Y), ToLinear(srgb.Z));

    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
}

/// <summary>
/// Small multi-threaded z-buffer rasteriser. Draws a mesh either with simple lighting or as a
/// simulated backlit lithophane: the light's colour and power, attenuated by how much of it reaches
/// each point of the wall and by the wall's thickness and filament colour (Beer–Lambert, 100% infill).
/// </summary>
public sealed class SoftwareRenderer
{
    /// <summary>Attenuation of white PLA at 100% infill, per mm (about 22% left after 1 mm, 1% after 3 mm).</summary>
    private const float WhiteAbsorption = 1.5f;
    /// <summary>Thickness at which a coloured filament shows its nominal colour.</summary>
    private const float ReferenceThickness = 1.5f;
    /// <summary>Room light reflected off the front of the print, so unlit areas are not pure black.</summary>
    private const float Ambient = 0.004f;

    private const int LutSize = 4096;
    private const float LutRange = 16f;
    /// <summary>Tone mapping + sRGB encoding of a linear value in [0, LutRange], indexed by its square root so dark tones keep enough steps.</summary>
    private static readonly byte[] ToneLut = BuildLut();

    private float[] _depth = Array.Empty<float>();
    private Vector3[] _screen = Array.Empty<Vector3>();
    private Vector3[] _vertexColor = Array.Empty<Vector3>();
    private float[] _triShade = Array.Empty<float>();
    private byte[] _triPart = Array.Empty<byte>();

    public void Render(Mesh? mesh, OrbitCamera cam, int width, int height, bool backlit, LightSimulation sim, int[] pixels)
    {
        if (_depth.Length < width * height) _depth = new float[width * height];
        FillBackground(pixels, width, height, backlit);
        Array.Fill(_depth, float.MaxValue, 0, width * height);
        if (mesh == null || mesh.Vertices.Count == 0) return;

        var verts = mesh.Vertices;
        var idx = mesh.Indices;
        int nv = verts.Count, nt = idx.Count / 3;
        if (_screen.Length < nv) { _screen = new Vector3[nv]; _vertexColor = new Vector3[nv]; }
        if (_triShade.Length < nt) { _triShade = new float[nt]; _triPart = new byte[nt]; }

        var camPos = cam.Position;
        float near = Math.Max(0.01f, cam.Distance * 0.001f);
        var view = Matrix4x4.CreateLookAt(camPos, cam.Target, Vector3.UnitZ);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(cam.FieldOfView, (float)width / height, near, cam.Distance * 10 + 1000);
        var viewProj = view * proj;

        // Which part each vertex / triangle belongs to.
        var triPart = _triPart;
        Array.Fill(triPart, (byte)MeshPart.Lithophane, 0, nt);
        var vertexPart = new byte[nv];
        foreach (var (kind, vStart, tStart) in mesh.Parts)
        {
            Array.Fill(vertexPart, (byte)kind, vStart, nv - vStart);
            Array.Fill(triPart, (byte)kind, tStart, nt - tStart);
        }

        // Light and filament in linear RGB; the filament colour sets a per-channel absorption.
        var light = LightSimulation.ToLinear(sim.LightColor) * sim.Power;
        var filament = LightSimulation.ToLinear(sim.FilamentColor);
        var absorb = new Vector3(
            WhiteAbsorption - MathF.Log(Math.Max(0.02f, filament.X)) / ReferenceThickness,
            WhiteAbsorption - MathF.Log(Math.Max(0.02f, filament.Y)) / ReferenceThickness,
            WhiteAbsorption - MathF.Log(Math.Max(0.02f, filament.Z)) / ReferenceThickness);
        var ambient = filament * Ambient;

        var screen = _screen;
        var vcolor = _vertexColor;
        Parallel.For(0, nv, i =>
        {
            var c = Vector4.Transform(new Vector4(verts[i], 1), viewProj);
            screen[i] = c.W <= near
                ? new Vector3(float.NaN)
                : new Vector3((c.X / c.W + 1) * 0.5f * width, (1 - c.Y / c.W) * 0.5f * height, c.Z / c.W);
            if (!backlit) return;
            float lit = i < mesh.Illumination.Count ? mesh.Illumination[i] : 1f;
            switch ((MeshPart)vertexPart[i])
            {
                case MeshPart.Lithophane:
                    float t = mesh.Thickness[i];
                    vcolor[i] = light * lit * new Vector3(MathF.Exp(-absorb.X * t), MathF.Exp(-absorb.Y * t), MathF.Exp(-absorb.Z * t));
                    break;
                case MeshPart.Light:
                    vcolor[i] = light * (2f * lit);
                    break;
            }
        });

        // Headlight slightly above and to the left of the camera.
        var forward = Vector3.Normalize(cam.Target - camPos);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        var up = Vector3.Cross(right, forward);
        var headlight = Vector3.Normalize(-forward + 0.4f * up - 0.3f * right);

        var tshade = _triShade;
        Parallel.For(0, nt, t =>
        {
            var a = verts[idx[3 * t]];
            var b = verts[idx[3 * t + 1]];
            var c = verts[idx[3 * t + 2]];
            var n = Vector3.Cross(b - a, c - a);
            float len = n.Length();
            float d = len > 0 ? Vector3.Dot(n / len, headlight) : 0;
            tshade[t] = 0.18f + 0.82f * Math.Max(0, d);
        });

        var lightSrgb = sim.LightColor;
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
                float flat = tshade[t];
                var part = (MeshPart)triPart[t];
                bool glowing = backlit && part != MeshPart.Support;
                Vector3 c0 = default, c1 = default, c2 = default;
                if (glowing) { c0 = vcolor[i0]; c1 = vcolor[i1]; c2 = vcolor[i2]; }
                int flatColor = part switch
                {
                    MeshPart.Support => Support(flat, backlit),
                    MeshPart.Light => Fixture(flat, lightSrgb, (mesh.Illumination[i0] + mesh.Illumination[i1] + mesh.Illumination[i2]) / 3),
                    _ => Shaded(flat),
                };
                var fill = part == MeshPart.Lithophane ? ambient * flat : Vector3.Zero;

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

                        pixels[k] = glowing ? Tone(w0 * c0 + w1 * c1 + w2 * c2 + fill) : flatColor;
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

    /// <summary>The light fixture when it is switched off: grey, tinted with the light's colour where it glows.</summary>
    private static int Fixture(float i, Vector3 lightSrgb, float glow)
    {
        var tint = Vector3.Lerp(new Vector3(0.75f), lightSrgb, glow);
        float v = i * (0.6f + 0.4f * glow) * 255;
        int r = (int)(v * tint.X), g = (int)(v * tint.Y), b = (int)(v * tint.Z);
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }

    private static int Tone(Vector3 linear)
    {
        int r = ToneLut[LutIndex(linear.X)], g = ToneLut[LutIndex(linear.Y)], b = ToneLut[LutIndex(linear.Z)];
        return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
    }

    private static int LutIndex(float x) =>
        x <= 0 ? 0 : Math.Min(LutSize - 1, (int)(MathF.Sqrt(x / LutRange) * (LutSize - 1) + 0.5f));

    private static byte[] BuildLut()
    {
        var lut = new byte[LutSize];
        for (int i = 0; i < LutSize; i++)
        {
            float f = (float)i / (LutSize - 1);
            float x = LutRange * f * f;
            float v = 1 - MathF.Exp(-3.5f * x);            // soft roll-off instead of clipping
            lut[i] = (byte)Math.Round(255 * MathF.Pow(v, 1 / 2.2f));
        }
        return lut;
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
