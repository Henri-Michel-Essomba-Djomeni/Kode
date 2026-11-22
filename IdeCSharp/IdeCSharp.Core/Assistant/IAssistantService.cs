namespace IdeCSharp.Core.Assistant;

/// <summary>Contrat de l'assistant : un service d'IA répond aux questions de l'utilisateur.</summary>
public interface IAssistantService
{
    /// <summary>Vrai si l'assistant est prêt (par exemple, une clé d'accès est configurée).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Envoie une question avec le fichier ouvert comme contexte, et renvoie la réponse
    /// par morceaux, au fur et à mesure qu'elle arrive.
    /// </summary>
    IAsyncEnumerable<string> AskAsync(string question, string? fileName, string? fileContent,
        CancellationToken cancellationToken = default);
}