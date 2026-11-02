using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrfCircuits.Tests;

/// <summary>Builds a temporary copy of the fixture registry, optionally changed.</summary>
public static class TestRegistry
{
    public const string FixtureFile = "circuits/ES/synthetic-ring.json";

    public static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "fixtures", "registry");

    public static JsonObject LoadFixture() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureRoot, FixtureFile)))!.AsObject();

    public static string Serialize(JsonNode node) =>
        node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n";

    public static string Create(Action<JsonObject>? mutate = null, params (string path, string content)[] extraFiles)
    {
        var root = Path.Combine(Path.GetTempPath(), "orf-circuits-tests", Guid.NewGuid().ToString("N"));
        Copy(FixtureRoot, root);
        if (mutate is not null)
        {
            var json = LoadFixture();
            mutate(json);
            File.WriteAllText(Path.Combine(root, FixtureFile), Serialize(json));
        }
        foreach (var (path, content) in extraFiles)
        {
            var full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        return root;
    }

    /// <summary>A registry with no circuits: only circuits/README.md.</summary>
    public static string CreateEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "orf-circuits-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "circuits"));
        File.WriteAllText(Path.Combine(root, "circuits", "README.md"), "Circuits.\n");
        return root;
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from))
            Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
