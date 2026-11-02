namespace OrfCircuits;

/// <summary>ISO 3166-1 alpha-2 codes currently assigned (embedded list, one code per line).</summary>
public static class CountryCodes
{
    public static IReadOnlySet<string> Assigned { get; } = Load();

    private static HashSet<string> Load()
    {
        using var stream = typeof(CountryCodes).Assembly.GetManifestResourceStream("iso3166-alpha2.txt")
            ?? throw new InvalidOperationException("Embedded country list is missing.");
        using var reader = new StreamReader(stream);
        var set = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
            if (line.Trim() is { Length: > 0 } code)
                set.Add(code);
        return set;
    }
}
