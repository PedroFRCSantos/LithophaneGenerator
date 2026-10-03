using System.Numerics;

namespace LithophaneGUI.Core;

public enum MeshPart
{
    Lithophane,
    /// <summary>The light support, drawn in a different colour in the preview.</summary>
    Support,
    /// <summary>The light source inside the support; preview only, never exported.</summary>
    Light,
}

public sealed class Mesh
{
    public List<Vector3> Vertices { get; } = new();
    /// <summary>Per-vertex wall thickness in mm, used for the backlit preview.</summary>
    public List<float> Thickness { get; } = new();
    /// <summary>
    /// Per-vertex light factor for the backlit preview: for the lithophane, how much of the light
    /// reaches the back of the wall there (0..1); for the light part, how brightly it glows.
    /// </summary>
    public List<float> Illumination { get; } = new();
    public List<int> Indices { get; } = new();

    public int TriangleCount => Indices.Count / 3;

    private readonly List<(MeshPart Kind, int VertexStart, int TriangleStart)> _parts = new();

    /// <summary>Appended parts in order; triangles before the first entry belong to the lithophane.</summary>
    public IReadOnlyList<(MeshPart Kind, int VertexStart, int TriangleStart)> Parts => _parts;

    public Mesh Clone()
    {
        var m = new Mesh();
        m.Vertices.AddRange(Vertices);
        m.Thickness.AddRange(Thickness);
        m.Illumination.AddRange(Illumination);
        m.Indices.AddRange(Indices);
        m._parts.AddRange(_parts);
        return m;
    }

    /// <summary>Appends another mesh as the light support.</summary>
    public void AppendSecondary(Mesh other) => Append(other, MeshPart.Support);

    /// <summary>Appends another mesh as a part of the given kind.</summary>
    public void Append(Mesh other, MeshPart kind)
    {
        int offset = Vertices.Count;
        _parts.Add((kind, offset, TriangleCount));
        Vertices.AddRange(other.Vertices);
        Thickness.AddRange(other.Thickness);
        Illumination.AddRange(other.Illumination);
        // Keep the per-vertex lists aligned even if the other mesh did not fill them.
        while (Thickness.Count < Vertices.Count) Thickness.Add(0);
        while (Illumination.Count < Vertices.Count) Illumination.Add(1);
        Indices.Capacity = Indices.Count + other.Indices.Count;
        foreach (int i in other.Indices) Indices.Add(i + offset);
    }

    public (Vector3 Min, Vector3 Max) Bounds()
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var v in Vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        return (min, max);
    }

    public void Translate(Vector3 offset)
    {
        for (int i = 0; i < Vertices.Count; i++)
            Vertices[i] += offset;
    }

    /// <summary>Signed volume; positive when triangles are wound counter-clockwise seen from outside.</summary>
    public double SignedVolume()
    {
        double vol = 0;
        for (int i = 0; i < Indices.Count; i += 3)
        {
            var a = Vertices[Indices[i]];
            var b = Vertices[Indices[i + 1]];
            var c = Vertices[Indices[i + 2]];
            vol += Vector3.Dot(a, Vector3.Cross(b, c));
        }
        return vol / 6.0;
    }

    public void FlipWinding()
    {
        for (int i = 0; i < Indices.Count; i += 3)
            (Indices[i + 1], Indices[i + 2]) = (Indices[i + 2], Indices[i + 1]);
    }
}
