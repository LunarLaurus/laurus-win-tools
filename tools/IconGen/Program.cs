using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace IconGen;

internal static class Program
{
    private static readonly int[] DefaultSizes = { 16, 24, 32, 48, 64, 128, 256 };

    private static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] is not "--glyph")
        {
            Console.Error.WriteLine("Usage: IconGen --glyph <name> <output.ico>");
            Console.Error.WriteLine("Glyphs: clipboard");
            return 2;
        }

        string glyph = args[1];
        string output = args.Length >= 3 ? args[2] : (glyph + ".ico");

        Func<int, byte[]> renderer = glyph switch
        {
            "clipboard" => RenderClipboard,
            _ => throw new ArgumentException($"Unknown glyph '{glyph}'"),
        };

        var pngs = DefaultSizes.Select(renderer).ToArray();
        WriteIco(output, DefaultSizes, pngs);
        Console.WriteLine($"IconGen: wrote {output} ({DefaultSizes.Length} frames: {string.Join(", ", DefaultSizes)})");
        return 0;
    }

    private static byte[] RenderClipboard(int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        // All measurements are computed relative to a 256x256 reference grid
        // and scaled down. This keeps proportions identical across all sizes.
        float s = size / 256f;

        // Board (clipboard backing): rounded rect with a soft accent blue.
        var board = new RectangleF(
            32 * s,
            44 * s,
            256 * s - 64 * s,
            256 * s - 64 * s);
        using (var brush = new SolidBrush(Color.FromArgb(255, 37, 99, 235))) // #2563eb
            FillRoundedRect(g, board, 18 * s, brush);

        // Paper sheet: slightly smaller white rounded rect, offset down so the
        // clip has room at the top.
        var paper = new RectangleF(
            board.Left + 14 * s,
            board.Top + 28 * s,
            board.Width - 28 * s,
            board.Height - 42 * s);
        using (var brush = new SolidBrush(Color.FromArgb(255, 248, 250, 252))) // #f8fafc
            FillRoundedRect(g, paper, 8 * s, brush);

        // Clip: small rectangle at the top center, straddling the board edge.
        float clipW = 72 * s;
        float clipH = 36 * s;
        var clip = new RectangleF(
            (size - clipW) / 2f,
            board.Top - clipH * 0.45f,
            clipW,
            clipH);
        using (var brush = new SolidBrush(Color.FromArgb(255, 30, 64, 175))) // darker blue, #1e40af
            FillRoundedRect(g, clip, 8 * s, brush);

        // Content lines on the paper (skip for the smallest sizes — they'd
        // smudge to noise at 16px).
        if (size >= 32)
        {
            using var lineBrush = new SolidBrush(Color.FromArgb(255, 156, 175, 195));
            float lineH = Math.Max(2, 8 * s);
            float lineLeft  = paper.Left  + 18 * s;
            float lineRight = paper.Right - 18 * s;
            float top = paper.Top + 36 * s;
            float step = 28 * s;
            for (int i = 0; i < 3; i++)
                g.FillRectangle(lineBrush, lineLeft, top + i * step, lineRight - lineLeft, lineH);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static void FillRoundedRect(Graphics g, RectangleF r, float radius, Brush brush)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        using var path = new GraphicsPath();
        path.AddArc(r.Left,           r.Top,            d, d, 180, 90);
        path.AddArc(r.Right - d,      r.Top,            d, d, 270, 90);
        path.AddArc(r.Right - d,      r.Bottom - d,     d, d,   0, 90);
        path.AddArc(r.Left,           r.Bottom - d,     d, d,  90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private static void WriteIco(string output, int[] sizes, byte[][] pngs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        using var fs = File.Create(output);
        using var bw = new BinaryWriter(fs);

        // ICONDIR (6 bytes)
        bw.Write((ushort)0);             // reserved
        bw.Write((ushort)1);             // type = ICO
        bw.Write((ushort)sizes.Length);  // image count

        // ICONDIRENTRY[count] (16 bytes each)
        int dataOffset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int dim = sizes[i];
            bw.Write((byte)(dim >= 256 ? 0 : dim)); // width (0 means 256)
            bw.Write((byte)(dim >= 256 ? 0 : dim)); // height
            bw.Write((byte)0);                       // colorCount (0 = no palette)
            bw.Write((byte)0);                       // reserved
            bw.Write((ushort)1);                     // planes
            bw.Write((ushort)32);                    // bitCount
            bw.Write((uint)pngs[i].Length);          // bytesInRes
            bw.Write((uint)dataOffset);              // imageOffset
            dataOffset += pngs[i].Length;
        }

        foreach (var png in pngs)
            bw.Write(png);
    }
}
