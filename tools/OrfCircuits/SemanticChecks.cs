using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenRunFormat;

namespace OrfCircuits;

/// <summary>Checks that need more than the JSON schema (run only on files that passed the schema).</summary>
internal static class SemanticChecks
{
    public const double MinGateWidthM = 5;
    public const double MaxGateWidthM = 100;
    public const double MaxGateDistanceM = 10_000;

    private static readonly string[] AllowedLicences = ["CC0-1.0", "CC-BY-4.0"];

    public sealed record ExternalSystem(string Id, Regex Pattern);

    public static IReadOnlyDictionary<string, ExternalSystem> LoadSystems(string root)
    {
        var path = Path.Combine(root, "schema", "systems.json");
        if (!File.Exists(path))
            path = Path.Combine(AppContext.BaseDirectory, "schema", "systems.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        return json["systems"]!.AsArray()
            .Select(s => new ExternalSystem(s!["id"]!.GetValue<string>(),
                new Regex(s["pattern"]!.GetValue<string>(), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))))
            .ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    /// <summary>Checks inside one file.</summary>
    public static void CheckFile(CircuitFile file, IReadOnlyDictionary<string, ExternalSystem> systems, List<Finding> findings)
    {
        var j = file.Json;
        void Add(string path, string code, string message) => findings.Add(new Finding(file.RelativePath, path, code, message));

        var id = Str(j, "id")!;
        if (!CircuitId.IsValidCircuit(id) || CircuitId.TryParse(id, out var parsed) && parsed.IsProvisional)
            Add("/id", "id-syntax", $"'{id}' is not a valid registry circuit id.");
        var aliases = Arr(j, "aliases");
        for (int i = 0; i < aliases.Count; i++)
        {
            var alias = aliases[i]!.GetValue<string>();
            if (!CircuitId.IsValidCircuit(alias))
                Add($"/aliases/{i}", "id-syntax", $"'{alias}' is not a valid registry circuit id.");
        }

        CheckUidVersion(j, "/uid", Add);

        var layouts = Arr(j, "layouts");
        var layoutIds = new HashSet<string>(StringComparer.Ordinal);
        for (int li = 0; li < layouts.Count; li++)
        {
            var layout = layouts[li]!.AsObject();
            var lp = $"/layouts/{li}";
            var layoutId = Str(layout, "id")!;
            if (!layoutIds.Add(layoutId))
                Add($"{lp}/id", "duplicate-layout", $"Layout '{layoutId}' appears more than once.");
            if (layoutId.Contains('@') || !CircuitId.IsValidVariant(layoutId))
                Add($"{lp}/id", "id-syntax", $"'{layoutId}' is not a valid layout id.");
            CheckUidVersion(layout, $"{lp}/uid", Add);
            CheckVersions(id, layout, lp, Add);
        }

        var provenance = Arr(j, "provenance").Select(p => p!.AsObject())
            .GroupBy(p => Str(p, "id")!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        CheckProvenance(j, Add);
        CheckGates(j, provenance, Add);
        CheckExternal(j, systems, Add);
    }

    private static void CheckUidVersion(JsonObject owner, string path, Action<string, string, string> add)
    {
        var uid = Guid.Parse(Str(owner, "uid")!);
        if (uid.Version != 7)
            add(path, "uid-version", $"uid must be a UUID version 7 (found version {uid.Version}); generate one with 'orf-circuits uid'.");
    }

    private static void CheckVersions(string circuitId, JsonObject layout, string lp, Action<string, string, string> add)
    {
        var versions = Arr(layout, "versions");
        var layoutId = Str(layout, "id")!;
        var versionIds = new HashSet<string>(StringComparer.Ordinal);
        var intervals = new List<(int Index, DateOnly From, DateOnly? To)>();
        for (int vi = 0; vi < versions.Count; vi++)
        {
            var v = versions[vi]!.AsObject();
            var vp = $"{lp}/versions/{vi}";
            var vid = Str(v, "id");
            if (vid is not null)
            {
                if (!versionIds.Add(vid))
                    add($"{vp}/id", "duplicate-version", $"Version '{vid}' appears more than once in layout '{layoutId}'.");
                if (!CircuitId.TryParse($"{circuitId}/{layoutId}@{vid}", out _))
                    add($"{vp}/id", "id-syntax", $"'{circuitId}/{layoutId}@{vid}' is not a valid variant id.");
            }
            if (versions.Count > 1 && (vid is null || v["from"] is null))
                add(vp, "version-dates", "When a layout has several versions, each needs an 'id' and a 'from' date.");

            var from = Date(v, "from", $"{vp}/from", add, out var fromOk);
            var to = Date(v, "to", $"{vp}/to", add, out var toOk);
            if (!fromOk || !toOk)
                continue;
            if (from is { } f && to is { } t && t <= f)
                add($"{vp}/to", "version-dates", "'to' must be after 'from'.");
            if (vid is not null && from is { } start && !VersionIdMatches(vid, start))
                add($"{vp}/id", "version-id", $"Version id '{vid}' does not match 'from' {start:yyyy-MM-dd}.");
            intervals.Add((vi, from ?? DateOnly.MinValue, to));
        }

        for (int a = 0; a < intervals.Count; a++)
        for (int b = a + 1; b < intervals.Count; b++)
        {
            var (ia, fa, ta) = intervals[a];
            var (ib, fb, tb) = intervals[b];
            var aEndsBeforeB = ta is { } ea && ea <= fb;
            var bEndsBeforeA = tb is { } eb && eb <= fa;
            if (!aEndsBeforeB && !bEndsBeforeA)
                add($"{lp}/versions/{ib}", "version-dates", $"Versions {ia} and {ib} of layout '{layoutId}' overlap in time.");
        }
    }

    private static bool VersionIdMatches(string vid, DateOnly from) =>
        vid.Length == 4
            ? from.Year == int.Parse(vid, CultureInfo.InvariantCulture)
            : from.Year == int.Parse(vid[..4], CultureInfo.InvariantCulture) && from.Month == int.Parse(vid[5..], CultureInfo.InvariantCulture);

    private static DateOnly? Date(JsonObject owner, string name, string path, Action<string, string, string> add, out bool ok)
    {
        ok = true;
        var text = Str(owner, name);
        if (text is null)
            return null;
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        ok = false;
        add(path, "version-dates", $"'{text}' is not a valid date.");
        return null;
    }

    private static IEnumerable<(string Path, JsonObject Gate)> Gates(JsonObject j)
    {
        var layouts = Arr(j, "layouts");
        for (int li = 0; li < layouts.Count; li++)
        {
            var versions = Arr(layouts[li]!.AsObject(), "versions");
            for (int vi = 0; vi < versions.Count; vi++)
            {
                var v = versions[vi]!.AsObject();
                var vp = $"/layouts/{li}/versions/{vi}";
                yield return ($"{vp}/start", v["start"]!.AsObject());
                yield return ($"{vp}/finish", v["finish"]!.AsObject());
                var splits = Arr(v, "splits");
                for (int si = 0; si < splits.Count; si++)
                    yield return ($"{vp}/splits/{si}", splits[si]!.AsObject());
            }
        }
    }

    private static void CheckGates(JsonObject j, Dictionary<string, JsonObject> provenance, Action<string, string, string> add)
    {
        var location = j["location"]!;
        var lat0 = location["lat"]!.GetValue<double>();
        var lon0 = location["lon"]!.GetValue<double>();
        foreach (var (path, gate) in Gates(j))
        {
            var line = gate["line"]!.AsArray().Select(n => n!.GetValue<double>()).ToArray();
            var width = Geo.DistanceM(line[0], line[1], line[2], line[3]);
            if (width < MinGateWidthM || width > MaxGateWidthM)
                add($"{path}/line", "gate-width",
                    string.Create(CultureInfo.InvariantCulture, $"Gate is {width:0.0} m wide; it must be between {MinGateWidthM} and {MaxGateWidthM} m."));
            var midLat = (line[0] + line[2]) / 2;
            var midLon = (line[1] + line[3]) / 2;
            var distance = Geo.DistanceM(lat0, lon0, midLat, midLon);
            if (distance > MaxGateDistanceM)
                add($"{path}/line", "gate-location",
                    string.Create(CultureInfo.InvariantCulture, $"Gate is {distance / 1000:0.0} km from the circuit location; are latitude and longitude swapped?"));

            var pid = Str(gate, "provenance")!;
            if (!provenance.TryGetValue(pid, out var p))
                add($"{path}/provenance", "provenance-missing", $"Provenance '{pid}' is not declared in this file.");
            else if (Str(p, "method") == "official")
                add($"{path}/provenance", "provenance-method", $"Gates must be measured ('measured-gps' or 'orthophoto'); '{pid}' is 'official'.");
        }
    }

    private static void CheckProvenance(JsonObject j, Action<string, string, string> add)
    {
        var list = Arr(j, "provenance");
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i]!.AsObject();
            var pp = $"/provenance/{i}";
            var licence = Str(p, "licence");
            if (Str(p, "method") == "orthophoto" && (Str(p, "source") is null || licence is null))
                add(pp, "provenance-licence", "Orthophoto provenance needs 'source' and 'licence'.");
            if (licence is not null && !AllowedLicences.Contains(licence, StringComparer.Ordinal))
            {
                if (licence == "ODbL-1.0")
                    add($"{pp}/licence", "provenance-forbidden", "OpenStreetMap data (ODbL) cannot be used in this registry.");
                else
                    add($"{pp}/licence", "provenance-licence", $"Licence '{licence}' is not compatible; use CC0-1.0 or CC-BY-4.0.");
            }
            if (licence == "CC-BY-4.0" && Str(p, "attribution") is null)
                add(pp, "provenance-licence", "CC-BY-4.0 sources need an 'attribution' text.");
            foreach (var field in new[] { "source", "url", "attribution", "notes" })
                if (Str(p, field) is { } text && text.Contains("openstreetmap", StringComparison.OrdinalIgnoreCase))
                    add($"{pp}/{field}", "provenance-forbidden", "OpenStreetMap data (ODbL) cannot be used in this registry.");
        }
    }

    private static void CheckExternal(JsonObject j, IReadOnlyDictionary<string, ExternalSystem> systems, Action<string, string, string> add)
    {
        var layouts = Arr(j, "layouts").Select(l => l!.AsObject())
            .GroupBy(l => Str(l, "id")!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);   // duplicate layouts are reported elsewhere
        var list = Arr(j, "external");
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i]!.AsObject();
            var ep = $"/external/{i}";
            var system = Str(e, "system")!;
            var eid = Str(e, "id")!;
            if (!systems.TryGetValue(system, out var s))
                add($"{ep}/system", "unknown-system", $"System '{system}' is not listed in schema/systems.json.");
            else if (!s.Pattern.IsMatch(eid))
                add($"{ep}/id", "unknown-system", $"'{eid}' is not a valid {system} id.");

            var layout = Str(e, "layout");
            var version = Str(e, "version");
            if (Str(e, "scope") == "circuit")
            {
                if (layout is not null || version is not null)
                    add(ep, "external-target", "An external id with scope 'circuit' must not name a layout or version.");
            }
            else if (layout is null || !layouts.TryGetValue(layout, out var l))
                add(ep, "external-target", $"Layout '{layout}' does not exist in this circuit.");
            else if (version is not null && !Arr(l, "versions").Any(v => Str(v!.AsObject(), "id") == version))
                add(ep, "external-target", $"Version '{version}' does not exist in layout '{layout}'.");
        }
    }

    /// <summary>Checks across files: ids and aliases, uids and external ids must be unique in the registry.</summary>
    public static void CheckRegistry(IReadOnlyList<CircuitFile> files, List<Finding> findings)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var uids = new Dictionary<string, string>(StringComparer.Ordinal);
        var externals = new Dictionary<(string, string), string>();
        foreach (var file in files)
        {
            var j = file.Json;
            void Add(string path, string code, string message) => findings.Add(new Finding(file.RelativePath, path, code, message));

            var names = new List<(string Path, string Id)> { ("/id", Str(j, "id")!) };
            var aliases = Arr(j, "aliases");
            for (int i = 0; i < aliases.Count; i++)
                names.Add(($"/aliases/{i}", aliases[i]!.GetValue<string>()));
            foreach (var (path, id) in names)
                if (!ids.TryAdd(id, file.RelativePath))
                    Add(path, "duplicate-id", $"'{id}' is already used by {ids[id]}.");

            var uidList = new List<(string Path, string Uid)> { ("/uid", Str(j, "uid")!) };
            var layouts = Arr(j, "layouts");
            for (int i = 0; i < layouts.Count; i++)
                uidList.Add(($"/layouts/{i}/uid", Str(layouts[i]!.AsObject(), "uid")!));
            foreach (var (path, uid) in uidList)
                if (!uids.TryAdd(uid, $"{file.RelativePath}:{path}"))
                    Add(path, "duplicate-uid", $"uid {uid} is already used by {uids[uid]}.");

            var external = Arr(j, "external");
            for (int i = 0; i < external.Count; i++)
            {
                var e = external[i]!.AsObject();
                var key = (Str(e, "system")!, Str(e, "id")!);
                if (!externals.TryAdd(key, file.RelativePath))
                    Add($"/external/{i}", "duplicate-external", $"{key.Item1} {key.Item2} is already used by {externals[key]}.");
            }
        }
    }

    internal static string? Str(JsonObject o, string name) => o[name]?.GetValue<string>();

    internal static JsonArray Arr(JsonObject o, string name) => o[name] as JsonArray ?? [];
}
