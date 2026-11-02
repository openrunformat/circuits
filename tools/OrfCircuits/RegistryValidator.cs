using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace OrfCircuits;

public static class RegistryValidator
{
    private static readonly Lazy<JsonSchema> CircuitSchema = new(() =>
        JsonSchema.FromText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema", "circuit.schema.json"))));

    /// <summary>Validates the registry under <paramref name="root"/>; findings sorted by File, Path, Code (ordinal).</summary>
    public static IReadOnlyList<Finding> Validate(string root)
    {
        var findings = new List<Finding>();
        CheckStrayFiles(root, findings);
        var files = RegistryLoader.Load(root, findings);

        var wellFormed = new List<CircuitFile>();
        foreach (var file in files)
        {
            var schemaFindings = CheckSchema(file);
            findings.AddRange(schemaFindings);
            if (schemaFindings.Count > 0)
                continue;
            CheckFileName(file, findings);
            wellFormed.Add(file);
        }

        var systems = SemanticChecks.LoadSystems(root, findings);
        foreach (var file in wellFormed)
            SemanticChecks.CheckFile(file, systems, findings);
        SemanticChecks.CheckRegistry(wellFormed, findings);

        return findings
            .Distinct()
            .OrderBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Message, StringComparer.Ordinal)
            .ToList();
    }

    private static void CheckStrayFiles(string root, List<Finding> findings)
    {
        var dir = Path.Combine(root, "circuits");
        if (!Directory.Exists(dir))
            return;
        foreach (var file in Directory.GetFiles(dir))
            if (Path.GetFileName(file) != "README.md")
                findings.Add(new Finding(RegistryLoader.Relative(root, file), "", "stray-file",
                    "Only README.md may sit directly in circuits/; circuit files go in circuits/<CC>/."));
        foreach (var sub in Directory.GetDirectories(dir))
        {
            foreach (var file in Directory.GetFiles(sub))
                if (!file.EndsWith(".json", StringComparison.Ordinal))
                    findings.Add(new Finding(RegistryLoader.Relative(root, file), "", "stray-file",
                        "Only circuit files (*.json) may sit in a country folder."));
            foreach (var nested in Directory.GetDirectories(sub))
                findings.Add(new Finding(RegistryLoader.Relative(root, nested), "", "stray-file",
                    "Country folders must not contain subfolders."));
        }
    }

    private static List<Finding> CheckSchema(CircuitFile file)
    {
        var result = new List<Finding>();
        using var doc = JsonDocument.Parse(file.Json.ToJsonString());
        var evaluation = CircuitSchema.Value.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (evaluation.IsValid)
        {
            // .NET regular expressions let "$" match before a final "\n", so the schema patterns alone do not
            // keep control characters out of ids, uids and dates.
            CheckControlCharacters(file.RelativePath, file.Json, "", result);
            return result;
        }
        foreach (var detail in evaluation.Details ?? [])
            if (detail.Errors is { Count: > 0 } errors)
                foreach (var error in errors)
                    result.Add(new Finding(file.RelativePath, detail.InstanceLocation.ToString(), "schema", $"{error.Key}: {error.Value}"));
        if (result.Count == 0)
            result.Add(new Finding(file.RelativePath, "", "schema", "Does not match schema/circuit.schema.json."));
        return result;
    }

    private static void CheckControlCharacters(string file, JsonNode? node, string path, List<Finding> result)
    {
        static bool HasControl(string s) => s.Any(char.IsControl);
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o)
                {
                    var p = $"{path}/{key.Replace("~", "~0").Replace("/", "~1")}";
                    if (HasControl(key))
                        result.Add(new Finding(file, p, "schema", "Property names must not contain control characters."));
                    CheckControlCharacters(file, value, p, result);
                }
                break;
            case JsonArray a:
                for (int i = 0; i < a.Count; i++)
                    CheckControlCharacters(file, a[i], $"{path}/{i}", result);
                break;
            case JsonValue v when v.TryGetValue<string>(out var s) && HasControl(s):
                result.Add(new Finding(file, path, "schema", "Text values must not contain control characters (line breaks, tabs...)."));
                break;
        }
    }

    private static void CheckFileName(CircuitFile file, List<Finding> findings)
    {
        var id = file.Json["id"]!.GetValue<string>();
        var country = file.Json["country"]!.GetValue<string>();
        var dash = id.IndexOf('-');
        var idCountry = id[..dash];
        var idName = id[(dash + 1)..];
        var parts = file.RelativePath.Split('/');   // circuits / CC / name.json
        var folder = parts[1];

        if (file.RelativePath != $"circuits/{idCountry}/{idName}.json")
            findings.Add(new Finding(file.RelativePath, "/id", "file-name",
                $"Circuit '{id}' must be stored as circuits/{idCountry}/{idName}.json."));
        if (country != idCountry || country != folder)
            findings.Add(new Finding(file.RelativePath, "/country", "country",
                $"Country '{country}' must match the id prefix '{idCountry}' and the folder '{folder}'."));
        if (!CountryCodes.Assigned.Contains(country))
            findings.Add(new Finding(file.RelativePath, "/country", "country",
                $"'{country}' is not an assigned ISO 3166-1 alpha-2 code."));
    }
}
