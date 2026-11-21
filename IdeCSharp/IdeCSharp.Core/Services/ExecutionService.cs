using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IdeCSharp.Core.Interfaces;
using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Services;

/// <summary>
/// Implémentation de IExecutionService : compile et exécute un projet via le SDK .NET (dotnet build / dotnet run).
/// OutputReceived peut être levé depuis un autre thread : l'UI doit passer par son Dispatcher.
/// </summary>
public sealed class ExecutionService : IExecutionService
{
    private const string SdkIntrouvable = "SDK .NET introuvable. Installe-le pour pouvoir compiler.";

    // fichier(ligne,col): error CS1002: message [projet.csproj]
    private static readonly Regex DiagnosticRegex = new(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>error|warning)\s+(?<code>\w+):\s+(?<msg>.?)(\s+\[[^\]]\.csproj\])?\s*$",
        RegexOptions.Compiled);

    private readonly object _sync = new();
    private Process? _current;
    private bool _busy;

    public event Action<string>? OutputReceived;

    /// <summary>Durée maximale d'exécution d'un programme (tâche T40). null = pas de limite.</summary>
    public TimeSpan? MaxRunDuration { get; set; } = TimeSpan.FromMinutes(5);

    // ---------- SDK ----------

    public bool IsSdkAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) return false;

            string sortie = p.StandardOutput.ReadToEnd().Trim();
            if (!p.WaitForExit(5000))
            {
                p.Kill();
                return false;
            }
            return p.ExitCode == 0 && sortie.Length > 0;
        }
        catch (Win32Exception)
        {
            return false; // "dotnet" introuvable
        }
    }

    // ---------- Compiler ----------

    public async Task<BuildResult> BuildAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            Emit("Une opération est déjà en cours.");
            return new BuildResult(false, Array.Empty<BuildError>());
        }

        try
        {
            return await BuildCoreAsync(projectPath, cancellationToken);
        }
        catch (Win32Exception)
        {
            Emit(SdkIntrouvable);
            return new BuildResult(false, Array.Empty<BuildError>());
        }
        finally
        {
            End();
        }
    }

    private async Task<BuildResult> BuildCoreAsync(string projectPath, CancellationToken ct)
    {
        string path = CleanPath(projectPath);
        Emit($"Compilation de {path}...");

        var erreurs = new List<BuildError>();
        var dejaVues = new HashSet<string>();   // dotnet répète chaque erreur dans son résumé

        int exitCode = await ExecuteAsync($"build \"{path}\" --nologo", redirectInput: false, line =>
        {
            var m = DiagnosticRegex.Match(line);
            if (!m.Success)
            {
                Emit(line);
                return;
            }

            lock (dejaVues)
            {
                if (!dejaVues.Add(line.Trim())) return;
            }

            string fichier = m.Groups["file"].Value;
            int numLigne = int.Parse(m.Groups["line"].Value);
            int colonne = int.Parse(m.Groups["col"].Value);
            string gravite = m.Groups["sev"].Value;
            string message = $"{m.Groups["code"].Value}: {m.Groups["msg"].Value}";

            // Texte lisible pour le panneau Sortie (les avertissements restent ici, pas dans Errors)
            Emit($"{Path.GetFileName(fichier)}({numLigne},{colonne}): {gravite} {message}");

            if (gravite == "error")
            {
                lock (erreurs) erreurs.Add(new BuildError(fichier, numLigne, colonne, message));
            }
        }, ct);

        return new BuildResult(exitCode == 0, erreurs);
    }

    // ---------- Exécuter ----------

    public async Task RunAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        if (!TryBegin())
        {
            Emit("Une opération est déjà en cours.");
            return;
        }

        try
        {
            var build = await BuildCoreAsync(projectPath, cancellationToken);
            if (!build.Success)
            {
                Emit("Compilation échouée : le programme n'est pas lancé.");
                return;
            }

            Emit("Exécution...");

            // Délai maximal : si dépassé, on arrête le programme
            bool delaiDepasse = false;
            using var minuteur = new CancellationTokenSource();
            if (MaxRunDuration is { } delai) minuteur.CancelAfter(delai);
            using var registration = minuteur.Token.Register(() =>
            {
                if (KillCurrent()) delaiDepasse = true;
            });

            string path = CleanPath(projectPath);
            int exitCode = await ExecuteAsync(
                $"run --project \"{path}\" --no-build", redirectInput: true, line => Emit(line), cancellationToken);

            if (delaiDepasse)
                Emit($"Délai maximal dépassé ({MaxRunDuration!.Value.TotalSeconds:0} s) : programme arrêté.");
            Emit($"Programme terminé (code {exitCode}).");
        }
        catch (Win32Exception)
        {
            Emit(SdkIntrouvable);
        }
        finally
        {
            End();
        }
    }

    // ---------- Saisie et arrêt ----------

    public void SendInput(string text)
    {
        Process? p;
        lock (_sync) p = _current;
        if (p is null) return;

        try
        {
            p.StandardInput.WriteLine(text);
            p.StandardInput.Flush();
        }
        catch (IOException) { }                 // le programme vient de se terminer
        catch (InvalidOperationException) { }   // entrée non redirigée (pendant la compilation)
    }

    public void Stop() => KillCurrent();

    private bool KillCurrent()
    {
        Process? p;
        lock (_sync) p = _current;
        if (p is null) return false;

        try
        {
            if (!p.HasExited)
            {
                p.Kill(entireProcessTree: true);   // tue aussi les processus enfants
                return true;
            }
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
        return false;
    }

    // ---------- Outils internes ----------

    /// <summary>Lance "dotnet &lt;arguments&gt;", lit les sorties ligne par ligne, retourne le code de sortie.</summary>
    private async Task<int> ExecuteAsync(
        string arguments, bool redirectInput, Action<string> onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };

        using var registration = ct.Register(() => KillCurrent());   // annulation = arrêt du processus

        process.Start();
        lock (_sync) _current = process;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        finally
        {
            lock (_sync) _current = null;
        }
    }

    private bool TryBegin()
    {
        lock (_sync)
        {
            if (_busy) return false;
            _busy = true;
            return true;
        }
    }

    private void End()
    {
        lock (_sync) _busy = false;
    }

    private void Emit(string line) => OutputReceived?.Invoke(line);

    // Un chemin qui finit par \ ferait échapper le guillemet de fermeture dans la ligne de commande
    private static string CleanPath(string path) => path.TrimEnd('\\', '/');
}