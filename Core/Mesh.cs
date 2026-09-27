using System.Numerics;

namespace LithophaneGUI.Core;

public sealed class Mesh
{
    public List<Vector3> Vertices { get; } = new();
    /// <summary>Per-vertex wall thickness in mm, used for the backlit preview.</summary>
    public List<float> Thickness { get; } = new();
    public List<int> Indices { get; } = new();

    public int TriangleCount => Indices.Count / 3;

    /// <summary>
    /// First vertex / triangle of a secondary part (the light support), drawn in a different
    /// colour in the preview. Equal to the counts when there is none.
    /// </summary>
    public int SecondaryVertexStart { get; private set; } = int.MaxValue;
    public int SecondaryTriangleStart { get; private set; } = int.MaxValue;

    public Mesh Clone()
    {
        var m = new Mesh();
        m.Vertices.AddRange(Vertices);
        m.Thickness.AddRange(Thickness);
        m.Indices.AddRange(Indices);
        m.SecondaryVertexStart = SecondaryVertexStart;
        m.SecondaryTriangleStart = SecondaryTriangleStart;
        return m;
    }

    /// <summary>Appends another mesh as the secondary part.</summary>
    public void AppendSecondary(Mesh other)
    {
        int offset = Vertices.Count;
        SecondaryVertexStart = Math.Min(SecondaryVertexStart, offset);
        SecondaryTriangleStart = Math.Min(SecondaryTriangleStart, TriangleCount);
        Vertices.AddRange(other.Vertices);
        Thickness.AddRange(other.Thickness);
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
