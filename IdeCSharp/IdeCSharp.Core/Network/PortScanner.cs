using System.Diagnostics;

namespace IdeCSharp.Core.Network;

/// <summary>Un port TCP en écoute sur cette machine.</summary>
public record ListeningPort(int Port, string Address, int ProcessId, string ProcessName);

public static class PortScanner
{
    /// <summary>Liste les ports TCP en écoute, triés par numéro. Lance « netstat » en arrière-plan.</summary>
    public static async Task<IReadOnlyList<ListeningPort>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo("netstat", "-ano -p TCP")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("netstat n'a pas pu démarrer.");
        var text = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var result = new List<ListeningPort>();
        var names = new Dictionary<int, string>();   // un même processus ouvre souvent plusieurs ports

        foreach (var line in text.Split('\n'))
        {
            // Une ligne utile ressemble à : TCP  0.0.0.0:8000  0.0.0.0:0  LISTENING  4321
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 5 || !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)) continue;

            // Un port en écoute n'a pas d'interlocuteur : son adresse distante se termine par « :0 ».
            // On évite ainsi de lire le mot « LISTENING », qui change selon la langue de Windows.
            if (!parts[2].EndsWith(":0", StringComparison.Ordinal)) continue;

            if (!int.TryParse(parts[^1], out var pid)) continue;

            var local = parts[1];
            var colon = local.LastIndexOf(':');
            if (colon < 0 || !int.TryParse(local[(colon + 1)..], out var port)) continue;

            if (!names.TryGetValue(pid, out var name))
            {
                name = NameOf(pid);
                names[pid] = name;
            }
            result.Add(new ListeningPort(port, local[..colon], pid, name));
        }

        return result.OrderBy(p => p.Port).ThenBy(p => p.Address).ToList();
    }

    private static string NameOf(int pid)
    {
        if (pid == 0) return "Système";
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException) { return "(terminé)"; }
        catch (InvalidOperationException) { return "(inconnu)"; }
        catch (System.ComponentModel.Win32Exception) { return "(accès refusé)"; }
    }
}