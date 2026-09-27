using SkiaSharp;

namespace LithophaneGUI.Core;

/// <summary>
/// A grayscale (luminance) image, values in [0,1] where 1 is white.
/// Transparent pixels are blended onto white.
/// </summary>
public sealed class GrayImage
{
    public int Width { get; }
    public int Height { get; }
    public float[] Pixels { get; }

    public GrayImage(int width, int height, float[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public float this[int x, int y] => Pixels[y * Width + x];

    public static GrayImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException($"Unsupported or corrupt image: {Path.GetFileName(path)}");

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            throw new InvalidDataException($"Could not decode image: {result}");

        int w = info.Width, h = info.Height;
        var data = new float[w * h];
        var span = bitmap.GetPixelSpan();
        for (int i = 0, p = 0; i < data.Length; i++, p += 4)
        {
            float r = span[p] / 255f, g = span[p + 1] / 255f, b = span[p + 2] / 255f, a = span[p + 3] / 255f;
            float lum = 0.299f * r + 0.587f * g + 0.114f * b;
            data[i] = lum * a + (1f - a);
        }

        var img = new GrayImage(w, h, data);
        return img.ApplyOrigin(codec.EncodedOrigin);
    }

    /// <summary>Applies EXIF orientation so photos from phones come out upright.</summary>
    private GrayImage ApplyOrigin(SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => Transform(Width, Height, (x, y) => (Width - 1 - x, y)),
        SKEncodedOrigin.BottomRight => Transform(Width, Height, (x, y) => (Width - 1 - x, Height - 1 - y)),
        SKEncodedOrigin.BottomLeft => Transform(Width, Height, (x, y) => (x, Height - 1 - y)),
        SKEncodedOrigin.LeftTop => Transform(Height, Width, (x, y) => (y, x)),
        SKEncodedOrigin.RightTop => Transform(Height, Width, (x, y) => (y, Height - 1 - x)),
        SKEncodedOrigin.RightBottom => Transform(Height, Width, (x, y) => (Width - 1 - y, Height - 1 - x)),
        SKEncodedOrigin.LeftBottom => Transform(Height, Width, (x, y) => (Width - 1 - y, x)),
        _ => this,
    };

    /// <summary>Builds a new image where each destination pixel (x,y) reads source pixel map(x,y).</summary>
    private GrayImage Transform(int newW, int newH, Func<int, int, (int X, int Y)> map)
    {
        var data = new float[newW * newH];
        for (int y = 0; y < newH; y++)
            for (int x = 0; x < newW; x++)
            {
                var (sx, sy) = map(x, y);
                data[y * newW + x] = this[sx, sy];
            }
        return new GrayImage(newW, newH, data);
    }

    public GrayImage Rotate90() => Transform(Height, Width, (x, y) => (y, Height - 1 - x));

    /// <summary>Bilinear sample at continuous pixel coordinates (pixel centers at +0.5).</summary>
    public float Sample(double px, double py)
    {
        double fx = Math.Clamp(px - 0.5, 0, Width - 1);
        double fy = Math.Clamp(py - 0.5, 0, Height - 1);
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = Math.Min(x0 + 1, Width - 1), y1 = Math.Min(y0 + 1, Height - 1);
        float tx = (float)(fx - x0), ty = (float)(fy - y0);
        float a = this[x0, y0] + (this[x1, y0] - this[x0, y0]) * tx;
        float b = this[x0, y1] + (this[x1, y1] - this[x0, y1]) * tx;
        return a + (b - a) * ty;
    }
}
