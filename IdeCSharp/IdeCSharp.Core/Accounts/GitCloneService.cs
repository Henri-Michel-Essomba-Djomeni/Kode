using System.ComponentModel;
using System.Diagnostics;

namespace IdeCSharp.Core.Accounts;

/// <summary>Clone un dépôt avec la commande git installée sur la machine.</summary>
public static class GitCloneService
{
    public static async Task CloneAsync(string url, string targetPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        // On refuse tout ce qui n'est pas une adresse GitHub en https
        if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seules les adresses https://github.com/... sont acceptées.");

        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // ArgumentList : chaque valeur est transmise telle quelle, sans interprétation par le shell
        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--progress");
        startInfo.ArgumentList.Add(url);
        startInfo.ArgumentList.Add(targetPath);
        // Jamais de question de mot de passe dans une fenêtre cachée : on échoue proprement
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        Process process;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Win32Exception)
        {
            throw new InvalidOperationException("Git est introuvable. Installe Git (git-scm.com) puis relance Kode.");
        }

        using (process)
        {
            // Si on annule, on arrête git
            using var cancelRegistration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { /* déjà terminé */ }
            });

            // git écrit sa progression sur la sortie d'erreur
            await Task.WhenAll(
                PumpAsync(process.StandardError, progress),
                PumpAsync(process.StandardOutput, progress));
            await process.WaitForExitAsync(CancellationToken.None);

            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"Le clonage a échoué (code {process.ExitCode}). Vérifie que le dépôt est public et que l'adresse est correcte.");
        }
    }

    private static async Task PumpAsync(StreamReader reader, IProgress<string>? progress)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.Length > 0) progress?.Report(line);
        }
    }
}