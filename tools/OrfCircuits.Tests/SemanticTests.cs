using System.Text.Json.Nodes;

namespace OrfCircuits.Tests;

public class SemanticTests
{
    private static IReadOnlyList<Finding> Validate(Action<JsonObject>? mutate = null, params (string path, string content)[] extra) =>
        RegistryValidator.Validate(TestRegistry.Create(mutate, extra));

    private static JsonObject Layout(JsonObject j, int i) => j["layouts"]![i]!.AsObject();
    private static JsonObject Version(JsonObject j, int layout, int version) => Layout(j, layout)["versions"]![version]!.AsObject();
    private static JsonObject Provenance(JsonObject j, int i) => j["provenance"]![i]!.AsObject();

    /// <summary>A second, valid circuit (ES-second-ring) with its own uids and no external ids.</summary>
    private static JsonObject SecondRing(Action<JsonObject>? mutate = null)
    {
        var j = TestRegistry.LoadFixture();
        j["id"] = "ES-second-ring";
        j["uid"] = "01a19c89-ee4a-7000-8000-000000000101";
        Layout(j, 0)["uid"] = "01a19c89-ee4a-7000-8000-000000000102";
        Layout(j, 1)["uid"] = "01a19c89-ee4a-7000-8000-000000000103";
        j.Remove("external");
        mutate?.Invoke(j);
        return j;
    }

    private static (string, string) SecondRingFile(Action<JsonObject>? mutate = null) =>
        ("circuits/ES/second-ring.json", TestRegistry.Serialize(SecondRing(mutate)));

    [Fact]
    public void Fixture_registry_is_valid() => Assert.Empty(Validate());

    [Fact]
    public void Fixture_with_a_second_circuit_is_valid() => Assert.Empty(Validate(null, SecondRingFile()));

    [Fact]
    public void Alias_equal_to_another_circuit_id_is_a_duplicate()
    {
        var findings = Validate(null, SecondRingFile(j => j["aliases"] = new JsonArray("ES-synthetic-ring")));
        Assert.Contains(findings, f => f.Code == "duplicate-id");
    }

    [Fact]
    public void Alias_equal_to_own_id_is_a_duplicate()
    {
        var findings = Validate(j => j["aliases"] = new JsonArray("ES-synthetic-ring"));
        Assert.Contains(findings, f => f.Code == "duplicate-id");
    }

    [Fact]
    public void Id_with_double_hyphen_is_rejected()
    {
        var root = TestRegistry.Create();
        var j = TestRegistry.LoadFixture();
        j["id"] = "ES-synthetic--ring";
        File.Delete(Path.Combine(root, TestRegistry.FixtureFile));
        File.WriteAllText(Path.Combine(root, "circuits/ES/synthetic--ring.json"), TestRegistry.Serialize(j));
        var findings = RegistryValidator.Validate(root);
        Assert.Contains(findings, f => f.Code == "id-syntax");
        Assert.DoesNotContain(findings, f => f.Code == "file-name");
    }

    [Fact]
    public void Uid_reused_by_another_circuit_is_a_duplicate()
    {
        var findings = Validate(null, SecondRingFile(j => j["uid"] = "01a19c89-ee4a-7051-a1b6-8b847d5e04bf"));
        Assert.Contains(findings, f => f.Code == "duplicate-uid");
    }

    [Fact]
    public void Uid_reused_by_a_layout_is_a_duplicate()
    {
        var findings = Validate(j => Layout(j, 1)["uid"] = j["uid"]!.GetValue<string>());
        Assert.Contains(findings, f => f.Code == "duplicate-uid");
    }

    [Fact]
    public void Uid_that_is_not_version_7_is_rejected()
    {
        var findings = Validate(j => Layout(j, 0)["uid"] = "0192f0c4-8b7a-4c3e-9a1d-3f5e6b7c8d9e");
        Assert.Contains(findings, f => f.Code == "uid-version" && f.Path == "/layouts/0/uid");
    }

    [Fact]
    public void Repeated_layout_id_is_rejected()
    {
        var findings = Validate(j => Layout(j, 1)["id"] = "full");
        Assert.Contains(findings, f => f.Code == "duplicate-layout");
    }

    [Fact]
    public void Repeated_version_id_is_rejected()
    {
        var findings = Validate(j => { Version(j, 0, 1)["id"] = "2019"; Version(j, 0, 1)["from"] = "2019-06-01"; Version(j, 0, 0)["to"] = "2019-06-01"; });
        Assert.Contains(findings, f => f.Code == "duplicate-version");
    }

    [Fact]
    public void Overlapping_versions_are_rejected()
    {
        var findings = Validate(j => Version(j, 0, 0)["to"] = "2023-06-01");
        Assert.Contains(findings, f => f.Code == "version-dates");
    }

    [Fact]
    public void Open_version_followed_by_later_version_overlaps()
    {
        var findings = Validate(j => Version(j, 0, 0).Remove("to"));
        Assert.Contains(findings, f => f.Code == "version-dates");
    }

    [Fact]
    public void Several_versions_without_from_are_rejected()
    {
        var findings = Validate(j => Version(j, 0, 0).Remove("from"));
        Assert.Contains(findings, f => f.Code == "version-dates");
    }

    [Fact]
    public void Several_versions_without_id_are_rejected()
    {
        var findings = Validate(j => Version(j, 0, 1).Remove("id"));
        Assert.Contains(findings, f => f.Code == "version-dates");
    }

    [Fact]
    public void End_before_start_is_rejected()
    {
        var findings = Validate(j => Version(j, 0, 0)["to"] = "2019-01-01");
        Assert.Contains(findings, f => f.Code == "version-dates");
    }

    [Fact]
    public void Impossible_date_is_rejected()
    {
        var findings = Validate(j => Version(j, 0, 0)["to"] = "2023-02-30");
        Assert.Contains(findings, f => f.Code == "version-dates");
    }

    [Fact]
    public void Version_id_must_match_from()
    {
        var findings = Validate(j => Version(j, 0, 1)["id"] = "2022");
        Assert.Contains(findings, f => f.Code == "version-id");
    }

    [Fact]
    public void Version_id_month_must_match_from()
    {
        var findings = Validate(j => Version(j, 0, 1)["id"] = "2023-05");
        Assert.Contains(findings, f => f.Code == "version-id");
    }

    [Fact]
    public void Unknown_external_system_is_rejected()
    {
        var findings = Validate(j => j["external"]![0]!["system"] = "acme");
        Assert.Contains(findings, f => f.Code == "unknown-system");
    }

    [Fact]
    public void Wikidata_id_must_match_pattern()
    {
        var findings = Validate(j => j["external"]![0]!["id"] = "12345");
        Assert.Contains(findings, f => f.Code == "unknown-system");
    }

    [Fact]
    public void External_pointing_to_two_circuits_is_rejected()
    {
        var findings = Validate(null, SecondRingFile(j =>
            j["external"] = new JsonArray(new JsonObject { ["system"] = "wikidata", ["id"] = "Q999999999", ["scope"] = "circuit" })));
        Assert.Contains(findings, f => f.Code == "duplicate-external");
    }

    [Fact]
    public void External_to_missing_layout_is_rejected()
    {
        var findings = Validate(j =>
            j["external"] = new JsonArray(new JsonObject { ["system"] = "wikidata", ["id"] = "Q999999999", ["scope"] = "layout", ["layout"] = "nope" }));
        Assert.Contains(findings, f => f.Code == "external-target");
    }

    [Fact]
    public void External_to_missing_version_is_rejected()
    {
        var findings = Validate(j =>
            j["external"] = new JsonArray(new JsonObject { ["system"] = "wikidata", ["id"] = "Q999999999", ["scope"] = "layout", ["layout"] = "full", ["version"] = "2020" }));
        Assert.Contains(findings, f => f.Code == "external-target");
    }

    [Fact]
    public void External_with_layout_scope_and_existing_version_is_valid()
    {
        var findings = Validate(j =>
            j["external"] = new JsonArray(new JsonObject { ["system"] = "wikidata", ["id"] = "Q999999999", ["scope"] = "layout", ["layout"] = "full", ["version"] = "2019" }));
        Assert.Empty(findings);
    }

    [Fact]
    public void External_with_circuit_scope_must_not_name_a_layout()
    {
        var findings = Validate(j => j["external"]![0]!["layout"] = "full");
        Assert.Contains(findings, f => f.Code == "external-target");
    }

    [Theory]
    [InlineData(-2.99996)]
    [InlineData(-2.9988)]
    [InlineData(-3.0)]
    public void Gate_width_out_of_range_is_rejected(double lon2)
    {
        var findings = Validate(j => Version(j, 0, 0)["start"]!["line"] = new JsonArray(40.0, -3.0, 40.0, lon2));
        Assert.Contains(findings, f => f.Code == "gate-width" && f.Path == "/layouts/0/versions/0/start/line");
    }

    [Fact]
    public void Swapped_lat_lon_is_reported()
    {
        var findings = Validate(j => Version(j, 0, 0)["finish"]!["line"] = new JsonArray(-3.0, 40.0, -3.0, 40.00014));
        Assert.Contains(findings, f => f.Code == "gate-location" && f.Path == "/layouts/0/versions/0/finish/line");
    }

    [Fact]
    public void Split_far_away_is_reported()
    {
        var findings = Validate(j => Version(j, 0, 1)["splits"]![0]!["line"] = new JsonArray(41.0, -3.0, 41.0, -2.99986));
        Assert.Contains(findings, f => f.Code == "gate-location" && f.Path == "/layouts/0/versions/1/splits/0/line");
    }

    [Fact]
    public void Gate_with_unknown_provenance_is_rejected()
    {
        var findings = Validate(j => Version(j, 0, 0)["start"]!["provenance"] = "p9");
        Assert.Contains(findings, f => f.Code == "provenance-missing");
    }

    [Fact]
    public void Gate_from_official_source_is_rejected()
    {
        var findings = Validate(j => Provenance(j, 0)["method"] = "official");
        Assert.Contains(findings, f => f.Code == "provenance-method");
    }

    [Theory]
    [InlineData("no-source")]
    [InlineData("no-licence")]
    [InlineData("nc-licence")]
    [InlineData("no-attribution")]
    public void Orthophoto_needs_source_and_compatible_licence(string mutation)
    {
        var findings = Validate(j =>
        {
            var p = Provenance(j, 1);
            switch (mutation)
            {
                case "no-source": p.Remove("source"); break;
                case "no-licence": p.Remove("licence"); break;
                case "nc-licence": p["licence"] = "CC-BY-NC-SA-4.0"; break;
                case "no-attribution": p.Remove("attribution"); break;
            }
        });
        Assert.Contains(findings, f => f.Code == "provenance-licence");
    }

    [Theory]
    [InlineData("source")]
    [InlineData("url")]
    [InlineData("attribution")]
    [InlineData("notes")]
    [InlineData("licence")]
    public void OpenStreetMap_data_is_rejected(string field)
    {
        var findings = Validate(j =>
            Provenance(j, 1)[field] = field == "licence" ? "ODbL-1.0" : "Traced from OpenStreetMap contributors");
        Assert.Contains(findings, f => f.Code == "provenance-forbidden");
    }

    [Fact]
    public void Distance_of_one_thousandth_degree_of_latitude_is_about_111_m()
    {
        var d = Geo.DistanceM(40, -3, 40.001, -3);
        Assert.InRange(d, 111.0, 111.4);
    }

    [Fact]
    public void Fixture_gate_is_about_12_m_wide()
    {
        Assert.InRange(Geo.DistanceM(40.0, -3.0, 40.0, -2.99986), 11.5, 12.5);
    }
}
