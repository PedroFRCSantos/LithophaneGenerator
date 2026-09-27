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

public enum LightSupportType
{
    None,
    LedStrip,
    Lamp,
}

public enum ExportLayout
{
    /// <summary>Lithophane and light support in separate STL files.</summary>
    SeparateFiles,
    /// <summary>One STL with both parts next to each other on the print bed.</summary>
    SideBySide,
    /// <summary>One STL with the lithophane standing in the support (printed as one piece).</summary>
    Assembled,
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

    // Light support (base that holds the lithophane and the light source)
    public LightSupportType LightSupport { get; init; } = LightSupportType.None;
    public double SupportHeight { get; init; } = 20;
    public double SlotDepth { get; init; } = 4;
    /// <summary>Gap on each side of the lithophane inside the slot, in mm.</summary>
    public double SlotClearance { get; init; } = 0.3;
    public double SupportWall { get; init; } = 2;
    /// <summary>Minimum front-to-back depth of the base for flat and curved lithophanes.</summary>
    public double SupportMinDepth { get; init; } = 30;
    public double LedChannelWidth { get; init; } = 12;
    public double LedChannelDepth { get; init; } = 6;
    public double LampDiameter { get; init; } = 40;
    public double LampDepth { get; init; } = 12;
    public bool LampThroughHole { get; init; }
    public double CableDiameter { get; init; } = 6;

    public bool IsClosed => Shape is ShapeType.Cylinder or ShapeType.Heart or ShapeType.Dome;
    public bool HasLightSupport => LightSupport != LightSupportType.None;
    public bool SupportsStand => Shape != ShapeType.Dome && !HasLightSupport;
}
