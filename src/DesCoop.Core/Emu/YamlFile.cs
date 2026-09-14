using System.Text;
using YamlDotNet.RepresentationModel;

namespace DesCoop.Emu;

/// <summary>Round-trips RPCS3's YAML files and edits only the keys we care about.</summary>
public sealed class YamlFile
{
    readonly string _path;
    public YamlMappingNode Root { get; }

    public YamlFile(string path)
    {
        _path = path;
        Root = new YamlMappingNode();
        if (!File.Exists(path)) return;
        try
        {
            var ys = new YamlStream();
            ys.Load(new StringReader(File.ReadAllText(path)));
            if (ys.Documents.Count > 0 && ys.Documents[0].RootNode is YamlMappingNode m) Root = m;
        }
        catch { /* corrupt/empty file: start fresh */ }
    }

    public static YamlMappingNode Map(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var n) && n is YamlMappingNode m) return m;
        var created = new YamlMappingNode();
        parent.Children[k] = created;
        return created;
    }

    public static void Set(YamlMappingNode parent, string key, string value) =>
        parent.Children[new YamlScalarNode(key)] = new YamlScalarNode(value);

    public static string? Get(YamlMappingNode parent, string key) =>
        parent.Children.TryGetValue(new YamlScalarNode(key), out var n) && n is YamlScalarNode s ? s.Value : null;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var sw = new StringWriter();
        new YamlStream(new YamlDocument(Root)).Save(sw, false);
        var text = sw.ToString().Replace("\r\n", "\n");
        if (text.EndsWith("...\n")) text = text[..^4];
        File.WriteAllText(_path, text, new UTF8Encoding(false));
    }
}

/// <summary>Tiny Qt-style INI editor (RPCS3 GUI settings).</summary>
public static class IniFile
{
    public static void Set(string path, string section, IDictionary<string, string> values)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        int start = lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            if (lines.Count > 0 && lines[^1].Length > 0) lines.Add("");
            lines.Add($"[{section}]");
            start = lines.Count - 1;
        }
        int end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith('[')) end++;
        foreach (var (k, v) in values)
        {
            int idx = lines.FindIndex(start + 1, end - start - 1, l => l.Split('=')[0].Trim() == k);
            if (idx >= 0) lines[idx] = $"{k}={v}";
            else { lines.Insert(end, $"{k}={v}"); end++; }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }
}
