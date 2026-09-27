using System.Numerics;
using System.Text;

namespace LithophaneGUI.Core;

public static class StlWriter
{
    public static void WriteBinary(Mesh mesh, string path, string name = "lithophane")
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        using var w = new BinaryWriter(stream);

        var header = new byte[80];
        Encoding.ASCII.GetBytes($"LithophaneGUI {name}".PadRight(80)[..80]).CopyTo(header, 0);
        w.Write(header);
        w.Write((uint)mesh.TriangleCount);

        var verts = mesh.Vertices;
        var idx = mesh.Indices;
        for (int t = 0; t < idx.Count; t += 3)
        {
            var a = verts[idx[t]];
            var b = verts[idx[t + 1]];
            var c = verts[idx[t + 2]];
            var n = Vector3.Cross(b - a, c - a);
            float len = n.Length();
            n = len > 0 ? n / len : Vector3.Zero;

            Write(w, n);
            Write(w, a);
            Write(w, b);
            Write(w, c);
            w.Write((ushort)0);
        }
    }

    private static void Write(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }
}
