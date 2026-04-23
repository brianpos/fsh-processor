using YamlDotNet.Serialization;

namespace Hl7.FhirShorthand.Compiler_tester_r4;


/// <summary>
/// Documentation can be found here https://fshschool.org/docs/sushi/configuration
/// </summary>
public class SushiYaml
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "canonical")]
    public string Canonical { get; set; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; set; } = string.Empty;

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "status")]
    public string Status { get; set; } = string.Empty;

    [YamlMember(Alias = "publisher")]
    public SushiYamlPublisher Publisher { get; set; } = new();

    [YamlMember(Alias = "contact")]
    public SushiYamlContact Contact { get; set; } = new();

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "license")]
    public string License { get; set; } = string.Empty;

    [YamlMember(Alias = "fhirVersion")]
    public string FhirVersion { get; set; } = string.Empty;

    [YamlMember(Alias = "instanceOptions")]
    public SushiYamlInstanceOptions InstanceOptions { get; set; } = new();

    [YamlMember(Alias = "parameters")]
    public SushiYamlParameters Parameters { get; set; } = new();

    [YamlMember(Alias = "copyrightYear")]
    public string CopyrightYear { get; set; } = string.Empty;

    [YamlMember(Alias = "releaseLabel")]
    public string ReleaseLabel { get; set; } = string.Empty;

    [YamlMember(Alias = "jurisdiction")]
    public string Jurisdiction { get; set; } = string.Empty;

    [YamlMember(Alias = "extension")]
    public List<SushiYamlExtension> Extension { get; set; } = [];

    [YamlMember(Alias = "dependencies")]
    public Dictionary<string, SushiYamlDependency> Dependencies { get; set; } = [];

    [YamlMember(Alias = "pages")]
    public Dictionary<string, Dictionary<string, object>> Pages { get; set; } = [];

    [YamlMember(Alias = "resources")]
    public Dictionary<string, SushiYamlResource> Resources { get; set; } = [];
}

public class SushiYamlPublisher
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "url")]
    public string Url { get; set; } = string.Empty;
}

public class SushiYamlContact
{
    [YamlMember(Alias = "telecom")]
    public List<SushiYamlTelecom> Telecom { get; set; } = [];
}

public class SushiYamlTelecom
{
    [YamlMember(Alias = "system")]
    public string System { get; set; } = string.Empty;

    [YamlMember(Alias = "value")]
    public string Value { get; set; } = string.Empty;
}

public class SushiYamlInstanceOptions
{
    [YamlMember(Alias = "manualSliceOrdering")]
    public bool ManualSliceOrdering { get; set; }
}

public class SushiYamlParameters
{
    [YamlMember(Alias = "auto-oid-root")]
    public string AutoOidRoot { get; set; } = string.Empty;

    [YamlMember(Alias = "apply-publisher")]
    public bool ApplyPublisher { get; set; }

    [YamlMember(Alias = "apply-contact")]
    public bool ApplyContact { get; set; }

    [YamlMember(Alias = "globals-in-artifacts")]
    public bool GlobalsInArtifacts { get; set; }

    [YamlMember(Alias = "pin-canonicals")]
    public string PinCanonicals { get; set; } = string.Empty;

    [YamlMember(Alias = "show-inherited-invariants")]
    public bool ShowInheritedInvariants { get; set; }

    [YamlMember(Alias = "shownav")]
    public bool ShowNav { get; set; }

    [YamlMember(Alias = "validation")]
    public string Validation { get; set; } = string.Empty;

    [YamlMember(Alias = "version-comparison")]
    public List<string> VersionComparison { get; set; } = [];
}

public class SushiYamlExtension
{
    [YamlMember(Alias = "url")]
    public string Url { get; set; } = string.Empty;

    [YamlMember(Alias = "valueCode")]
    public string ValueCode { get; set; } = string.Empty;

    [YamlMember(Alias = "valueInteger")]
    public string ValueInteger { get; set; } = string.Empty;
}

public class SushiYamlDependency
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "uri")]
    public string Uri { get; set; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; set; } = string.Empty;

    [YamlMember(Alias = "reason")]
    public string Reason { get; set; } = string.Empty;
}

public class SushiYamlResource
{
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "exampleCanonical")]
    public string ExampleCanonical { get; set; } = string.Empty;
}
