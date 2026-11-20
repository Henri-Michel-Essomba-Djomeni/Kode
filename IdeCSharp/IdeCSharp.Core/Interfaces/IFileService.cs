using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Interfaces;

/// <summary>Contrat du module Fichiers (responsable : Raïssa).</summary>
public interface IFileService
{
    /// <summary>Lit un fichier par blocs, avec progression (0 à 1) et annulation possible.</summary>
    Task<string> ReadFileAsync(string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Enregistre le contenu dans un fichier.</summary>
    Task SaveFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Liste le contenu d'un dossier pour l'arborescence.</summary>
    IReadOnlyList<FileNode> ListDirectory(string folderPath);

    void CreateFile(string path);
    void CreateFolder(string path);
    void Rename(string path, string newName);
    void Delete(string path);

    /// <summary>Vrai si le fichier dépasse le seuil du mode « gros fichier ».</summary>
    bool IsLargeFile(string path);
}
