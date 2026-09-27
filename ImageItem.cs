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

    /// <param name="fileData">The original image file, kept so it can be embedded in a project.</param>
    public ImageItem(string path, byte[] fileData, GrayImage image, int rotation = 0)
    {
        Path = path;
        FileData = fileData;
        Rotation = rotation;
        _image = image;
        _thumbnail = CreateThumbnail(image);
    }

    /// <summary>Decodes an image file (on the calling thread) and applies the given number of 90° turns.</summary>
    public static GrayImage Decode(byte[] fileData, string name, int rotation)
    {
        var img = GrayImage.Load(fileData, name);
        for (int i = 0; i < ((rotation % 4) + 4) % 4; i++)
            img = img.Rotate90();
        return img;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path { get; }
    public byte[] FileData { get; }
    /// <summary>Number of 90° rotations applied to the original image.</summary>
    public int Rotation { get; private set; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeText => $"{_image.Width} × {_image.Height} px";
    public Bitmap Thumbnail => _thumbnail;

    public GrayImage Image
    {
        get => _image;
        private set
        {
            _image = value;
            _thumbnail = CreateThumbnail(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeText)));
        }
    }

    public void Rotate90()
    {
        Rotation = (Rotation + 1) % 4;
        Image = _image.Rotate90();
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
