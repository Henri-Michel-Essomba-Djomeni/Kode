using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Interfaces;

/// <summary>Contrat du module Exécution (responsable : Kammadeu).</summary>
public interface IExecutionService
{
    /// <summary>Vrai si le SDK .NET est détecté sur la machine.</summary>
    bool IsSdkAvailable();

    /// <summary>Compile le projet en arrière-plan et retourne les erreurs éventuelles.</summary>
    Task<BuildResult> BuildAsync(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>Lance le programme compilé.</summary>
    Task RunAsync(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>Envoie une saisie utilisateur au programme en cours.</summary>
    void SendInput(string text);

    /// <summary>Arrête proprement le processus en cours.</summary>
    void Stop();

    /// <summary>Déclenché à chaque ligne de sortie du programme (panneau Sortie).</summary>
    event Action<string>? OutputReceived;
}
