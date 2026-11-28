using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IdeCSharp.Core.Accounts;

/// <summary>Un dépôt public GitHub.</summary>
public record GitHubRepo(string Name, string? Description, string? Language, string CloneUrl);

/// <summary>Liste les dépôts publics d'un pseudo GitHub (aucun jeton nécessaire).</summary>
public static class GitHubRepoService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kode-IDE");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<IReadOnlyList<GitHubRepo>> ListPublicReposAsync(string login, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"https://api.github.com/users/{Uri.EscapeDataString(login)}/repos?per_page=100&sort=updated";
            using var response = await Http.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException("Compte GitHub introuvable.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new InvalidOperationException(
                    "GitHub limite les requêtes pour le moment. Réessaie dans quelques minutes ou colle l'URL du dépôt.");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

            var repos = new List<GitHubRepo>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                repos.Add(new GitHubRepo(
                    item.GetProperty("name").GetString()!,
                    item.TryGetProperty("description", out var d) ? d.GetString() : null,
                    item.TryGetProperty("language", out var l) ? l.GetString() : null,
                    item.GetProperty("clone_url").GetString()!));
            }
            return repos;
        }
        catch (HttpRequestException)
        {
            throw new GitHubNetworkException("Impossible de joindre GitHub. Vérifie ta connexion internet.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubNetworkException("GitHub met trop de temps à répondre. Réessaie.");
        }
    }
}