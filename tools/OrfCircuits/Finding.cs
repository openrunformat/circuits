namespace OrfCircuits;

/// <summary>One problem found in the registry.</summary>
/// <param name="File">Path of the file, relative to the registry root, with '/' separators.</param>
/// <param name="Path">JSON pointer inside the file ("" for the whole file).</param>
/// <param name="Code">Stable short code, e.g. "schema" or "gate-width".</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record Finding(string File, string Path, string Code, string Message)
{
    public override string ToString() => $"{File}:{Path}: {Code}: {Message}";
}
