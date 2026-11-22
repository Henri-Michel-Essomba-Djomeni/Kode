namespace IdeCSharp.Core.Runners;

/// <summary>
/// Comment exécuter un type de fichier. Dans les arguments :
///   {file} chemin du fichier, {dir} son dossier, {name} son nom sans extension,
///   {out} l'exécutable produit par la compilation.
/// </summary>
public sealed record RunnerDefinition(
    string Language,
    string[] Extensions,
    string[] Tools,             // programmes cherchés dans le PATH, le premier trouvé est utilisé
    string? CompileArguments,   // null : pas d'étape de compilation séparée
    string RunArguments,        // utilisé quand il n'y a pas de compilation séparée
    string InstallHint,         // message affiché si l'outil est absent
    bool OpenWithShell = false) // true : on ouvre le fichier avec Windows (ex. une page HTML)
{
    public bool NeedsCompile => CompileArguments is not null;
}

public static class RunnerCatalog
{
    public static IReadOnlyList<RunnerDefinition> All { get; } = new List<RunnerDefinition>
    {
        // Le SDK .NET 10 sait lancer directement un fichier .cs
        new("C#", new[] { ".cs" }, new[] { "dotnet" }, null, "run \"{file}\"",
            "Installe le SDK .NET 10 : https://dotnet.microsoft.com/download"),

        new("Python", new[] { ".py" }, new[] { "python", "py" }, null, "\"{file}\"",
            "Installe Python (coche « Add to PATH ») : https://www.python.org/downloads"),

        new("JavaScript", new[] { ".js", ".mjs" }, new[] { "node" }, null, "\"{file}\"",
            "Installe Node.js : https://nodejs.org"),

        // Java 11+ sait lancer directement un fichier source
        new("Java", new[] { ".java" }, new[] { "java" }, null, "\"{file}\"",
            "Installe un JDK 11 ou plus : https://adoptium.net"),

        new("C", new[] { ".c" }, new[] { "gcc" }, "\"{file}\" -o \"{out}\"", "",
            "Installe MSYS2 / MinGW-w64 (gcc) et ajoute-le au PATH : https://www.msys2.org"),

        new("C++", new[] { ".cpp", ".cc", ".cxx" }, new[] { "g++" }, "\"{file}\" -o \"{out}\"", "",
            "Installe MSYS2 / MinGW-w64 (g++) et ajoute-le au PATH : https://www.msys2.org"),

        new("Rust", new[] { ".rs" }, new[] { "rustc" }, "\"{file}\" -o \"{out}\"", "",
            "Installe Rust : https://rustup.rs"),

        new("Go", new[] { ".go" }, new[] { "go" }, null, "run \"{file}\"",
            "Installe Go : https://go.dev/dl"),

        new("PHP", new[] { ".php" }, new[] { "php" }, null, "\"{file}\"",
            "Installe PHP : https://windows.php.net/download"),

        new("Ruby", new[] { ".rb" }, new[] { "ruby" }, null, "\"{file}\"",
            "Installe Ruby : https://rubyinstaller.org"),

        new("Lua", new[] { ".lua" }, new[] { "lua" }, null, "\"{file}\"",
            "Installe Lua : https://www.lua.org/download.html"),

        // Bypass ne vaut que pour ce lancement : la politique de PowerShell n'est pas modifiée
        new("PowerShell", new[] { ".ps1" }, new[] { "pwsh", "powershell" }, null,
            "-NoProfile -ExecutionPolicy Bypass -File \"{file}\"",
            "PowerShell est inclus avec Windows"),

        new("Batch", new[] { ".bat", ".cmd" }, new[] { "cmd" }, null, "/c \"{file}\"",
            "cmd est inclus avec Windows"),

        // Pas d'interpréteur : Windows ouvre la page dans le navigateur
        new("HTML", new[] { ".html", ".htm" }, Array.Empty<string>(), null, "",
            "", OpenWithShell: true),
    };

    /// <summary>Le lanceur qui correspond à l'extension du fichier, ou null si le type n'est pas géré.</summary>
    public static RunnerDefinition? ForFile(string path)
    {
        var extension = Path.GetExtension(path);
        return All.FirstOrDefault(r => r.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Chemin complet de l'outil trouvé dans le PATH, ou null s'il est absent.</summary>
    public static string? FindTool(RunnerDefinition runner)
    {
        foreach (var tool in runner.Tools)
        {
            var found = FindInPath(tool);
            if (found != null) return found;
        }
        return null;
    }

    public static bool IsAvailable(RunnerDefinition runner) =>
        runner.OpenWithShell || FindTool(runner) != null;

    /// <summary>Remplace {file}, {dir}, {name} et {out} dans un modèle d'arguments.</summary>
    public static string Expand(string template, string sourceFile) => template
        .Replace("{file}", sourceFile)
        .Replace("{dir}", Path.GetDirectoryName(sourceFile) ?? "")
        .Replace("{name}", Path.GetFileNameWithoutExtension(sourceFile))
        .Replace("{out}", OutputPathFor(sourceFile));

    /// <summary>Où écrire l'exécutable produit : un dossier temporaire, pour ne pas polluer le projet.</summary>
    public static string OutputPathFor(string sourceFile)
    {
        var folder = Path.Combine(Path.GetTempPath(), "Kode", "build");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, Path.GetFileNameWithoutExtension(sourceFile) + ".exe");
    }

    // Cherche un programme dans les dossiers du PATH, avec les extensions de Windows (.exe, .cmd…)
    private static string? FindInPath(string tool)
    {
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawFolder in folders)
        {
            var folder = rawFolder.Trim().Trim('"');
            if (folder.Length == 0) continue;

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(folder, tool + extension);
                if (!File.Exists(candidate)) continue;

                // Windows crée de faux « python.exe » vides qui ouvrent le Microsoft Store :
                // on les écarte pour ne pas croire que Python est installé
                if (candidate.Contains("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase)
                    && new FileInfo(candidate).Length == 0)
                {
                    continue;
                }

                return candidate;
            }
        }
        return null;
    }
}