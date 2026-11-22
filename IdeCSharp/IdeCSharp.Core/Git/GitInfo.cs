namespace IdeCSharp.Core.Git;

/// <summary>Infos Git lues directement dans le dossier .git, sans lancer git.</summary>
public static class GitInfo
{
    /// <summary>Nom de la branche du dossier (ou de l'un de ses parents), ou null si ce n'est pas un dépôt.</summary>
    public static string? GetBranch(string folder)
    {
        var dir = new DirectoryInfo(folder);
        while (dir != null)
        {
            var gitPath = Path.Combine(dir.FullName, ".git");

            if (Directory.Exists(gitPath))
            {
                return ReadHead(Path.Combine(gitPath, "HEAD"));
            }

            // Dépôt secondaire (worktree) : .git est un fichier qui pointe vers le vrai dossier
            if (File.Exists(gitPath))
            {
                try
                {
                    var line = File.ReadAllText(gitPath).Trim();
                    if (line.StartsWith("gitdir:", StringComparison.Ordinal))
                    {
                        var target = line["gitdir:".Length..].Trim();
                        if (!Path.IsPathRooted(target))
                        {
                            target = Path.GetFullPath(Path.Combine(dir.FullName, target));
                        }
                        return ReadHead(Path.Combine(target, "HEAD"));
                    }
                }
                catch (IOException) { return null; }
                catch (UnauthorizedAccessException) { return null; }
            }

            dir = dir.Parent;
        }
        return null;
    }

    private static string? ReadHead(string headFile)
    {
        try
        {
            if (!File.Exists(headFile)) return null;

            var text = File.ReadAllText(headFile).Trim();
            const string prefix = "ref: refs/heads/";
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text[prefix.Length..];
            }

            // HEAD détachée : on affiche le début du numéro de commit
            return text.Length >= 7 ? text[..7] : text;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}