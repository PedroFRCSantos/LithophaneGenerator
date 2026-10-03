using System.Text.Json;
using System.Text.Json.Serialization;

namespace LithophaneGUI.Core;

/// <summary>
/// A saved project (<c>.lipo</c> file): all settings plus the images. The original image files are
/// embedded so a project still opens after the source images are moved or deleted.
/// </summary>
public sealed class LithophaneProject
{
    public const string Extension = ".lipo";
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public int Version { get; set; } = CurrentVersion;
    public LithophaneSettings Settings { get; set; } = new();
    public ExportLayout Layout { get; set; }
    /// <summary>Index into the lamp preset list in the UI (the last entry is "Custom").</summary>
    public int LampPreset { get; set; }
    public bool Backlit { get; set; }
    /// <summary>Draw the light source inside the support in the preview.</summary>
    public bool ShowLight { get; set; } = true;
    // Backlit simulation: preset indices (the last entry is "Custom") and the colours as "#AARRGGBB".
    public int LightPreset { get; set; }
    public string LightColor { get; set; } = "#FFFFA957";
    public double LightPower { get; set; } = 1;
    public int FilamentPreset { get; set; }
    public string FilamentColor { get; set; } = "#FFFFFFFF";
    public int SelectedImage { get; set; } = -1;
    public List<ProjectImage> Images { get; set; } = new();

    public void Save(string path)
    {
        // Write to a temporary file first so a failed save never destroys the previous project.
        string temp = path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, this, JsonOptions);
        File.Move(temp, path, overwrite: true);
    }

    public static LithophaneProject Load(string path)
    {
        using var stream = File.OpenRead(path);
        var project = JsonSerializer.Deserialize<LithophaneProject>(stream, JsonOptions)
            ?? throw new InvalidDataException("Empty project file.");
        if (project.Version > CurrentVersion)
            throw new InvalidDataException("This project was saved by a newer version of the app.");
        return project;
    }
}

public sealed class ProjectImage
{
    /// <summary>Path the image was originally loaded from (for display only).</summary>
    public string SourcePath { get; set; } = "";
    /// <summary>The original image file's bytes (base64 in JSON).</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    /// <summary>Number of clockwise 90° rotations applied after loading.</summary>
    public int Rotation { get; set; }
}
