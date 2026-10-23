using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace Utopia.Tests.Tools;

/// <summary>
/// Lists translation keys referenced in source/scenes/resources that are
/// missing from the source catalog, printed as PO stubs ready to paste.
///   godot --headless res://tests/tools/extract_keys.tscn
/// Exit code 1 when keys are missing.
/// </summary>
public partial class ExtractKeys : Node
{
    public override void _Ready()
    {
        var catalog = GD.Load<Translation>("res://assets/localization/en.po");
        var known = new HashSet<string>(catalog.GetMessageList());
        var missing = new SortedDictionary<string, string>();
        foreach (string path in SourceFiles.Collect())
            foreach (Match m in Regex.Matches(FileAccess.GetFileAsString(path), SourceFiles.KeyPattern))
            {
                string key = m.Groups[1].Value;
                if (key.EndsWith("_PLURAL"))
                    key = key[..^"_PLURAL".Length];
                if (!known.Contains(key) && !SourceFiles.NotTranslationKeys.Contains(key))
                    missing[key] = path;
            }
        foreach ((string key, string path) in missing)
            GD.Print($"#. TODO translator note (referenced in {path})\nmsgid \"{key}\"\nmsgstr \"\"\n");
        GD.Print($"{missing.Count} missing key(s)");
        GetTree().Quit(missing.Count > 0 ? 1 : 0);
    }
}

/// <summary>Shared scan rules for translation-key tooling and tests.</summary>
public static class SourceFiles
{
    public static readonly string[] SourceDirs = { "res://autoload", "res://core", "res://features", "res://scenes", "res://automation" };
    public static readonly string[] Extensions = { "cs", "tscn", "tres" };
    public const string KeyPattern = "\"([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+)\"";
    /// <summary>UPPER_SNAKE strings that are not translation keys (environment variable names).</summary>
    public static readonly string[] NotTranslationKeys = { "UTOPIA_AUTOMATION_TOKEN" };

    public static List<string> Collect(IEnumerable<string> dirs = null, IEnumerable<string> extensions = null)
    {
        var files = new List<string>();
        string[] wanted = (extensions ?? Extensions).ToArray();
        foreach (string dir in dirs ?? SourceDirs)
            CollectInto(dir, wanted, files);
        return files;
    }

    private static void CollectInto(string dir, string[] extensions, List<string> output)
    {
        foreach (string sub in DirAccess.GetDirectoriesAt(dir))
            CollectInto(dir.PathJoin(sub), extensions, output);
        foreach (string file in DirAccess.GetFilesAt(dir))
            if (extensions.Contains(file.GetExtension()))
                output.Add(dir.PathJoin(file));
    }
}
