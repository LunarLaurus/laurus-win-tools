using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipTray;

public sealed class ImageStore
{
    private readonly string _dir;

    public ImageStore(string itemsDirectory)
    {
        _dir = itemsDirectory;
        Directory.CreateDirectory(_dir);
    }

    public string PathFor(string hash) => Path.Combine(_dir, hash + ".png");

    public string Write(string hash, byte[] pngBytes)
    {
        var path = PathFor(hash);
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, pngBytes);
        }
        return path;
    }

    public bool Exists(string hash) => File.Exists(PathFor(hash));

    public void Delete(string hash)
    {
        try { File.Delete(PathFor(hash)); }
        catch (FileNotFoundException) { }
    }

    public long TotalBytes()
    {
        if (!Directory.Exists(_dir)) return 0;
        return Directory.EnumerateFiles(_dir, "*.png")
            .Sum(p => new FileInfo(p).Length);
    }

    public void SweepOrphans(IEnumerable<string> knownHashes)
    {
        if (!Directory.Exists(_dir)) return;
        var known = new HashSet<string>(knownHashes, System.StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(_dir, "*.png"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!known.Contains(name))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}
