using System.Diagnostics;
using System.Text;
using IdeCSharp.Core.Services;

// Programme de mesure T33 / T36 (Raïssa) : lecture de gros fichiers avec FileService.
// Usage : dotnet run -c Release -- <taille en Mo>      exemple : 10, 50 ou 100
// Chaque taille se lance dans un processus séparé pour que le pic de mémoire soit fiable.

if (args.Length == 0 || !int.TryParse(args[0], out int sizeMb) || sizeMb <= 0)
{
    Console.WriteLine("Usage : dotnet run -c Release -- <taille en Mo>   (ex. 10, 50, 100)");
    return;
}

string path = Path.Combine(Path.GetTempPath(), $"kode_bench_{sizeMb}mb.txt");

try
{
    Console.WriteLine($"Génération d'un fichier de {sizeMb} Mo...");
    GenerateFile(path, sizeMb);
    await MeasureAsync(path, sizeMb);
}
finally
{
    if (File.Exists(path)) File.Delete(path); // on ne laisse pas 100 Mo traîner dans le dossier temporaire
}

// ----------------------------------------------------------------------

static void GenerateFile(string path, int sizeMb)
{
    long target = sizeMb * 1024L * 1024L;
    long written = 0;
    int i = 0;

    // Fichier de texte ressemblant à du code (lignes d'environ 90 caractères, ASCII).
    using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false), bufferSize: 1 << 16);
    while (written < target)
    {
        string line = $"    public void Method{i}(int value) {{ Console.WriteLine(\"Ligne {i} : valeur = \" + value); }}";
        writer.WriteLine(line);
        written += line.Length + 2; // + retour à la ligne Windows (\r\n)
        i++;
    }
}

static async Task MeasureAsync(string path, int sizeMb)
{
    var service = new FileService();
    long fileBytes = new FileInfo(path).Length;
    var progress = new CountingProgress();

    // On part d'une mémoire propre pour que la mesure ne soit pas faussée.
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    long before = GC.GetTotalMemory(forceFullCollection: true);

    var sw = Stopwatch.StartNew();
    string content = await service.ReadFileAsync(path, progress);
    sw.Stop();

    // Mémoire gardée par le texte chargé (le texte doit rester vivant pour être compté).
    long retained = GC.GetTotalMemory(forceFullCollection: true) - before;
    long peakProcess = Process.GetCurrentProcess().PeakWorkingSet64;
    double seconds = sw.Elapsed.TotalSeconds;

    Console.WriteLine();
    Console.WriteLine($"Fichier                      : {sizeMb} Mo ({fileBytes:N0} octets)");
    Console.WriteLine($"Caractères lus               : {content.Length:N0}");
    Console.WriteLine($"Temps de lecture             : {sw.ElapsedMilliseconds:N0} ms");
    Console.WriteLine($"Débit                        : {sizeMb / seconds:N0} Mo/s");
    Console.WriteLine($"Mémoire du texte en RAM      : {retained / 1024 / 1024:N0} Mo");
    Console.WriteLine($"Pic mémoire du processus     : {peakProcess / 1024 / 1024:N0} Mo");
    Console.WriteLine($"Rapports de progression      : {progress.Count:N0} (dernier = {progress.Last:P0})");
    Console.WriteLine();
    Console.WriteLine("Ligne à copier dans le tableau (Taille Mo | Temps ms | Débit Mo/s | RAM texte Mo | Pic Mo) :");
    Console.WriteLine($"| {sizeMb} | {sw.ElapsedMilliseconds} | {sizeMb / seconds:N0} | {retained / 1024 / 1024} | {peakProcess / 1024 / 1024} |");

    GC.KeepAlive(content);
}

sealed class CountingProgress : IProgress<double>
{
    public int Count { get; private set; }
    public double Last { get; private set; }

    public void Report(double value)
    {
        Count++;
        Last = value;
    }
}
