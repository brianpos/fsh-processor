using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;

namespace Hl7.FhirShorthand.Compiler;

public class DependencyNameResolver : IAsyncResourceResolver, IResourceResolver
{
    /// <summary>
    /// Index of resource details by name. Primary usecase is to resolve dependencies by name, which is not supported by the canonical URI resolver. This is used for resources that don't have a canonical URI, but can be referenced by name (e.g. InstanceDefinitions).
    /// </summary>
    Dictionary<string, ResourceSummaryExtendedDetails> _resourcesByName = new Dictionary<string, ResourceSummaryExtendedDetails>();

    ModelInspector _inspector;

    /// <summary>
    /// Reads FHIR artifacts (Profiles, ValueSets, CodeSystems etc.) from memory.
    /// </summary>
    /// <param name="resources">Resources to be loaded in memory.</param>
    public DependencyNameResolver(ModelInspector inspector)
    {
        _inspector = inspector;
    }

    public void AppendDetails(IEnumerable<ResourceSummaryDetails> details, string path)
    {
        foreach (var detail in details)
        {
            var pd = new ResourceSummaryExtendedDetails(detail, path);
            // detail.PackagePath = path;
            if (!string.IsNullOrEmpty(detail.Name))
                _resourcesByName.TryAdd(detail.Name, pd);
            if (!string.IsNullOrEmpty(detail.Url))
                _resourcesByName.TryAdd(detail.Url, pd);
        }
    }

    private Resource? ResolveResource(ResourceSummaryExtendedDetails details)
    {
        if (details.Resource == null)
        {
            // deserialize the resource if it hasn't been deserialized yet
            if (details.FileName.EndsWith(".xml"))
            {
                BaseFhirXmlPocoDeserializer serializer = new(_inspector);
                var xml = File.ReadAllText(Path.Combine(details.PackagePath, details.FileName));
                details.Resource = serializer.DeserializeResource(xml);
            }
            else if (details.FileName.EndsWith(".json"))
            {
                BaseFhirJsonPocoDeserializer serializer = new(_inspector);
                var xml = File.ReadAllText(Path.Combine(details.PackagePath, details.FileName));
                details.Resource = serializer.DeserializeResource(xml);
            }
        }
        return details.Resource;
    }

    ///<inheritdoc/>
    public Resource? ResolveByCanonicalUri(string uri)
    {
        // Lookup the URL in the index
        if (_resourcesByName.ContainsKey(uri))
        {
            var details = _resourcesByName[uri];
            return ResolveResource(details);
        }
        return null;
    }

    ///<inheritdoc/>
    public Task<Resource?> ResolveByCanonicalUriAsync(string uri)
    {
        return Task.FromResult(ResolveByCanonicalUri(uri));
    }

    ///<inheritdoc/>
    public Resource? ResolveByUri(string uri)
    {
        // Lookup the URL in the index
        if (_resourcesByName.ContainsKey(uri))
        {
            var details = _resourcesByName[uri];
            return ResolveResource(details);
        }
        return null;
    }

    ///<inheritdoc/>
    public Task<Resource?> ResolveByUriAsync(string uri)
    {
        return Task.FromResult(ResolveByUri(uri));
    }
}

public class ResourceSummaryExtendedDetails : ResourceSummaryDetails
{
    public ResourceSummaryExtendedDetails(ResourceSummaryDetails other, string packagePath)
    {
        PackagePath = packagePath;

        FileName = other.FileName;
        ResourceType = other.ResourceType;
        Id = other.Id;
        Name = other.Name;
        Url = other.Url;
        Version = other.Version;
    }

    public string PackagePath { get; set; }

    public Hl7.Fhir.Model.Resource? Resource { get; set; }
}
