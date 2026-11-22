using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Interfaces;

/// <summary>Contrat du module Terminal (console cmd / PowerShell du panneau du bas).</summary>
public interface ITerminalService
{
    /// <summary>Vrai si une console est démarrée et n'est pas terminée.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Démarre une console dans le dossier donné (chemin absolu, doit exister).
    /// Si une console tourne déjà, elle est arrêtée d'abord.
    /// Lève DirectoryNotFoundException si le dossier n'existe pas.
    /// </summary>
    void Start(ShellKind shell, string workingDirectory);

    /// <summary>Envoie une ligne de commande à la console (le retour à la ligne est ajouté ici).</summary>
    void SendCommand(string commandLine);

    /// <summary>Arrête la console et ses processus enfants. Sans effet si rien ne tourne.</summary>
    void Stop();

    /// <summary>
    /// Texte reçu de la console (sortie normale ET erreurs mélangées), par morceaux
    /// et non par lignes : l'invite « C:\&gt; » arrive donc sans attendre un retour à la ligne.
    /// ATTENTION : levé depuis un thread d'arrière-plan. L'interface doit repasser par le Dispatcher.
    /// </summary>
    event Action<string>? OutputReceived;

    /// <summary>
    /// Levé quand la console se termine d'elle-même (ex. l'utilisateur tape « exit »).
    /// Le paramètre est le code de sortie. Non levé quand c'est Stop() ou Start() qui l'a arrêtée.
    /// Même remarque : thread d'arrière-plan.
    /// </summary>
    event Action<int>? Exited;
}
