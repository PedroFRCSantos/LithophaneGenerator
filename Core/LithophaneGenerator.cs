using System.Numerics;

namespace LithophaneGUI.Core;

/// <param name="Light">The light inside the support, for the preview only.</param>
public sealed record ModelParts(Mesh Lithophane, Mesh? Support, LightFixture? Light, IReadOnlyList<string> Notes);

public static class LithophaneGenerator
{
    /// <summary>Builds the lithophane mesh, resting on Z = 0 and centred on the origin in X/Y.</summary>
    public static Mesh Generate(GrayImage image, LithophaneSettings s, int maxNodes = int.MaxValue,
        CancellationToken ct = default)
    {
        var mesh = GenerateParts(image, s, maxNodes, 0, ct).Lithophane;
        PlaceOnBed(mesh);
        return mesh;
    }

    /// <summary>
    /// Builds the lithophane and, if enabled, its light support, both in the same coordinate frame
    /// (the lithophane standing in the support's slot).
    /// </summary>
    /// <param name="supportCells">Voxel budget for the support; 0 picks a default suited to export.</param>
    public static ModelParts GenerateParts(GrayImage image, LithophaneSettings s, int maxNodes = int.MaxValue,
        int supportCells = 0, CancellationToken ct = default)
    {
        var map = HeightMap.Build(image, s, maxNodes);
        ct.ThrowIfCancellationRequested();

        var surface = Surface.Create(s, map.WidthMm, map.HeightMm);
        var mesh = new Mesh();

        // Evaluate the surface once per column/row pair.
        var pos = new Vector3[map.Nu * map.Nv];
        var nrm = new Vector3[map.Nu * map.Nv];
        Parallel.For(0, map.Nv, new ParallelOptions { CancellationToken = ct }, j =>
        {
            double z = map.Z(j);
            for (int i = 0; i < map.Nu; i++)
            {
                var (p, n) = surface.Eval(map.X(i), z);
                pos[j * map.Nu + i] = p;
                nrm[j * map.Nu + i] = n;
            }
        });
        ct.ThrowIfCancellationRequested();

        AddSlab(mesh, map.Nu, map.Nv, map.ClosedU,
            (i, j) => (pos[j * map.Nu + i], nrm[j * map.Nu + i]),
            (i, j) => map[i, j]);

        if (s.SupportsStand && s.StandDepth > 0 && s.StandHeight > 0 && surface is ProfileSurface profile)
            AddStand(mesh, map, profile, s);

        ct.ThrowIfCancellationRequested();
        if (mesh.SignedVolume() < 0)
            mesh.FlipWinding();

        var notes = new List<string>();
        Mesh? support = null;
        LightFixture? light = null;
        if (s.HasLightSupport)
        {
            var footprint = Surface.Footprint(s, map.WidthMm, map.HeightMm);
            (support, light) = LightSupportGenerator.Generate(s, footprint,
                supportCells > 0 ? supportCells : 4_000_000, notes, ct);
            ct.ThrowIfCancellationRequested();

            // How much light reaches each point of the wall; front and back vertices share a node.
            var lit = Illumination.Compute(pos, nrm, light);
            for (int k = 0; k < lit.Length; k++)
            {
                mesh.Illumination[k] = lit[k];
                mesh.Illumination[lit.Length + k] = lit[k];
            }
        }

        return new ModelParts(mesh, support, light, notes);
    }

    /// <summary>Moves a mesh so it rests on Z = 0, centred on the origin in X/Y.</summary>
    public static void PlaceOnBed(Mesh mesh)
    {
        var (min, max) = mesh.Bounds();
        mesh.Translate(new Vector3(-(min.X + max.X) / 2, -(min.Y + max.Y) / 2, -min.Z));
    }

    /// <summary>Lithophane standing in its support, with the support drawn as the secondary part.</summary>
    /// <param name="showLight">Also include the light fixture (preview only).</param>
    public static Mesh Assemble(ModelParts parts, bool showLight = false)
    {
        var mesh = parts.Lithophane.Clone();
        if (parts.Support != null)
            mesh.AppendSecondary(parts.Support);
        if (showLight && parts.Light != null)
            mesh.Append(parts.Light.Mesh, MeshPart.Light);
        PlaceOnBed(mesh);
        return mesh;
    }

    /// <summary>Returns the meshes to write for the chosen layout, each with a file-name suffix.</summary>
    public static List<(string Suffix, Mesh Mesh)> Arrange(ModelParts parts, ExportLayout layout)
    {
        if (parts.Support == null)
        {
            var only = parts.Lithophane.Clone();
            PlaceOnBed(only);
            return new() { ("", only) };
        }

        switch (layout)
        {
            case ExportLayout.Assembled:
                return new() { ("", Assemble(parts)) };

            case ExportLayout.SideBySide:
            {
                var litho = parts.Lithophane.Clone();
                var support = parts.Support.Clone();
                PlaceOnBed(litho);
                PlaceOnBed(support);
                const float gap = 5;
                var (lMin, lMax) = litho.Bounds();
                var (sMin, _) = support.Bounds();
                support.Translate(new Vector3(0, lMax.Y + gap - sMin.Y, 0));
                litho.AppendSecondary(support);
                PlaceOnBed(litho);
                return new() { ("", litho) };
            }

            default:
            {
                var litho = parts.Lithophane.Clone();
                var support = parts.Support.Clone();
                PlaceOnBed(litho);
                PlaceOnBed(support);
                return new() { ("_lithophane", litho), ("_light_support", support) };
            }
        }
    }

    /// <summary>
    /// Stand along the bottom edge, centred on the middle of the wall, from Z = -height up to Z = 0.
    /// </summary>
    private static void AddStand(Mesh mesh, HeightMap map, ProfileSurface profile, LithophaneSettings s)
    {
        float depth = (float)s.StandDepth;
        float offset = (float)(s.Thickness / 2 - s.StandDepth / 2);
        float height = (float)s.StandHeight;
        int nu = map.Nu;
        var cols = new (Vector2 P, Vector2 N)[nu];
        for (int i = 0; i < nu; i++)
            cols[i] = (profile.PointAt(map.X(i)), profile.NormalAt(map.X(i)));

        AddSlab(mesh, nu, 2, map.ClosedU, (i, j) =>
        {
            var (p, n) = cols[i];
            var q = p + n * offset;
            return (new Vector3(q.X, q.Y, j == 0 ? -height : 0f), new Vector3(n.X, n.Y, 0));
        }, (_, _) => depth);
    }

    /// <summary>
    /// Adds a closed solid: a back surface given by <paramref name="surface"/>, a front surface
    /// offset along the normal by <paramref name="thickness"/>, and side walls around the boundary.
    /// </summary>
    public static void AddSlab(Mesh mesh, int nu, int nv, bool closedU,
        Func<int, int, (Vector3 Pos, Vector3 Normal)> surface, Func<int, int, float> thickness)
    {
        int baseIndex = mesh.Vertices.Count;
        int count = nu * nv;
        mesh.Vertices.Capacity = Math.Max(mesh.Vertices.Capacity, baseIndex + 2 * count);
        mesh.Thickness.Capacity = Math.Max(mesh.Thickness.Capacity, baseIndex + 2 * count);

        var back = new Vector3[count];
        var front = new Vector3[count];
        var thick = new float[count];
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int k = j * nu + i;
                var (p, n) = surface(i, j);
                float t = thickness(i, j);
                back[k] = p;
                front[k] = p + n * t;
                thick[k] = t;
            }
        mesh.Vertices.AddRange(front);
        mesh.Vertices.AddRange(back);
        mesh.Thickness.AddRange(thick);
        mesh.Thickness.AddRange(thick);
        for (int k = 0; k < 2 * count; k++) mesh.Illumination.Add(1f);

        int F(int i, int j) => baseIndex + j * nu + (i % nu);
        int B(int i, int j) => baseIndex + count + j * nu + (i % nu);

        var idx = mesh.Indices;
        int cellsU = closedU ? nu : nu - 1;
        idx.Capacity = Math.Max(idx.Capacity, idx.Count + cellsU * (nv - 1) * 12 + (cellsU + nv) * 12);

        // Front and back faces. Front quad winding: (00,10,11), (00,11,01); back is reversed.
        for (int j = 0; j < nv - 1; j++)
            for (int i = 0; i < cellsU; i++)
            {
                int f00 = F(i, j), f10 = F(i + 1, j), f11 = F(i + 1, j + 1), f01 = F(i, j + 1);
                idx.Add(f00); idx.Add(f10); idx.Add(f11);
                idx.Add(f00); idx.Add(f11); idx.Add(f01);

                int b00 = B(i, j), b10 = B(i + 1, j), b11 = B(i + 1, j + 1), b01 = B(i, j + 1);
                idx.Add(b00); idx.Add(b11); idx.Add(b10);
                idx.Add(b00); idx.Add(b01); idx.Add(b11);
            }

        // Side walls. For each front boundary edge A->B the wall is (Bf, Af, Ab), (Bf, Ab, Bb).
        void Wall(int ai, int aj, int bi, int bj)
        {
            int af = F(ai, aj), bf = F(bi, bj), ab = B(ai, aj), bb = B(bi, bj);
            idx.Add(bf); idx.Add(af); idx.Add(ab);
            idx.Add(bf); idx.Add(ab); idx.Add(bb);
        }

        for (int i = 0; i < cellsU; i++)
        {
            Wall(i, 0, i + 1, 0);                 // bottom edge
            Wall(i + 1, nv - 1, i, nv - 1);       // top edge
        }
        if (!closedU)
        {
            for (int j = 0; j < nv - 1; j++)
            {
                Wall(nu - 1, j, nu - 1, j + 1);   // right edge
                Wall(0, j + 1, 0, j);             // left edge
            }
        }
    }
}
