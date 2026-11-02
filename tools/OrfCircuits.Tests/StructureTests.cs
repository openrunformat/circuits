using System.Reflection;
using System.Text;

namespace OrfCircuits.Tests;

public class StructureTests
{
    private const string Fixture = TestRegistry.FixtureFile;

    [Fact]
    public void Fixture_registry_has_no_structural_findings() =>
        Assert.Empty(RegistryValidator.Validate(TestRegistry.Create()));

    [Fact]
    public void Empty_registry_is_valid() =>
        Assert.Empty(RegistryValidator.Validate(TestRegistry.CreateEmpty()));

    [Fact]
    public void Missing_required_field_is_a_schema_finding()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(j => j.Remove("name")));
        Assert.Contains(findings, f => f.Code == "schema" && f.File == Fixture);
    }

    [Fact]
    public void Unknown_property_is_a_schema_finding()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(j => j["colour"] = "red"));
        Assert.Contains(findings, f => f.Code == "schema" && f.File == Fixture);
    }

    [Fact]
    public void File_name_must_match_id()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(j => j["id"] = "ES-other-ring"));
        Assert.Contains(findings, f => f.Code == "file-name" && f.File == Fixture);
    }

    [Fact]
    public void Country_must_match_id_and_folder()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(j => j["country"] = "PT"));
        Assert.Contains(findings, f => f.Code == "country" && f.File == Fixture);
    }

    [Fact]
    public void Unassigned_country_code_is_reported()
    {
        var other = TestRegistry.LoadFixture();
        other["id"] = "ZZ-x-ring";
        other["country"] = "ZZ";
        other["uid"] = "01a19c89-ee4a-7000-8000-000000000001";
        var root = TestRegistry.Create(null, ("circuits/ZZ/x-ring.json", TestRegistry.Serialize(other)));
        var findings = RegistryValidator.Validate(root);
        Assert.Contains(findings, f => f.Code == "country" && f.File == "circuits/ZZ/x-ring.json");
    }

    [Fact]
    public void Stray_file_is_reported()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(null, ("circuits/ES/notes.txt", "notes")));
        Assert.Contains(findings, f => f.Code == "stray-file" && f.File == "circuits/ES/notes.txt");
    }

    [Fact]
    public void Stray_file_at_circuits_root_is_reported()
    {
        var findings = RegistryValidator.Validate(TestRegistry.Create(null, ("circuits/ring.json", "{}")));
        Assert.Contains(findings, f => f.Code == "stray-file" && f.File == "circuits/ring.json");
    }

    public static TheoryData<string> BrokenJson => new()
    {
        "{",
        "{\"id\":1,}",
        "",
        "﻿" + File.ReadAllText(Path.Combine(TestRegistry.FixtureRoot, TestRegistry.FixtureFile)),
        "[1, 2]",
    };

    [Theory]
    [MemberData(nameof(BrokenJson))]
    public void Broken_json_is_reported_without_crashing(string content)
    {
        // The broken file sits next to a second circuit with a schema error, which must still be validated.
        var other = TestRegistry.LoadFixture();
        other["id"] = "PT-other-ring";
        other["country"] = "PT";
        other.Remove("name");
        var root = TestRegistry.Create(null,
            ("circuits/ES/broken-ring.json", ""),
            ("circuits/PT/other-ring.json", TestRegistry.Serialize(other)));
        File.WriteAllBytes(Path.Combine(root, "circuits/ES/broken-ring.json"), new UTF8Encoding(false).GetBytes(content));

        var findings = RegistryValidator.Validate(root);

        var json = Assert.Single(findings, f => f.Code == "json");
        Assert.Equal("circuits/ES/broken-ring.json", json.File);
        Assert.Contains(findings, f => f.Code == "schema" && f.File == "circuits/PT/other-ring.json");
        Assert.DoesNotContain(findings, f => f.File == Fixture);
    }

    [Fact]
    public void Iso_list_has_249_assigned_codes()
    {
        var codes = CountryCodes.Assigned;
        Assert.Equal(249, codes.Count);
        Assert.Contains("ES", codes);
        Assert.Contains("GB", codes);
        Assert.Contains("US", codes);
        Assert.DoesNotContain("ZZ", codes);
        Assert.DoesNotContain("UK", codes);
        Assert.DoesNotContain("EU", codes);
    }

    [Fact]
    public void Findings_are_sorted_by_file_path_and_code()
    {
        var root = TestRegistry.Create(j => { j.Remove("name"); j["country"] = "PT"; }, ("circuits/ES/a.txt", "x"));
        var findings = RegistryValidator.Validate(root);
        var sorted = findings.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Path, StringComparer.Ordinal)
            .ThenBy(f => f.Code, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, findings);
    }
}
