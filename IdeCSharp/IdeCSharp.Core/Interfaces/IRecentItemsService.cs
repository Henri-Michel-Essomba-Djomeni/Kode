namespace IdeCSharp.Core.Interfaces;

/// <summary>Contrat des fichiers et dossiers récents (T29, responsable : Raïssa).</summary>
public interface IRecentItemsService
{
    /// <summary>Fichiers récents, du plus récent au plus ancien. Ceux qui n'existent plus sont omis.</summary>
    IReadOnlyList<string> GetRecentFiles();

    /// <summary>Dossiers récents, du plus récent au plus ancien. Ceux qui n'existent plus sont omis.</summary>
    IReadOnlyList<string> GetRecentFolders();

    /// <summary>À appeler quand un fichier est ouvert ou enregistré.</summary>
    void AddFile(string path);

    /// <summary>À appeler quand un dossier est ouvert.</summary>
    void AddFolder(string path);

    /// <summary>Vide les deux listes (menu « Effacer les récents »).</summary>
    void Clear();
}
