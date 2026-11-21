using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace LithophaneGUI.Core;

/// <summary>
/// Writes a 3MF file (a zip with one XML model). Unlike STL it keeps the parts of a mesh apart:
/// lithophane, base and stand become separate objects, so a slicer can give each its own settings
/// (100 % infill for the picture, less for the base). Only the core 3MF specification is used.
/// </summary>
public static class ThreeMfWriter
{
    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"model\" ContentType=\"application/vnd.ms-package.3dmanufacturing-3dmodel+xml\"/>" +
        "</Types>\n";

    private const string Relationships =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Target=\"/3D/3dmodel.model\" Id=\"rel0\" " +
        "Type=\"http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel\"/>" +
        "</Relationships>\n";

    /// <param name="name">Name of the whole model, shown by the slicer.</param>
    /// <param name="partName">Display name for each kind of part.</param>
    /// <param name="loose">
    /// False: the parts form one assembly (one object with a component per part, which slicers show
    /// as one object with parts). True: every part is an object of its own on the build plate.
    /// </param>
    public static void Write(Mesh mesh, string path, string name, Func<MeshPart, string> partName, bool loose = false)
    {
        var parts = mesh.PartRanges().Where(p => p.Kind != MeshPart.Light).ToList();

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        WriteText(zip, "[Content_Types].xml", ContentTypes);
        WriteText(zip, "_rels/.rels", Relationships);

        var entry = zip.CreateEntry("3D/3dmodel.model", CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false), 1 << 16) { NewLine = "\n" };
        w.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        w.WriteLine("<model unit=\"millimeter\" xml:lang=\"en-US\" " +
                    "xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">");
        w.WriteLine(" <metadata name=\"Application\">Lithophane Maker</metadata>");
        w.WriteLine(" <resources>");

        for (int p = 0; p < parts.Count; p++)
        {
            w.WriteLine($"  <object id=\"{p + 1}\" type=\"model\" name=\"{Escape(partName(parts[p].Kind))}\">");
            WriteMesh(w, mesh, parts[p]);
            w.WriteLine("  </object>");
        }

        // One part needs no wrapper; several parts of one assembly are components of a parent object.
        var items = new List<int>();
        if (loose || parts.Count == 1)
        {
            for (int p = 0; p < parts.Count; p++) items.Add(p + 1);
        }
        else
        {
            int parent = parts.Count + 1;
            w.WriteLine($"  <object id=\"{parent}\" type=\"model\" name=\"{Escape(name)}\">");
            w.WriteLine("   <components>");
            for (int p = 0; p < parts.Count; p++)
                w.WriteLine($"    <component objectid=\"{p + 1}\"/>");
            w.WriteLine("   </components>");
            w.WriteLine("  </object>");
            items.Add(parent);
        }

        w.WriteLine(" </resources>");
        w.WriteLine(" <build>");
        foreach (int id in items)
            w.WriteLine($"  <item objectid=\"{id}\"/>");
        w.WriteLine(" </build>");
        w.WriteLine("</model>");
    }

    private static void WriteMesh(StreamWriter w, Mesh mesh, PartRange part)
    {
        var verts = mesh.Vertices;
        var idx = mesh.Indices;
        Span<char> buffer = stackalloc char[32];

        void Number(float value, Span<char> buf)
        {
            value.TryFormat(buf, out int n, default, CultureInfo.InvariantCulture);
            w.Write(buf[..n]);
        }

        w.WriteLine("   <mesh>");
        w.WriteLine("    <vertices>");
        for (int i = part.VertexStart; i < part.VertexStart + part.VertexCount; i++)
        {
            var v = verts[i];
            w.Write("<vertex x=\""); Number(v.X, buffer);
            w.Write("\" y=\""); Number(v.Y, buffer);
            w.Write("\" z=\""); Number(v.Z, buffer);
            w.Write("\"/>\n");
        }
        w.WriteLine("    </vertices>");
        w.WriteLine("    <triangles>");
        for (int t = part.TriangleStart; t < part.TriangleStart + part.TriangleCount; t++)
        {
            int a = idx[3 * t] - part.VertexStart, b = idx[3 * t + 1] - part.VertexStart, c = idx[3 * t + 2] - part.VertexStart;
            if (a == b || b == c || a == c) continue; // 3MF forbids a triangle that repeats a vertex
            w.Write("<triangle v1=\""); w.Write(a);
            w.Write("\" v2=\""); w.Write(b);
            w.Write("\" v3=\""); w.Write(c);
            w.Write("\"/>\n");
        }
        w.WriteLine("    </triangles>");
        w.WriteLine("   </mesh>");
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    private static string Escape(string text) => SecurityElement.Escape(text) ?? "";
}
