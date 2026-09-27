namespace LithophaneGUI.Core;

/// <summary>
/// Thickness grid in the "unrolled" space of the model. Node (i, j): i runs along the width
/// (left to right as seen from the relief side), j runs from the bottom to the top.
/// </summary>
public sealed class HeightMap
{
    public int Nu { get; }
    public int Nv { get; }
    public bool ClosedU { get; }
    public double WidthMm { get; }
    public double HeightMm { get; }
    public float[] Thickness { get; }

    private HeightMap(int nu, int nv, bool closedU, double widthMm, double heightMm, float[] thickness)
    {
        Nu = nu; Nv = nv; ClosedU = closedU; WidthMm = widthMm; HeightMm = heightMm; Thickness = thickness;
    }

    public float this[int i, int j] => Thickness[j * Nu + i];

    /// <summary>Unrolled x position (mm) of column i.</summary>
    public double X(int i) => ClosedU ? i * WidthMm / Nu : i * WidthMm / (Nu - 1);

    /// <summary>Height (mm) of row j.</summary>
    public double Z(int j) => j * HeightMm / (Nv - 1);

    /// <summary>Returns the image area size in mm (without border).</summary>
    public static (double W, double H) ImageSize(GrayImage img, LithophaneSettings s)
    {
        double aspect = (double)img.Width * s.RepeatX / ((double)img.Height * s.RepeatY);
        return aspect >= 1 ? (s.MaxSize, s.MaxSize / aspect) : (s.MaxSize * aspect, s.MaxSize);
    }

    /// <param name="maxNodes">Resolution is lowered, if needed, so the grid has at most this many nodes.</param>
    public static HeightMap Build(GrayImage img, LithophaneSettings s, int maxNodes = int.MaxValue)
    {
        bool closed = s.IsClosed;
        var (imgW, imgH) = ImageSize(img, s);
        double b = Math.Max(0, s.Border);
        double sideBorder = closed ? 0 : b;
        double totalW = imgW + 2 * sideBorder;
        double totalH = imgH + 2 * b;

        double res = Math.Max(0.05, s.Resolution);
        double estimate = totalW * res * totalH * res;
        if (estimate > maxNodes)
            res *= Math.Sqrt(maxNodes / estimate);

        int nu = closed ? Math.Max(8, (int)Math.Round(totalW * res)) : Math.Max(2, (int)Math.Round(totalW * res) + 1);
        int nv = Math.Max(2, (int)Math.Round(totalH * res) + 1);

        double tMax = Math.Max(0.1, s.Thickness);
        double tMin = Math.Clamp(s.MinThickness, 0.05, tMax);
        double gamma = Math.Clamp(s.Gamma, 0.1, 10);
        int repX = Math.Max(1, s.RepeatX), repY = Math.Max(1, s.RepeatY);

        var map = new HeightMap(nu, nv, closed, totalW, totalH, new float[nu * nv]);
        const double eps = 1e-9;

        Parallel.For(0, nv, j =>
        {
            double z = map.Z(j);
            double iy = (z - b) / imgH;           // 0 at image bottom, 1 at image top
            bool rowInBorder = iy < -eps || iy > 1 + eps;
            for (int i = 0; i < nu; i++)
            {
                double ix = (map.X(i) - sideBorder) / imgW;
                float t;
                if (rowInBorder || ix < -eps || ix > 1 + eps)
                {
                    t = (float)tMax;
                }
                else
                {
                    double u = Math.Clamp(ix, 0, 1);
                    double v = 1 - Math.Clamp(iy, 0, 1); // image rows go top to bottom
                    if (s.MirrorX) u = 1 - u;
                    if (s.FlipY) v = 1 - v;

                    double px = TileCoord(u, repX, img.Width);
                    double py = TileCoord(v, repY, img.Height);
                    double lum = img.Sample(px, py);
                    if (gamma != 1) lum = Math.Pow(lum, gamma);
                    double darkness = s.Negative ? lum : 1 - lum;
                    t = (float)(tMin + darkness * (tMax - tMin));
                }
                map.Thickness[j * nu + i] = t;
            }
        });

        return map;
    }

    /// <summary>Maps a [0,1] coordinate onto pixel space of an image tiled 'repeat' times.</summary>
    private static double TileCoord(double u, int repeat, int size)
    {
        double f = u * repeat;
        int tile = Math.Min((int)Math.Floor(f), repeat - 1);
        return (f - tile) * size;
    }
}
