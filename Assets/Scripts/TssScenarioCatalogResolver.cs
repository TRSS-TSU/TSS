using System;

public sealed class TssScenarioCatalogResolver
{
    private readonly TssScenarioCatalog defaultCatalog;

    public TssScenarioCatalogResolver(TssScenarioCatalog defaultCatalog)
    {
        this.defaultCatalog = defaultCatalog;
    }

    public TssScenarioCatalog Resolve(string catalogId)
    {
        var requested = string.IsNullOrWhiteSpace(catalogId)
            ? TssExternalScenarioData.DefaultCatalogId
            : catalogId.Trim();

        if (defaultCatalog && string.Equals(defaultCatalog.catalogId, requested, StringComparison.OrdinalIgnoreCase))
            return defaultCatalog;

        return null;
    }
}
