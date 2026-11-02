using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrfCircuits;

/// <summary>Builds <c>dist/index.json</c>: every circuit, without provenance details, in a stable byte-exact form.</summary>
public static class IndexBuilder
{
    public const string Source = "https://github.com/openrunformat/circuits";

    /// <summary>
    /// Returns the index as indented JSON (2 spaces), LF line ends and a final newline. Throws
    /// <see cref="InvalidOperationException"/> when the registry has findings.
    /// </summary>
    public static string Build(string root, DateTimeOffset generatedAt)
    {
        var findings = RegistryValidator.Validate(root);
        if (findings.Count > 0)
            throw new InvalidOperationException(
                $"The registry has {findings.Count} finding(s); run 'orf-circuits validate' first. First: {findings[0]}");

        var files = RegistryLoader.Load(root, []).OrderBy(f => SemanticChecks.Str(f.Json, "id"), StringComparer.Ordinal);

        var buffer = new MemoryStream();
        var options = new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var w = new Utf8JsonWriter(buffer, options))
        {
            w.WriteStartObject();
            w.WriteNumber("version", 1);
            w.WriteString("generatedAt", generatedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            w.WriteString("source", Source);
            w.WriteString("licence", "CC0-1.0");
            w.WriteStartArray("circuits");
            foreach (var file in files)
                WriteCircuit(w, file.Json);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    private static void WriteCircuit(Utf8JsonWriter w, JsonObject j)
    {
        w.WriteStartObject();
        Copy(w, j, "id");
        Copy(w, j, "uid");
        CopyOr(w, j, "aliases", () => w.WriteStartArray("aliases"), () => w.WriteEndArray());
        Copy(w, j, "name");
        CopyOr(w, j, "names", () => w.WriteStartObject("names"), () => w.WriteEndObject());
        Copy(w, j, "country");
        w.WritePropertyName("location");
        w.WriteStartObject();
        Copy(w, j["location"]!.AsObject(), "lat");
        Copy(w, j["location"]!.AsObject(), "lon");
        w.WriteEndObject();
        CopyOrNull(w, j, "website");

        var provenance = SemanticChecks.Arr(j, "provenance").Select(p => p!.AsObject())
            .ToDictionary(p => SemanticChecks.Str(p, "id")!, StringComparer.Ordinal);
        var cited = new HashSet<string>(StringComparer.Ordinal);

        w.WriteStartArray("layouts");
        foreach (var layoutNode in SemanticChecks.Arr(j, "layouts"))
        {
            var layout = layoutNode!.AsObject();
            w.WriteStartObject();
            Copy(w, layout, "id");
            Copy(w, layout, "uid");
            Copy(w, layout, "name");
            CopyOrNull(w, layout, "direction");
            w.WriteStartArray("versions");
            foreach (var versionNode in SemanticChecks.Arr(layout, "versions"))
            {
                var v = versionNode!.AsObject();
                w.WriteStartObject();
                CopyOrNull(w, v, "id");
                CopyOrNull(w, v, "from");
                CopyOrNull(w, v, "to");
                CopyOrNull(w, v, "length_m");
                WriteGate(w, "start", v["start"]!, cited);
                WriteGate(w, "finish", v["finish"]!, cited);
                w.WriteStartArray("splits");
                foreach (var split in SemanticChecks.Arr(v, "splits"))
                    WriteGate(w, null, split!, cited);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteStartArray("external");
        foreach (var e in SemanticChecks.Arr(j, "external"))
        {
            w.WriteStartObject();
            foreach (var key in new[] { "system", "id", "scope", "layout", "version", "from", "to" })
                if (e![key] is not null)
                    Copy(w, e.AsObject(), key);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        w.WriteStartArray("attribution");
        var attributions = cited
            .Select(id => provenance.TryGetValue(id, out var p) ? SemanticChecks.Str(p, "attribution") : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        foreach (var a in attributions)
            w.WriteStringValue(a);
        w.WriteEndArray();

        w.WriteEndObject();
    }

    private static void WriteGate(Utf8JsonWriter w, string? name, JsonNode gate, HashSet<string> cited)
    {
        cited.Add(gate["provenance"]!.GetValue<string>());
        if (name is not null)
            w.WritePropertyName(name);
        gate["line"]!.WriteTo(w);
    }

    private static void Copy(Utf8JsonWriter w, JsonObject o, string name)
    {
        w.WritePropertyName(name);
        o[name]!.WriteTo(w);
    }

    private static void CopyOrNull(Utf8JsonWriter w, JsonObject o, string name)
    {
        if (o[name] is null)
            w.WriteNull(name);
        else
            Copy(w, o, name);
    }

    private static void CopyOr(Utf8JsonWriter w, JsonObject o, string name, Action writeEmptyStart, Action writeEmptyEnd)
    {
        if (o[name] is not null)
        {
            Copy(w, o, name);
            return;
        }
        writeEmptyStart();
        writeEmptyEnd();
    }
}
