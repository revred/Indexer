using System.Security.Cryptography;
using System.Text.Json;

namespace IndexContainment.Export;

public static class ManifestWriter
{
    public static string Write(string outputRoot, IDictionary<string, object> meta, IEnumerable<string> files)
    {
        Directory.CreateDirectory(outputRoot);
        var items = new List<object>();
        foreach (var f in files.Distinct())
        {
            if (!File.Exists(f)) continue;
            items.Add(new {
                path = f.Replace("\\","/"),
                sha256 = Sha256Of(f),
                size = new FileInfo(f).Length
            });
        }
        var payload = new {
            generatedUtc = DateTime.UtcNow,
            meta,
            artifacts = items
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(outputRoot, "manifest.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Sha256Of(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var bytes = sha.ComputeHash(fs);
        return string.Concat(bytes.Select(b => b.ToString("x2")));
    }
}