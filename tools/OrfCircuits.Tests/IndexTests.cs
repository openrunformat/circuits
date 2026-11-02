using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace OrfCircuits.Tests;

public class IndexTests
{
    private static readonly DateTimeOffset GeneratedAt = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    private static string Golden => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "expected-index.json"));

    [Fact]
    public void Index_of_fixture_matches_golden_file() =>
        Assert.Equal(Golden, IndexBuilder.Build(TestRegistry.Create(), GeneratedAt));

    [Fact]
    public void Index_validates_against_index_schema()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "schema", "index.schema.json")));
        using var doc = JsonDocument.Parse(IndexBuilder.Build(TestRegistry.Create(), GeneratedAt));
        Assert.True(schema.Evaluate(doc.RootElement).IsValid);
    }

    [Fact]
    public void Building_twice_gives_identical_bytes()
    {
        var root = TestRegistry.Create();
        Assert.Equal(IndexBuilder.Build(root, GeneratedAt), IndexBuilder.Build(root, GeneratedAt));
    }

    [Fact]
    public void Crlf_source_files_give_the_same_index()
    {
        var root = TestRegistry.Create();
        var path = Path.Combine(root, TestRegistry.FixtureFile);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        Assert.Contains("\r\n", File.ReadAllText(path));
        Assert.Equal(Golden, IndexBuilder.Build(root, GeneratedAt));
    }

    [Fact]
    public void Index_uses_lf_and_ends_with_newline()
    {
        var index = IndexBuilder.Build(TestRegistry.Create(), GeneratedAt);
        Assert.DoesNotContain("\r", index);
        Assert.EndsWith("}\n", index);
    }

    [Fact]
    public void Index_contains_no_provenance_details()
    {
        var index = IndexBuilder.Build(TestRegistry.Create(), GeneratedAt);
        Assert.DoesNotContain("contributor", index);
        Assert.DoesNotContain("provenance", index);
    }

    [Fact]
    public void Attribution_lists_only_sources_cited_by_gates()
    {
        // p2 (the only provenance with an attribution) is no longer cited by any gate.
        var root = TestRegistry.Create(j =>
        {
            foreach (var layout in j["layouts"]!.AsArray())
            foreach (var version in layout!["versions"]!.AsArray())
            {
                version!["start"]!["provenance"] = "p1";
                version["finish"]!["provenance"] = "p1";
                foreach (var split in version["splits"]?.AsArray() ?? [])
                    split!["provenance"] = "p1";
            }
        });
        var circuit = JsonNode.Parse(IndexBuilder.Build(root, GeneratedAt))!["circuits"]![0]!;
        Assert.Empty(circuit["attribution"]!.AsArray());
    }

    [Fact]
    public void Generated_at_is_written_in_utc_without_fraction()
    {
        var index = IndexBuilder.Build(TestRegistry.Create(), new DateTimeOffset(2026, 11, 2, 11, 0, 0, 123, TimeSpan.FromHours(1)));
        Assert.Contains("\"generatedAt\": \"2026-11-02T10:00:00Z\"", index);
    }

    [Fact]
    public void Invalid_registry_is_not_indexed()
    {
        var root = TestRegistry.Create(j => j["layouts"]![0]!["versions"]![0]!["start"]!["line"] = new JsonArray(40.0, -3.0, 40.0, -3.0));
        Assert.Throws<InvalidOperationException>(() => IndexBuilder.Build(root, GeneratedAt));
    }

    [Fact]
    public void Empty_registry_gives_empty_circuit_list()
    {
        var index = JsonNode.Parse(IndexBuilder.Build(TestRegistry.CreateEmpty(), GeneratedAt))!;
        Assert.Empty(index["circuits"]!.AsArray());
    }

    [Fact]
    public void Circuits_are_sorted_by_id()
    {
        var other = TestRegistry.LoadFixture();
        other["id"] = "ES-a-ring";
        other["uid"] = "01a19c89-ee4a-7000-8000-000000000201";
        other["layouts"]![0]!["uid"] = "01a19c89-ee4a-7000-8000-000000000202";
        other["layouts"]![1]!["uid"] = "01a19c89-ee4a-7000-8000-000000000203";
        other.Remove("external");
        var root = TestRegistry.Create(null, ("circuits/ES/a-ring.json", TestRegistry.Serialize(other)));
        var ids = JsonNode.Parse(IndexBuilder.Build(root, GeneratedAt))!["circuits"]!.AsArray().Select(c => c!["id"]!.GetValue<string>());
        Assert.Equal(["ES-a-ring", "ES-synthetic-ring"], ids);
    }
}
