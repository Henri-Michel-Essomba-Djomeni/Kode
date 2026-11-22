using System.Text.Json;
using IdeCSharp.Core.Interfaces;

namespace IdeCSharp.Core.Services;

/// <summary>
/// Mémorise les derniers fichiers et dossiers ouverts, même après la fermeture de l'IDE.
/// Les listes sont enregistrées dans un fichier JSON.
/// </summary>
public class RecentItemsService : IRecentItemsService
{
    public const int DefaultMaxItems = 10;

    private readonly string _storagePath;
    private readonly int _maxItems;
    private readonly object _lock = new();
    private List<string> _files = new();
    private List<string> _folders = new();

    /// <summary>Emplacement par défaut : %APPDATA%\Kode\recent.json</summary>
    public static string DefaultStoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kode", "recent.json");

    /// <param name="storagePath">Fichier de sauvegarde (par défaut : AppData). Les tests passent un dossier temporaire.</param>
    /// <param name="maxItems">Nombre maximum d'éléments gardés dans chaque liste.</param>
    public RecentItemsService(string? storagePath = null, int maxItems = DefaultMaxItems)
    {
        _storagePath = storagePath ?? DefaultStoragePath;
        _maxItems = maxItems;
        Load();
    }

    public IReadOnlyList<string> GetRecentFiles()
    {
        lock (_lock)
            return _files.Where(File.Exists).ToList();
    }

    public IReadOnlyList<string> GetRecentFolders()
    {
        lock (_lock)
            return _folders.Where(Directory.Exists).ToList();
    }

    public void AddFile(string path) => Add(_files, path);

    public void AddFolder(string path) => Add(_folders, path);

    public void Clear()
    {
        lock (_lock)
        {
            _files.Clear();
            _folders.Clear();
            Save();
        }
    }

    // ------------------------------------------------------------------

    private void Add(List<string> list, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return; // rien à mémoriser

        string fullPath = Path.GetFullPath(path);

        lock (_lock)
        {
            // Si l'élément est déjà dans la liste, on l'enlève pour le remettre en premier.
            // Windows ne distingue pas majuscules et minuscules : on compare sans casse.
            list.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, fullPath);

            if (list.Count > _maxItems)
                list.RemoveRange(_maxItems, list.Count - _maxItems);

            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
                return;

            var data = JsonSerializer.Deserialize<StoredData>(File.ReadAllText(_storagePath));
            if (data == null)
                return;

            _files = (data.Files ?? new()).Take(_maxItems).ToList();
            _folders = (data.Folders ?? new()).Take(_maxItems).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fichier illisible ou abîmé : on repart avec des listes vides
            // plutôt que de faire planter le démarrage de l'IDE.
        }
    }

    private void Save()
    {
        try
        {
            string? folder = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var data = new StoredData { Files = _files, Folders = _folders };
            File.WriteAllText(_storagePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ne pas pouvoir mémoriser les récents n'est pas grave : on ignore l'erreur.
        }
    }

    private sealed class StoredData
    {
        public List<string>? Files { get; set; }
        public List<string>? Folders { get; set; }
    }
}
