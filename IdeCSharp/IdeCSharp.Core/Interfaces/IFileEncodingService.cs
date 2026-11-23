using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Interfaces;

/// <summary>
/// Extension de IFileService pour gérer l'encodage (T30, responsable : Raïssa).
/// Interface SÉPARÉE : l'interface du chef (IFileService, T05) n'est pas modifiée,
/// donc le code des autres modules continue de compiler sans changement.
/// Le routeur peut tester : « if (Files is IFileEncodingService enc) ... ».
/// </summary>
public interface IFileEncodingService
{
    /// <summary>Lit un fichier et renvoie le texte avec l'encodage détecté (voir FileEncodings).</summary>
    Task<FileReadResult> ReadFileWithEncodingAsync(
        string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Enregistre le texte dans l'encodage demandé (un nom de FileEncodings).</summary>
    Task SaveFileWithEncodingAsync(
        string path, string content, string encodingName, CancellationToken cancellationToken = default);
}
