using System.Numerics;

namespace LithophaneGUI.Core;

/// <summary>
/// Turns a sampled signed distance field (negative inside) into a closed triangle mesh using
/// naive surface nets: one vertex per cell that the surface crosses, one quad per crossed grid edge.
/// The outermost layer of samples must be outside (positive) so the result is watertight.
/// </summary>
public static class SurfaceNets
{
    private static readonly (int A, int B)[] CubeEdges = BuildEdges();

    private static (int, int)[] BuildEdges()
    {
        var edges = new List<(int, int)>();
        for (int c = 0; c < 8; c++)
            foreach (int bit in new[] { 1, 2, 4 })
                if ((c & bit) == 0) edges.Add((c, c | bit));
        return edges.ToArray();
    }

    public static Mesh Extract(float[] f, int nx, int ny, int nz, Vector3 origin, float h)
    {
        int cx = nx - 1, cy = ny - 1, cz = nz - 1;
        int P(int i, int j, int k) => (k * ny + j) * nx + i;
        int C(int i, int j, int k) => (k * cy + j) * cx + i;

        var mesh = new Mesh();
        var cellVertex = new int[cx * cy * cz];
        Span<float> corner = stackalloc float[8];

        for (int k = 0; k < cz; k++)
            for (int j = 0; j < cy; j++)
                for (int i = 0; i < cx; i++)
                {
                    int mask = 0;
                    for (int c = 0; c < 8; c++)
                    {
                        corner[c] = f[P(i + (c & 1), j + ((c >> 1) & 1), k + ((c >> 2) & 1))];
                        if (corner[c] < 0) mask |= 1 << c;
                    }
                    if (mask == 0 || mask == 255)
                    {
                        cellVertex[C(i, j, k)] = -1;
                        continue;
                    }

                    var sum = Vector3.Zero;
                    int count = 0;
                    foreach (var (a, b) in CubeEdges)
                    {
                        float fa = corner[a], fb = corner[b];
                        if ((fa < 0) == (fb < 0)) continue;
                        float t = fa / (fa - fb);
                        var pa = new Vector3(a & 1, (a >> 1) & 1, (a >> 2) & 1);
                        var pb = new Vector3(b & 1, (b >> 1) & 1, (b >> 2) & 1);
                        sum += Vector3.Lerp(pa, pb, t);
                        count++;
                    }
                    cellVertex[C(i, j, k)] = mesh.Vertices.Count;
                    mesh.Vertices.Add(origin + (new Vector3(i, j, k) + sum / count) * h);
                    mesh.Thickness.Add(0);
                }

        var idx = mesh.Indices;
        void Quad(bool insideLow, int c0, int c1, int c2, int c3)
        {
            int v0 = cellVertex[c0], v1 = cellVertex[c1], v2 = cellVertex[c2], v3 = cellVertex[c3];
            if (!insideLow) (v1, v3) = (v3, v1);
            idx.Add(v0); idx.Add(v1); idx.Add(v2);
            idx.Add(v0); idx.Add(v2); idx.Add(v3);
        }

        for (int k = 1; k < nz - 1; k++)
            for (int j = 1; j < ny - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    bool a = f[P(i, j, k)] < 0, b = f[P(i + 1, j, k)] < 0;
                    if (a != b)
                        Quad(a, C(i, j - 1, k - 1), C(i, j, k - 1), C(i, j, k), C(i, j - 1, k));
                }

        for (int k = 1; k < nz - 1; k++)
            for (int j = 0; j < ny - 1; j++)
                for (int i = 1; i < nx - 1; i++)
                {
                    bool a = f[P(i, j, k)] < 0, b = f[P(i, j + 1, k)] < 0;
                    if (a != b)
                        Quad(a, C(i - 1, j, k - 1), C(i - 1, j, k), C(i, j, k), C(i, j, k - 1));
                }

        for (int k = 0; k < nz - 1; k++)
            for (int j = 1; j < ny - 1; j++)
                for (int i = 1; i < nx - 1; i++)
                {
                    bool a = f[P(i, j, k)] < 0, b = f[P(i, j, k + 1)] < 0;
                    if (a != b)
                        Quad(a, C(i - 1, j - 1, k), C(i, j - 1, k), C(i, j, k), C(i - 1, j, k));
                }

        return mesh;
    }
}
