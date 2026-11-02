using System.Text.Json.Nodes;

namespace OrfCircuits.Tests;

public class ReviewFixTests
{
    [Fact]
    public void Duplicate_key_is_a_json_finding()
    {
        var root = TestRegistry.Create(null, ("circuits/ES/dup-ring.json", "{\"name\":\"A\",\"name\":\"B\"}"));
        var findings = RegistryValidator.Validate(root);
        Assert.Contains(findings, f => f.Code == "json" && f.File == "circuits/ES/dup-ring.json");
    }

    [Theory]
    [InlineData("{ \"systems\": [")]
    [InlineData("{ \"systems\": [ { \"id\": \"wikidata\", \"name\": \"Wikidata\", \"pattern\": \"(\" } ] }")]
    [InlineData("{ \"systems\": [ { \"id\": \"a\", \"name\": \"A\", \"pattern\": \"x\" }, { \"id\": \"a\", \"name\": \"A\", \"pattern\": \"x\" } ] }")]
    public void Broken_systems_file_is_a_finding(string content)
    {
        var root = TestRegistry.Create(null, ("schema/systems.json", content));
        var findings = RegistryValidator.Validate(root);
        Assert.Contains(findings, f => f.File == "schema/systems.json" && f.Code == "systems");
    }

    [Fact]
    public void Version_id_with_trailing_newline_is_rejected_without_crashing()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(j => j["layouts"]![0]!["versions"]![0]!["id"] = "2019\n"));
        Assert.Contains(findings, f => f.File == TestRegistry.FixtureFile && f.Path == "/layouts/0/versions/0/id");
    }

    [Fact]
    public void Uid_with_trailing_newline_is_rejected()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(j =>
            j["layouts"]![1]!["uid"] = j["layouts"]![0]!["uid"]!.GetValue<string>() + "\n"));
        Assert.Contains(findings, f => f.File == TestRegistry.FixtureFile && f.Path == "/layouts/1/uid");
    }

    [Fact]
    public void Validate_of_missing_root_fails()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Cli.Run(["validate", Path.Combine(Path.GetTempPath(), "orf-circuits-tests", "does-not-exist")], output, error);
        Assert.Equal(2, code);
        Assert.DoesNotContain("OK", output.ToString());
    }

    [Fact]
    public void Validate_of_existing_root_succeeds()
    {
        var output = new StringWriter();
        Assert.Equal(0, Cli.Run(["validate", TestRegistry.Create()], output, new StringWriter()));
        Assert.Contains("OK: 1 circuits", output.ToString());
    }
}
