using System.Text;
using IdeCSharp.Core.Interfaces;
using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Services;

/// <summary>
/// Implémentation du module Fichiers (responsable : Raïssa).
/// Aucune dépendance à l'interface graphique : uniquement System.IO.
/// </summary>
public class FileService : IFileService, IFileEncodingService
{
    static FileService()
    {
        // Active les anciens encodages Windows (dont Windows-1252, celui du Bloc-notes « ANSI »).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // Taille d'un bloc de lecture : 64 Ko. Assez grand pour être rapide,
    // assez petit pour ne jamais bloquer l'interface.
    private const int ChunkSize = 64 * 1024;

    // Seuil du mode « gros fichier ». Valeur PROVISOIRE : elle sera fixée
    // avec Valdez après l'étude T33 et les mesures de T36.
    public const long DefaultLargeFileThresholdBytes = 10L * 1024 * 1024; // 10 Mo

    // Taille maximale qu'on accepte de charger : au-delà, la lecture est refusée.
    // Mesuré : 100 Mo de fichier = environ 437 Mo de mémoire. Au-delà, rien n'a été mesuré.
    public const long DefaultMaxFileSizeBytes = 100L * 1024 * 1024; // 100 Mo

    private readonly long _largeFileThresholdBytes;
    private readonly long _maxFileSizeBytes;

    public FileService(
        long largeFileThresholdBytes = DefaultLargeFileThresholdBytes,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes)
    {
        _largeFileThresholdBytes = largeFileThresholdBytes;
        _maxFileSizeBytes = maxFileSizeBytes;
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
        var result = await ReadFileWithEncodingAsync(path, progress, cancellationToken).ConfigureAwait(false);
        return result.Content;
    }

    /// <summary>
    /// Lit le fichier et détecte son encodage :
    ///  1. un BOM (UTF-8, UTF-16, UTF-32) donne l'encodage sans ambiguïté ;
    ///  2. sans BOM, on essaie l'UTF-8 en mode strict ;
    ///  3. si le fichier n'est pas de l'UTF-8 valide (ancien fichier « ANSI » avec des accents),
    ///     on le relit en Windows-1252 au lieu d'afficher des caractères illisibles.
    /// </summary>
    public async Task<FileReadResult> ReadFileWithEncodingAsync(
        string path,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Un seul « limiteur » pour les deux tentatives : la progression ne recule jamais.
        var throttle = progress is null ? null : new ThrottledProgress(progress);

        FileReadResult result;
        try
        {
            result = await ReadOnceAsync(path, useWindows1252: false, throttle, cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            // Octets invalides en UTF-8 : ce n'est pas de l'UTF-8, on relit en Windows-1252.
            result = await ReadOnceAsync(path, useWindows1252: true, throttle, cancellationToken).ConfigureAwait(false);
        }

        throttle?.Complete();
        return result;
    }

    private async Task<FileReadResult> ReadOnceAsync(
        string path, bool useWindows1252, ThrottledProgress? throttle, CancellationToken cancellationToken)
    {
        // useAsync: true => lecture réellement asynchrone (l'interface ne gèle pas).
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: ChunkSize, useAsync: true);

        // Garde-fou : un fichier trop gros saturerait la mémoire et ferait planter l'IDE.
        // Le routeur affichera ce message dans la barre d'état.
        if (stream.Length > _maxFileSizeBytes)
            throw new IOException(
                $"Fichier trop volumineux ({stream.Length / 1024 / 1024} Mo). " +
                $"Limite : {_maxFileSizeBytes / 1024 / 1024} Mo.");

        Encoding encoding;
        string name;
        string? bomName = DetectBom(stream, out Encoding? bomEncoding);
        if (bomName != null)
        {
            encoding = bomEncoding!;
            name = bomName;
        }
        else if (useWindows1252)
        {
            encoding = Encoding.GetEncoding(1252);
            name = FileEncodings.Windows1252;
        }
        else
        {
            // Mode strict : un octet invalide lève DecoderFallbackException (voir plus haut).
            encoding = new UTF8Encoding(false, throwOnInvalidBytes: true);
            name = FileEncodings.Utf8;
        }

        // Avec un BOM, le StreamReader le saute lui-même (detectEncodingFromByteOrderMarks).
        using var reader = new StreamReader(
            stream, encoding, detectEncodingFromByteOrderMarks: bomName != null, bufferSize: ChunkSize);

        long totalBytes = stream.Length;
        var builder = new StringBuilder();
        var buffer = new char[ChunkSize];

        int read;
        // ConfigureAwait(false) : après chaque bloc, on ne force PAS le retour sur le thread
        // de l'interface. La lecture se déroule entièrement en arrière-plan.
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            builder.Append(buffer, 0, read);

            if (throttle != null && totalBytes > 0)
                throttle.Report(Math.Min(1.0, (double)stream.Position / totalBytes));
        }

        return new FileReadResult(builder.ToString(), name);
    }

    /// <summary>Cherche un BOM (marque d'ordre des octets) au début du fichier, puis revient au début.</summary>
    private static string? DetectBom(FileStream stream, out Encoding? encoding)
    {
        var head = new byte[4];
        int n = stream.Read(head, 0, 4);
        stream.Position = 0;
        encoding = null;

        // UTF-32 d'abord : son BOM (FF FE 00 00) commence comme celui de l'UTF-16.
        if (n >= 4 && head[0] == 0xFF && head[1] == 0xFE && head[2] == 0x00 && head[3] == 0x00)
        {
            encoding = new UTF32Encoding(false, true);
            return FileEncodings.Utf32Le;
        }
        if (n >= 4 && head[0] == 0x00 && head[1] == 0x00 && head[2] == 0xFE && head[3] == 0xFF)
        {
            encoding = new UTF32Encoding(true, true);
            return FileEncodings.Utf32Be;
        }
        if (n >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            encoding = Encoding.UTF8;
            return FileEncodings.Utf8Bom;
        }
        if (n >= 2 && head[0] == 0xFF && head[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
            return FileEncodings.Utf16Le;
        }
        if (n >= 2 && head[0] == 0xFE && head[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
            return FileEncodings.Utf16Be;
        }
        return null;
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

    /// <summary>
    /// Enregistre dans l'encodage demandé (un nom de FileEncodings).
    /// Le texte est converti AVANT d'ouvrir le fichier : si un caractère n'existe pas dans l'encodage
    /// (par exemple « € » en Windows-1252 n'est pas un souci, mais « 日 » oui), une erreur est levée
    /// et le fichier existant n'est pas touché.
    /// </summary>
    public async Task SaveFileWithEncodingAsync(
        string path, string content, string encodingName, CancellationToken cancellationToken = default)
    {
        Encoding encoding = EncodingFor(encodingName); // lève ArgumentException si le nom est inconnu

        byte[] preamble = encoding.GetPreamble(); // le BOM, s'il y en a un
        byte[] body = encoding.GetBytes(content); // lève une erreur si un caractère est impossible
        byte[] all = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, all, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, all, preamble.Length, body.Length);

        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        await File.WriteAllBytesAsync(path, all, cancellationToken).ConfigureAwait(false);
    }

    private static Encoding EncodingFor(string name) => name switch
    {
        FileEncodings.Utf8 => new UTF8Encoding(false),
        FileEncodings.Utf8Bom => new UTF8Encoding(true),
        FileEncodings.Utf16Le => new UnicodeEncoding(false, true),
        FileEncodings.Utf16Be => new UnicodeEncoding(true, true),
        FileEncodings.Utf32Le => new UTF32Encoding(false, true),
        FileEncodings.Utf32Be => new UTF32Encoding(true, true),
        // Mode strict : un caractère impossible lève une erreur au lieu de devenir « ? » en silence.
        FileEncodings.Windows1252 => Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback),
        _ => throw new ArgumentException($"Encodage inconnu : « {name} ».", nameof(name))
    };

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

    /// <summary>Ne transmet la progression que lorsque le pourcentage change (100 rapports au maximum).</summary>
    private sealed class ThrottledProgress
    {
        private readonly IProgress<double> _inner;
        private int _lastPercent = -1;

        public ThrottledProgress(IProgress<double> inner) => _inner = inner;

        public void Report(double fraction)
        {
            int percent = (int)(fraction * 100);
            if (percent > _lastPercent)
            {
                _lastPercent = percent;
                _inner.Report(percent / 100.0);
            }
        }

        /// <summary>La lecture se termine toujours sur 100 %.</summary>
        public void Complete()
        {
            if (_lastPercent < 100)
            {
                _lastPercent = 100;
                _inner.Report(1.0);
            }
        }
    }
}
