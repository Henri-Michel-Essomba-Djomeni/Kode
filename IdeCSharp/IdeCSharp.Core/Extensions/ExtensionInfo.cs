namespace IdeCSharp.Core.Extensions;

/// <summary>Une extension du catalogue. Pour l'instant : un thème de couleurs.</summary>
public record ExtensionInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "1.0.0";

    /// <summary>Vrai si le thème est sombre (règle le mode des contrôles standard).</summary>
    public bool Dark { get; init; } = true;

    /// <summary>
    /// Nom de la couleur de Kode → "#RRGGBB", ou "#RRGGBB,#RRGGBB" pour un dégradé.
    /// Ex. : "AccentBrush": "#2DD4BF"
    /// </summary>
    public Dictionary<string, string> Colors { get; init; } = new();
}

/// <summary>Le catalogue chargé, et d'où il vient (réseau ou copie intégrée).</summary>
public record CatalogResult(IReadOnlyList<ExtensionInfo> Extensions, bool FromNetwork);