using OrfCircuits;

return Cli.Run(args, Console.Out, Console.Error);

namespace OrfCircuits
{
    internal static class Cli
    {
        private const string Usage = "usage: orf-circuits validate [<root>] | uid";

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
                case "uid" when args.Length == 1:
                    output.WriteLine(Guid.CreateVersion7().ToString("D"));
                    return 0;
                default:
                    error.WriteLine(Usage);
                    return 2;
            }
        }
    }
}
