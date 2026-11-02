using OrfCircuits;

return Cli.Run(args, Console.Out, Console.Error);

namespace OrfCircuits
{
    internal static class Cli
    {
        private const string Usage = "usage: orf-circuits validate [<root>] | index [<root>] --generated-at <ISO-8601> [--out <path>] | uid";

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args.Length == 0)
            {
                error.WriteLine(Usage);
                return 2;
            }
            switch (args[0])
            {
                case "validate" when args.Length <= 2:
                {
                    var root = args.Length == 2 ? args[1] : ".";
                    var findings = RegistryValidator.Validate(root);
                    foreach (var f in findings)
                        output.WriteLine(f);
                    if (findings.Count > 0)
                        return 1;
                    output.WriteLine($"OK: {RegistryLoader.Load(root, []).Count} circuits");
                    return 0;
                }
                case "index":
                    return Index(args[1..], output, error);
                case "uid" when args.Length == 1:
                    output.WriteLine(Guid.CreateVersion7().ToString("D"));
                    return 0;
                default:
                    error.WriteLine(Usage);
                    return 2;
            }
        }

        private static int Index(string[] args, TextWriter output, TextWriter error)
        {
            string root = ".";
            string? outPath = null;
            DateTimeOffset? generatedAt = null;
            var rootSet = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--generated-at" when i + 1 < args.Length:
                        if (!DateTimeOffset.TryParse(args[++i], System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.AssumeUniversal, out var at))
                        {
                            error.WriteLine($"Not an ISO-8601 date and time: '{args[i]}'.");
                            return 2;
                        }
                        generatedAt = at;
                        break;
                    case "--out" when i + 1 < args.Length:
                        outPath = args[++i];
                        break;
                    default:
                        if (rootSet || args[i].StartsWith("--", StringComparison.Ordinal))
                        {
                            error.WriteLine(Usage);
                            return 2;
                        }
                        root = args[i];
                        rootSet = true;
                        break;
                }
            }
            if (generatedAt is null)
            {
                error.WriteLine(Usage);
                return 2;
            }

            var findings = RegistryValidator.Validate(root);
            if (findings.Count > 0)
            {
                foreach (var f in findings)
                    output.WriteLine(f);
                return 1;
            }

            outPath ??= Path.Combine(root, "dist", "index.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
            File.WriteAllText(outPath, IndexBuilder.Build(root, generatedAt.Value), new System.Text.UTF8Encoding(false));
            output.WriteLine($"Wrote {outPath}");
            return 0;
        }
    }
}
