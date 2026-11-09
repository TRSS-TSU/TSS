using System;
using System.Linq;
using UnityEngine;

public sealed class TssScenarioCatalogResolver
{
    private readonly TssScenarioCatalog defaultCatalog;
    private readonly TssScenarioCatalog[] catalogs;

    public TssScenarioCatalogResolver(TssScenarioCatalog defaultCatalog)
    {
        this.defaultCatalog = defaultCatalog;
        catalogs = Resources.LoadAll<TssScenarioCatalog>(string.Empty);
    }

    public TssScenarioCatalog Resolve(string catalogId)
    {
        var requested = string.IsNullOrWhiteSpace(catalogId)
            ? TssExternalScenarioData.DefaultCatalogId
            : catalogId.Trim();

        if (defaultCatalog && string.Equals(defaultCatalog.catalogId, requested, StringComparison.OrdinalIgnoreCase))
            return defaultCatalog;

        return catalogs.FirstOrDefault(candidate => candidate && string.Equals(candidate.catalogId, requested, StringComparison.OrdinalIgnoreCase));
    }
}
