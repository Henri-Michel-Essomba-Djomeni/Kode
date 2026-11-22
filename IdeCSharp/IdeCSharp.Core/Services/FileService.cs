using System.Text;
using IdeCSharp.Core.Interfaces;
using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Services;

/// <summary>
/// Implémentation du module Fichiers (responsable : Raïssa).
/// Aucune dépendance à l'interface graphique : uniquement System.IO.
/// </summary>
public class FileService : IFileService
{
    // Taille d'un bloc de lecture : 64 Ko. Assez grand pour être rapide,
    // assez petit pour ne jamais bloquer l'interface.
    private const int ChunkSize = 64 * 1024;

    // Seuil du mode « gros fichier ». Valeur PROVISOIRE : elle sera fixée
    // avec Valdez après l'étude T33 et les mesures de T36.
    public const long DefaultLargeFileThresholdBytes = 10L * 1024 * 1024; // 10 Mo

    private readonly long _largeFileThresholdBytes;

    public FileService(long largeFileThresholdBytes = DefaultLargeFileThresholdBytes)
    {
        _largeFileThresholdBytes = largeFileThresholdBytes;
    }

    // ------------------------------------------------------------------
    // Lecture / écriture (T26, T34)
    // ------------------------------------------------------------------

    /// <summary>
    /// Lit le fichier par blocs de 64 Ko. Après chaque bloc, on signale la
    /// progression (0 à 1) et on vérifie si l'utilisateur a annulé.
    /// </summary>
    public async Task<string> ReadFileAsync(
        string path,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // useAsync: true => lecture réellement asynchrone (l'interface ne gèle pas).
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: ChunkSize, useAsync: true);

        // Le StreamReader détecte l'encodage grâce au BOM (UTF-8, UTF-16...).
        // Sans BOM, on suppose de l'UTF-8.
        using var reader = new StreamReader(
            stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: ChunkSize);

        long totalBytes = stream.Length;
        var builder = new StringBuilder();
        var buffer = new char[ChunkSize];
        int lastPercent = -1; // dernier pourcentage signalé

        int read;
        // ConfigureAwait(false) : après chaque bloc, on ne force PAS le retour sur le thread
        // de l'interface. La lecture se déroule entièrement en arrière-plan.
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            builder.Append(buffer, 0, read);

            if (progress != null && totalBytes > 0)
            {
                // On ne signale que lorsque le pourcentage change (100 rapports au maximum),
                // pour ne pas saturer le thread de l'interface avec des milliers de messages.
                int percent = (int)(Math.Min(1.0, (double)stream.Position / totalBytes) * 100);
                if (percent > lastPercent)
                {
                    lastPercent = percent;
                    progress.Report(percent / 100.0);
                }
            }
        }

        if (lastPercent < 100)
            progress?.Report(1.0); // la lecture se termine toujours sur 100 %
        return builder.ToString();
    }

    /// <summary>Enregistre en UTF-8 sans BOM (le standard le plus compatible).</summary>
    public async Task SaveFileAsync(
        string path, string content, CancellationToken cancellationToken = default)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder); // ne fait rien si le dossier existe déjà

        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), cancellationToken)
            .ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Arborescence (T27)
    // ------------------------------------------------------------------

    /// <summary>Dossiers d'abord, puis fichiers, chacun trié par nom.</summary>
    public IReadOnlyList<FileNode> ListDirectory(string folderPath)
    {
        var directory = new DirectoryInfo(folderPath);
        if (!directory.Exists)
            throw new DirectoryNotFoundException($"Dossier introuvable : {folderPath}");

        try
        {
            return directory.EnumerateFileSystemInfos()
                // On masque les éléments cachés/système (ex. le dossier .git).
                .Where(info => (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderByDescending(info => info is DirectoryInfo)       // dossiers en premier
                .ThenBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
                .Select(info => new FileNode(info.Name, info.FullName, info is DirectoryInfo))
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            // Dossier protégé par Windows : on affiche simplement « vide ».
            return Array.Empty<FileNode>();
        }
    }

    // ------------------------------------------------------------------
    // Créer / renommer / supprimer (T28)
    // ------------------------------------------------------------------

    public void CreateFile(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        // FileMode.CreateNew : échoue si le fichier existe déjà.
        // (File.Create, lui, écraserait silencieusement un fichier existant.)
        using var _ = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
    }

    public void CreateFolder(string path)
    {
        if (File.Exists(path))
            throw new IOException($"Un fichier porte déjà ce nom : {path}");

        Directory.CreateDirectory(path);
    }

    public void Rename(string path, string newName)
    {
        // newName doit être un simple nom, pas un chemin (pas de \ ni de /).
        if (string.IsNullOrWhiteSpace(newName) ||
            newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Nom invalide.", nameof(newName));

        string? parent = Path.GetDirectoryName(path);
        if (parent == null)
            throw new ArgumentException("Chemin invalide.", nameof(path));

        string destination = Path.Combine(parent, newName);

        if (File.Exists(destination) || Directory.Exists(destination))
            throw new IOException($"« {newName} » existe déjà dans ce dossier.");

        if (Directory.Exists(path))
            Directory.Move(path, destination);
        else if (File.Exists(path))
            File.Move(path, destination);
        else
            throw new FileNotFoundException("Élément introuvable.", path);
    }

    /// <summary>
    /// Supprime sans rien demander : la confirmation est le travail de l'interface
    /// (le Core ne doit jamais ouvrir de boîte de dialogue).
    /// </summary>
    public void Delete(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        else if (File.Exists(path))
            File.Delete(path);
        else
            throw new FileNotFoundException("Élément introuvable.", path);
    }

    // ------------------------------------------------------------------
    // Gros fichiers (T33, T35)
    // ------------------------------------------------------------------

    public bool IsLargeFile(string path)
    {
        var info = new FileInfo(path);
        return info.Exists && info.Length > _largeFileThresholdBytes;
    }
}
