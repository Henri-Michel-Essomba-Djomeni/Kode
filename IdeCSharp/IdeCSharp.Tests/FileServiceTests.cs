using System.Text;
using IdeCSharp.Core.Services;

namespace IdeCSharp.Tests;

/// <summary>
/// Tests automatiques du module Fichiers (Raïssa).
/// Chaque test travaille dans un dossier temporaire à lui, supprimé à la fin,
/// pour ne jamais toucher aux vrais fichiers de la machine.
/// </summary>
public class FileServiceTests : IDisposable
{
    private readonly string _root;
    private readonly FileService _service = new();

    public FileServiceTests()
    {
        // xUnit crée une nouvelle instance de la classe pour CHAQUE test :
        // ce constructeur prépare donc un dossier propre à chaque fois.
        _root = Path.Combine(Path.GetTempPath(), "IdeCSharpTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        // Nettoyage après chaque test.
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string P(string name) => Path.Combine(_root, name);

    /// <summary>Collecte les valeurs de progression (synchrone, donc fiable dans un test).</summary>
    private sealed class SyncProgress : IProgress<double>
    {
        public List<double> Values { get; } = new();
        public void Report(double value) => Values.Add(value);
    }

    // ------------------------------------------------------------------
    // Lecture / écriture (T26)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Save_puis_Read_rend_le_meme_texte()
    {
        string text = "Bonjour le monde !\nÉcole, café, œuvre, 日本語\n";

        await _service.SaveFileAsync(P("a.txt"), text);
        string result = await _service.ReadFileAsync(P("a.txt"));

        Assert.Equal(text, result);
    }

    [Fact]
    public async Task Save_cree_les_dossiers_manquants()
    {
        string path = Path.Combine(_root, "sous", "dossier", "b.txt");

        await _service.SaveFileAsync(path, "contenu");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Save_ecrit_en_UTF8_sans_BOM()
    {
        await _service.SaveFileAsync(P("c.txt"), "abc");

        byte[] bytes = await File.ReadAllBytesAsync(P("c.txt"));

        Assert.Equal(new byte[] { 0x61, 0x62, 0x63 }, bytes); // pas de EF BB BF au début
    }

    [Fact]
    public async Task Read_fichier_inexistant_leve_une_exception()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.ReadFileAsync(P("nexiste-pas.txt")));
    }

    // ------------------------------------------------------------------
    // Encodage (T30)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Read_detecte_un_fichier_UTF16_grace_au_BOM()
    {
        string text = "Élève : été à Yaoundé";
        // UnicodeEncoding(false, true) = UTF-16 petit-boutiste AVEC BOM
        await File.WriteAllTextAsync(P("utf16.txt"), text, new UnicodeEncoding(false, true));

        string result = await _service.ReadFileAsync(P("utf16.txt"));

        Assert.Equal(text, result);
    }

    // ------------------------------------------------------------------
    // Gros fichiers : progression, annulation (T34)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Read_signale_une_progression_croissante_jusqu_a_1()
    {
        // ~1 Mo : plusieurs blocs de 64 Ko, donc plusieurs rapports de progression.
        await File.WriteAllTextAsync(P("gros.txt"), new string('a', 1_000_000));
        var progress = new SyncProgress();

        string result = await _service.ReadFileAsync(P("gros.txt"), progress);

        Assert.Equal(1_000_000, result.Length);
        Assert.NotEmpty(progress.Values);
        Assert.All(progress.Values, v => Assert.InRange(v, 0.0, 1.0));
        Assert.Equal(progress.Values.OrderBy(v => v), progress.Values); // jamais de recul
        Assert.Equal(1.0, progress.Values[^1]);                          // finit à 100 %
    }

    [Fact]
    public async Task Read_limite_les_rapports_de_progression_a_un_par_pourcent()
    {
        // 20 Mo = environ 320 blocs de 64 Ko : sans limite, il y aurait ~320 rapports.
        await File.WriteAllTextAsync(P("enorme.txt"), new string('c', 20_000_000));
        var progress = new SyncProgress();

        await _service.ReadFileAsync(P("enorme.txt"), progress);

        Assert.True(progress.Values.Count <= 101, $"Trop de rapports : {progress.Values.Count}");
        Assert.Equal(1.0, progress.Values[^1]);
    }

    [Fact]
    public async Task Read_est_annulable()
    {
        await File.WriteAllTextAsync(P("gros2.txt"), new string('b', 1_000_000));
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // l'utilisateur a déjà cliqué sur « Annuler »

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ReadFileAsync(P("gros2.txt"), null, cts.Token));
    }

    [Fact]
    public async Task Read_fichier_vide_rend_une_chaine_vide()
    {
        await File.WriteAllTextAsync(P("vide.txt"), "");

        string result = await _service.ReadFileAsync(P("vide.txt"));

        Assert.Equal("", result);
    }

    [Fact]
    public void IsLargeFile_respecte_le_seuil()
    {
        var service = new FileService(largeFileThresholdBytes: 100);
        File.WriteAllText(P("petit.txt"), new string('x', 50));
        File.WriteAllText(P("grand.txt"), new string('x', 200));

        Assert.False(service.IsLargeFile(P("petit.txt")));
        Assert.True(service.IsLargeFile(P("grand.txt")));
        Assert.False(service.IsLargeFile(P("nexiste-pas.txt"))); // pas d'exception
    }

    // ------------------------------------------------------------------
    // Arborescence (T27)
    // ------------------------------------------------------------------

    [Fact]
    public void ListDirectory_met_les_dossiers_d_abord_puis_trie_par_nom()
    {
        File.WriteAllText(P("b.txt"), "");
        File.WriteAllText(P("A.cs"), "");
        Directory.CreateDirectory(P("zeta"));
        Directory.CreateDirectory(P("alpha"));

        var names = _service.ListDirectory(_root).Select(n => n.Name).ToList();

        Assert.Equal(new[] { "alpha", "zeta", "A.cs", "b.txt" }, names);
    }

    [Fact]
    public void ListDirectory_renseigne_IsDirectory_et_FullPath()
    {
        File.WriteAllText(P("f.txt"), "");
        Directory.CreateDirectory(P("d"));

        var nodes = _service.ListDirectory(_root);

        var folder = Assert.Single(nodes, n => n.Name == "d");
        var file = Assert.Single(nodes, n => n.Name == "f.txt");
        Assert.True(folder.IsDirectory);
        Assert.False(file.IsDirectory);
        Assert.Equal(P("f.txt"), file.FullPath);
    }

    [Fact]
    public void ListDirectory_masque_les_elements_caches()
    {
        File.WriteAllText(P("visible.txt"), "");
        File.WriteAllText(P("cache.txt"), "");
        File.SetAttributes(P("cache.txt"), FileAttributes.Hidden);

        var names = _service.ListDirectory(_root).Select(n => n.Name).ToList();

        Assert.Contains("visible.txt", names);
        Assert.DoesNotContain("cache.txt", names);
    }

    [Fact]
    public void ListDirectory_dossier_inexistant_leve_une_exception()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _service.ListDirectory(P("fantome")));
    }

    // ------------------------------------------------------------------
    // Créer / renommer / supprimer (T28)
    // ------------------------------------------------------------------

    [Fact]
    public void CreateFile_cree_un_fichier_vide()
    {
        _service.CreateFile(P("nouveau.cs"));

        Assert.True(File.Exists(P("nouveau.cs")));
        Assert.Equal(0, new FileInfo(P("nouveau.cs")).Length);
    }

    [Fact]
    public void CreateFile_n_ecrase_jamais_un_fichier_existant()
    {
        File.WriteAllText(P("important.txt"), "precieux");

        Assert.Throws<IOException>(() => _service.CreateFile(P("important.txt")));

        Assert.Equal("precieux", File.ReadAllText(P("important.txt"))); // intact
    }

    [Fact]
    public void CreateFolder_cree_le_dossier()
    {
        _service.CreateFolder(P("nouveau-dossier"));

        Assert.True(Directory.Exists(P("nouveau-dossier")));
    }

    [Fact]
    public void Rename_renomme_un_fichier()
    {
        File.WriteAllText(P("ancien.txt"), "x");

        _service.Rename(P("ancien.txt"), "nouveau.txt");

        Assert.False(File.Exists(P("ancien.txt")));
        Assert.True(File.Exists(P("nouveau.txt")));
    }

    [Fact]
    public void Rename_renomme_un_dossier()
    {
        Directory.CreateDirectory(P("dossierA"));

        _service.Rename(P("dossierA"), "dossierB");

        Assert.False(Directory.Exists(P("dossierA")));
        Assert.True(Directory.Exists(P("dossierB")));
    }

    [Fact]
    public void Rename_vers_un_nom_deja_pris_leve_une_exception()
    {
        File.WriteAllText(P("un.txt"), "1");
        File.WriteAllText(P("deux.txt"), "2");

        Assert.Throws<IOException>(() => _service.Rename(P("un.txt"), "deux.txt"));

        Assert.Equal("2", File.ReadAllText(P("deux.txt"))); // pas écrasé
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\\b.txt")]
    [InlineData("a/b.txt")]
    [InlineData("a<b>.txt")]
    public void Rename_refuse_les_noms_invalides(string badName)
    {
        File.WriteAllText(P("source.txt"), "x");

        Assert.Throws<ArgumentException>(() => _service.Rename(P("source.txt"), badName));
    }

    [Fact]
    public void Delete_supprime_un_fichier()
    {
        File.WriteAllText(P("a-supprimer.txt"), "x");

        _service.Delete(P("a-supprimer.txt"));

        Assert.False(File.Exists(P("a-supprimer.txt")));
    }

    [Fact]
    public void Delete_supprime_un_dossier_avec_son_contenu()
    {
        Directory.CreateDirectory(P("plein"));
        File.WriteAllText(Path.Combine(P("plein"), "dedans.txt"), "x");

        _service.Delete(P("plein"));

        Assert.False(Directory.Exists(P("plein")));
    }

    [Fact]
    public void Delete_element_inexistant_leve_une_exception()
    {
        Assert.Throws<FileNotFoundException>(() => _service.Delete(P("rien")));
    }
}
