using System.Text.Json;

namespace IdeCSharp.Core.Extensions;

/// <summary>Télécharge le catalogue d'extensions et gère celles qui sont installées.</summary>
public class ExtensionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _installFolder;
    private readonly string _bundledCatalogPath;
    private readonly string? _catalogUrl;

    // Délai court : avec un réseau instable, mieux vaut basculer vite sur la copie intégrée
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public ExtensionService(string installFolder, string bundledCatalogPath, string? catalogUrl = null)
    {
        _installFolder = installFolder;
        _bundledCatalogPath = bundledCatalogPath;
        _catalogUrl = catalogUrl;
        Directory.CreateDirectory(_installFolder);
    }

    /// <summary>Le catalogue en ligne si possible, sinon celui livré avec Kode.</summary>
    public async Task<CatalogResult> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_catalogUrl))
        {
            try
            {
                var json = await _http.GetStringAsync(_catalogUrl, cancellationToken);
                return new CatalogResult(Parse(json), true);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Réseau coupé, lent ou catalogue illisible : on retombe sur la copie intégrée
            }
        }

        var local = await File.ReadAllTextAsync(_bundledCatalogPath, cancellationToken);
        return new CatalogResult(Parse(local), false);
    }

    public bool IsInstalled(string id)
    {
        var path = PathFor(id);
        return path != null && File.Exists(path);
    }

    public IReadOnlyList<ExtensionInfo> GetInstalled()
    {
        var result = new List<ExtensionInfo>();
        foreach (var file in Directory.EnumerateFiles(_installFolder, "*.json"))
        {
            try
            {
                var info = JsonSerializer.Deserialize<ExtensionInfo>(File.ReadAllText(file), JsonOptions);
                if (info is not null && info.Id.Length > 0) result.Add(info);
            }
            catch (JsonException)
            {
                // Fichier abîmé : on l'ignore
            }
        }
        return result;
    }

    public async Task InstallAsync(ExtensionInfo extension, CancellationToken cancellationToken = default)
    {
        var path = PathFor(extension.Id)
            ?? throw new ArgumentException($"Identifiant d'extension invalide : « {extension.Id} »");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(extension, JsonOptions), cancellationToken);
    }

    public void Uninstall(string id)
    {
        var path = PathFor(id);
        if (path != null && File.Exists(path)) File.Delete(path);
    }

    // L'identifiant vient d'un catalogue distant : on n'accepte que lettres, chiffres, - et _
    // pour qu'il ne puisse jamais servir à écrire un fichier ailleurs sur le disque.
    private string? PathFor(string id)
    {
        bool valid = id.Length > 0 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        return valid ? Path.Combine(_installFolder, id + ".json") : null;
    }

    private sealed class CatalogFile
    {
        public List<ExtensionInfo> Extensions { get; set; } = new();
    }

    private static IReadOnlyList<ExtensionInfo> Parse(string json) =>
        JsonSerializer.Deserialize<CatalogFile>(json, JsonOptions)?.Extensions ?? new List<ExtensionInfo>();
}