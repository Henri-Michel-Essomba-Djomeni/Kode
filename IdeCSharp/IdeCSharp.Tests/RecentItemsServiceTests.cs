using IdeCSharp.Core.Services;

namespace IdeCSharp.Tests;

/// <summary>Tests des fichiers et dossiers récents (T29, Raïssa).</summary>
public class RecentItemsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _storage;

    public RecentItemsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "IdeCSharpRecent_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        // Dans un sous-dossier qui n'existe pas encore : le service doit le créer.
        _storage = Path.Combine(_root, "donnees", "recent.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private RecentItemsService NewService(int maxItems = 10) => new(_storage, maxItems);

    /// <summary>Crée un vrai fichier (les fichiers inexistants sont masqués par le service).</summary>
    private string Touch(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Le_plus_recent_est_en_premier()
    {
        string a = Touch("a.cs");
        string b = Touch("b.cs");
        var service = NewService();

        service.AddFile(a);
        service.AddFile(b);

        Assert.Equal(new[] { b, a }, service.GetRecentFiles());
    }

    [Fact]
    public void Rouvrir_un_fichier_le_remonte_sans_doublon()
    {
        string a = Touch("a.cs");
        string b = Touch("b.cs");
        var service = NewService();

        service.AddFile(a);
        service.AddFile(b);
        service.AddFile(a); // on rouvre a

        Assert.Equal(new[] { a, b }, service.GetRecentFiles());
    }

    [Fact]
    public void La_liste_est_limitee_au_maximum()
    {
        var service = NewService(maxItems: 3);
        var paths = Enumerable.Range(1, 5).Select(i => Touch($"f{i}.cs")).ToList();

        foreach (var p in paths)
            service.AddFile(p);

        // On garde les 3 derniers, le plus récent d'abord.
        Assert.Equal(new[] { paths[4], paths[3], paths[2] }, service.GetRecentFiles());
    }

    [Fact]
    public void Les_recents_survivent_a_un_redemarrage()
    {
        string a = Touch("a.cs");
        string folder = Path.Combine(_root, "projet");
        Directory.CreateDirectory(folder);

        var first = NewService();
        first.AddFile(a);
        first.AddFolder(folder);

        var second = NewService(); // simule un nouveau lancement de l'IDE

        Assert.Equal(new[] { a }, second.GetRecentFiles());
        Assert.Equal(new[] { folder }, second.GetRecentFolders());
    }

    [Fact]
    public void Un_fichier_supprime_n_apparait_plus()
    {
        string a = Touch("a.cs");
        string b = Touch("b.cs");
        var service = NewService();
        service.AddFile(a);
        service.AddFile(b);

        File.Delete(a);

        Assert.Equal(new[] { b }, service.GetRecentFiles());
    }

    [Fact]
    public void Fichiers_et_dossiers_sont_separes()
    {
        string a = Touch("a.cs");
        string folder = Path.Combine(_root, "dossier");
        Directory.CreateDirectory(folder);
        var service = NewService();

        service.AddFile(a);
        service.AddFolder(folder);

        Assert.Equal(new[] { a }, service.GetRecentFiles());
        Assert.Equal(new[] { folder }, service.GetRecentFolders());
    }

    [Fact]
    public void Un_fichier_de_sauvegarde_abime_ne_fait_pas_planter()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storage)!);
        File.WriteAllText(_storage, "ceci n'est pas du json {{{");

        var service = NewService(); // ne doit pas lever d'exception

        Assert.Empty(service.GetRecentFiles());
        Assert.Empty(service.GetRecentFolders());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_chemin_vide_est_ignore(string emptyPath)
    {
        var service = NewService();

        service.AddFile(emptyPath);

        Assert.Empty(service.GetRecentFiles());
    }

    [Fact]
    public void Clear_vide_les_deux_listes_meme_apres_redemarrage()
    {
        string a = Touch("a.cs");
        string folder = Path.Combine(_root, "dossier");
        Directory.CreateDirectory(folder);
        var service = NewService();
        service.AddFile(a);
        service.AddFolder(folder);

        service.Clear();

        Assert.Empty(service.GetRecentFiles());
        Assert.Empty(service.GetRecentFolders());
        Assert.Empty(NewService().GetRecentFiles()); // vidé aussi sur le disque
    }
}
