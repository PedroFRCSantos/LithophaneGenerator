namespace LithophaneGUI.Core;

public enum ShapeType
{
    Flat,
    OuterCurve,
    InnerCurve,
    Cylinder,
    Heart,
    Dome,
}

public sealed record LithophaneSettings
{
    // Image settings
    public bool Negative { get; init; }
    public bool MirrorX { get; init; }
    public bool FlipY { get; init; }
    public int RepeatX { get; init; } = 1;
    public int RepeatY { get; init; } = 1;
    public double Gamma { get; init; } = 1.0;

    // Model settings
    public ShapeType Shape { get; init; } = ShapeType.Flat;
    /// <summary>Longest side of the image area in mm (unrolled length for curved shapes).</summary>
    public double MaxSize { get; init; } = 100;
    /// <summary>Thickness of the darkest areas and of the border, in mm.</summary>
    public double Thickness { get; init; } = 3;
    /// <summary>Thickness of the brightest areas, in mm.</summary>
    public double MinThickness { get; init; } = 0.8;
    public double Border { get; init; } = 3;
    /// <summary>Mesh points per mm.</summary>
    public double Resolution { get; init; } = 5;
    /// <summary>Arc angle in degrees for the curved shapes.</summary>
    public double CurveAngle { get; init; } = 120;
    /// <summary>Depth of the stand under the model in mm (0 = no stand).</summary>
    public double StandDepth { get; init; }
    public double StandHeight { get; init; } = 3;

    public bool IsClosed => Shape is ShapeType.Cylinder or ShapeType.Heart or ShapeType.Dome;
    public bool SupportsStand => Shape != ShapeType.Dome;
}
