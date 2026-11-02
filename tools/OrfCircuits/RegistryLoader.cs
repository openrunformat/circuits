using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrfCircuits;

/// <summary>A circuit file that parsed as a JSON object.</summary>
public sealed record CircuitFile(string RelativePath, JsonObject Json);

public static class RegistryLoader
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads every <c>circuits/&lt;CC&gt;/*.json</c> file under <paramref name="root"/>. Files that are not
    /// valid UTF-8 JSON objects (without BOM) produce a "json" finding; this method never throws because of
    /// file content.
    /// </summary>
    public static IReadOnlyList<CircuitFile> Load(string root, List<Finding> findings)
    {
        var result = new List<CircuitFile>();
        var dir = Path.Combine(root, "circuits");
        if (!Directory.Exists(dir))
            return result;

        foreach (var countryDir in Directory.GetDirectories(dir).Order(StringComparer.Ordinal))
        foreach (var file in Directory.GetFiles(countryDir, "*.json").Order(StringComparer.Ordinal))
        {
            var rel = Relative(root, file);
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                findings.Add(new Finding(rel, "", "json", "File starts with a UTF-8 byte order mark; save it as UTF-8 without BOM."));
                continue;
            }
            try
            {
                var text = StrictUtf8.GetString(bytes);
                var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, AllowDuplicateProperties = false });
                if (node is JsonObject obj)
                    result.Add(new CircuitFile(rel, obj));
                else
                    findings.Add(new Finding(rel, "", "json", "The file must contain a JSON object."));
            }
            catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
            {
                findings.Add(new Finding(rel, "", "json", $"Not valid JSON: {ex.Message}"));
            }
        }
        return result;
    }

    internal static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
