using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;

namespace fsh_compiler;

public class AliasResolver : IAsyncResourceResolver, IResourceResolver
{
    Dictionary<string, StructureDefinition> _resources;

    /// <summary>
    /// Reads FHIR artifacts (Profiles, ValueSets, CodeSystems etc.) from memory.
    /// </summary>
    /// <param name="resources">Resources to be loaded in memory.</param>
    public AliasResolver(Dictionary<string, StructureDefinition> CompiledStructureDefinitions)
    {
        _resources = CompiledStructureDefinitions;
    }

    ///<inheritdoc/>
    public Resource? ResolveByCanonicalUri(string uri)
    {
        var canonical = new Canonical(uri);
        var canonicalUrl = canonical.Uri;
        var version = canonical.Version ?? string.Empty;

        // Filter by canonical URL first
        var candidateResources = _resources.Where(r => r.Key == canonicalUrl).ToList();

        if (!candidateResources.Any())
            return null;

        // If no version specified, return the first match
        if (string.IsNullOrEmpty(version))
        {
            var firstCandidate = candidateResources.FirstOrDefault();
            return firstCandidate.Value;
        }

        // Look for exact version match or partial version match
        foreach (var candidate in candidateResources)
        {
            if (candidate.Value is IVersionableConformanceResource versionableConformance)
            {
                if (Canonical.MatchesVersion(versionableConformance.Version, version))
                    return candidate.Value;
            }
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
        return _resources.Where(r => r.Key == uri)?.Select(r => r.Value).FirstOrDefault();
    }

    ///<inheritdoc/>
    public Task<Resource?> ResolveByUriAsync(string uri)
    {
        return Task.FromResult(ResolveByUri(uri));
    }
}
