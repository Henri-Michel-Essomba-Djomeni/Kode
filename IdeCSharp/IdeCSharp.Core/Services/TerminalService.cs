using System.Diagnostics;
using System.Globalization;
using System.Text;
using IdeCSharp.Core.Interfaces;
using IdeCSharp.Core.Models;

namespace IdeCSharp.Core.Services;

/// <summary>
/// Implémentation du module Terminal : lance cmd.exe ou powershell.exe avec les
/// flux redirigés. Aucune dépendance à l'interface graphique.
/// Limite connue : ce n'est pas un vrai terminal (pas de ConPTY). Les programmes qui
/// exigent une vraie console (éditeurs plein écran, REPL interactifs) ne marcheront pas.
/// </summary>
public sealed class TerminalService : ITerminalService, IDisposable
{
    private readonly object _lock = new();
    private Process? _process;

    public event Action<string>? OutputReceived;
    public event Action<int>? Exited;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                try { return _process is { HasExited: false }; }
                catch (InvalidOperationException) { return false; }
            }
        }
    }

    public void Start(ShellKind shell, string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory))
            throw new DirectoryNotFoundException($"Dossier introuvable : {workingDirectory}");

        Stop();

        // Windows écrit les consoles dans la page de code « OEM » (ex. 850 en français).
        // Sans ça, « é » s'affiche « Ú » ou « ?».
        var oem = GetOemEncoding();

        var info = new ProcessStartInfo
        {
            FileName = shell == ShellKind.Cmd ? "cmd.exe" : "powershell.exe",
            Arguments = shell == ShellKind.Cmd ? string.Empty : "-NoLogo -NoProfile",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = oem,
            StandardOutputEncoding = oem,
            StandardErrorEncoding = oem
        };

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.Exited += (_, _) => OnProcessExited(process);

        process.Start();
        process.StandardInput.AutoFlush = true;

        lock (_lock) { _process = process; }

        _ = ReadLoopAsync(process, process.StandardOutput);
        _ = ReadLoopAsync(process, process.StandardError);
    }

    public void SendCommand(string commandLine)
    {
        Process? process;
        lock (_lock) { process = _process; }

        if (process is null) throw new InvalidOperationException("Aucune console démarrée.");

        try
        {
            process.StandardInput.WriteLine(commandLine);
        }
        catch (IOException)
        {
            // La console vient de se fermer pendant l'écriture : Exited préviendra l'interface.
        }
    }

    public void Stop()
    {
        Process? process;
        lock (_lock)
        {
            process = _process;
            _process = null; // d'abord : ainsi Exited et la lecture ignorent ce processus
        }

        if (process is null) return;

        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* déjà terminé */ }
        catch (System.ComponentModel.Win32Exception) { /* accès refusé : on abandonne proprement */ }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose() => Stop();

    // Lit par morceaux (pas par lignes) pour afficher l'invite tout de suite.
    private async Task ReadLoopAsync(Process process, StreamReader reader)
    {
        var buffer = new char[1024];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0)
            {
                if (!IsCurrent(process)) return;
                OutputReceived?.Invoke(new string(buffer, 0, read));
            }
        }
        catch (ObjectDisposedException) { /* processus arrêté par Stop() */ }
        catch (IOException) { /* flux fermé */ }
    }

    private void OnProcessExited(Process process)
    {
        if (!IsCurrent(process)) return; // arrêt voulu : pas de notification

        int code;
        try { code = process.ExitCode; }
        catch (InvalidOperationException) { code = -1; }

        // On garde _process : la lecture peut encore livrer les derniers caractères.
        // Stop() ou Start() nettoiera plus tard.
        Exited?.Invoke(code);
    }

    private bool IsCurrent(Process process)
    {
        lock (_lock) { return ReferenceEquals(_process, process); }
    }

    private static Encoding GetOemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return new UTF8Encoding(false); // sans BOM, sinon un BOM serait envoyé à la console
        }
    }
}
