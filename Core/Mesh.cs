using System.Numerics;

namespace LithophaneGUI.Core;

public sealed class Mesh
{
    public List<Vector3> Vertices { get; } = new();
    /// <summary>Per-vertex wall thickness in mm, used for the backlit preview.</summary>
    public List<float> Thickness { get; } = new();
    public List<int> Indices { get; } = new();

    public int TriangleCount => Indices.Count / 3;

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
