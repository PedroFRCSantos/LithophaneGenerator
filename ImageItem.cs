using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LithophaneGUI.Core;

namespace LithophaneGUI;

/// <summary>An image loaded into the list, with its grayscale data and a thumbnail.</summary>
public sealed class ImageItem : INotifyPropertyChanged
{
    private GrayImage _image;
    private Bitmap _thumbnail;

    public ImageItem(string path, GrayImage image)
    {
        Path = path;
        _image = image;
        _thumbnail = CreateThumbnail(image);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeText => $"{_image.Width} × {_image.Height} px";
    public Bitmap Thumbnail => _thumbnail;

    public GrayImage Image
    {
        get => _image;
        set
        {
            _image = value;
            _thumbnail = CreateThumbnail(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeText)));
        }
    }

    private static Bitmap CreateThumbnail(GrayImage img, int maxSide = 96)
    {
        double scale = Math.Min(1.0, (double)maxSide / Math.Max(img.Width, img.Height));
        int w = Math.Max(1, (int)(img.Width * scale));
        int h = Math.Max(1, (int)(img.Height * scale));
        var pixels = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = img.Sample((x + 0.5) / scale, (y + 0.5) / scale);
                int g = (int)(Math.Clamp(v, 0, 1) * 255);
                pixels[y * w + x] = unchecked((int)0xFF000000) | (g << 16) | (g << 8) | g;
            }

        var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bmp.Lock())
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(pixels, y * w, fb.Address + y * fb.RowBytes, w);
        }
        return bmp;
    }
}
