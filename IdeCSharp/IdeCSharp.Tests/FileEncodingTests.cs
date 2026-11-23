using System.Text;
using IdeCSharp.Core.Models;
using IdeCSharp.Core.Services;

namespace IdeCSharp.Tests;

/// <summary>Tests de la détection et du choix de l'encodage (T30, Raïssa).</summary>
public class FileEncodingTests : IDisposable
{
    private readonly string _root;
    private readonly FileService _service = new();

    public FileEncodingTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "IdeCSharpEnc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string P(string name) => Path.Combine(_root, name);

    private sealed class SyncProgress : IProgress<double>
    {
        public List<double> Values { get; } = new();
        public void Report(double value) => Values.Add(value);
    }

    // ------------------------------------------------------------------
    // Détection à la lecture
    // ------------------------------------------------------------------

    [Fact]
    public async Task UTF8_sans_BOM_est_detecte()
    {
        // « café » en UTF-8 : c, a, f, puis C3 A9 (le é sur deux octets)
        await File.WriteAllBytesAsync(P("a.txt"), new byte[] { 0x63, 0x61, 0x66, 0xC3, 0xA9 });

        var result = await _service.ReadFileWithEncodingAsync(P("a.txt"));

        Assert.Equal("café", result.Content);
        Assert.Equal(FileEncodings.Utf8, result.EncodingName);
    }

    [Fact]
    public async Task UTF8_avec_BOM_est_detecte_et_le_BOM_n_apparait_pas_dans_le_texte()
    {
        await File.WriteAllTextAsync(P("b.txt"), "café", new UTF8Encoding(true));

        var result = await _service.ReadFileWithEncodingAsync(P("b.txt"));

        Assert.Equal("café", result.Content); // pas de caractère invisible U+FEFF au début
        Assert.Equal(FileEncodings.Utf8Bom, result.EncodingName);
    }

    [Fact]
    public async Task Un_fichier_ASCII_pur_est_considere_comme_UTF8()
    {
        await File.WriteAllTextAsync(P("c.txt"), "Hello", new UTF8Encoding(false));

        var result = await _service.ReadFileWithEncodingAsync(P("c.txt"));

        Assert.Equal("Hello", result.Content);
        Assert.Equal(FileEncodings.Utf8, result.EncodingName);
    }

    [Fact]
    public async Task Un_ancien_fichier_ANSI_avec_accents_est_relu_en_Windows1252()
    {
        // « café été » en Windows-1252 : é = E9 (un seul octet). C'est invalide en UTF-8.
        byte[] ansi = { 0x63, 0x61, 0x66, 0xE9, 0x20, 0xE9, 0x74, 0xE9 };
        await File.WriteAllBytesAsync(P("ansi.txt"), ansi);

        var result = await _service.ReadFileWithEncodingAsync(P("ansi.txt"));

        Assert.Equal("café été", result.Content);
        Assert.Equal(FileEncodings.Windows1252, result.EncodingName);
    }

    [Fact]
    public async Task La_progression_ne_recule_jamais_meme_apres_un_retour_en_Windows1252()
    {
        // 300 Ko de « a », avec un octet E9 suivi de « a » vers 97 % du fichier : l'UTF-8 échoue
        // presque à la fin, puis la lecture recommence en Windows-1252.
        byte[] data = new byte[300_000];
        Array.Fill(data, (byte)'a');
        data[290_000] = 0xE9;
        await File.WriteAllBytesAsync(P("tardif.txt"), data);
        var progress = new SyncProgress();

        var result = await _service.ReadFileWithEncodingAsync(P("tardif.txt"), progress);

        Assert.Equal(FileEncodings.Windows1252, result.EncodingName);
        Assert.Equal(300_000, result.Content.Length);
        Assert.Equal('é', result.Content[290_000]);
        Assert.Equal(progress.Values.OrderBy(v => v), progress.Values); // jamais de recul
        Assert.Equal(1.0, progress.Values[^1]);
    }

    // ------------------------------------------------------------------
    // Écriture dans un encodage choisi, puis relecture
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(FileEncodings.Utf8)]
    [InlineData(FileEncodings.Utf8Bom)]
    [InlineData(FileEncodings.Utf16Le)]
    [InlineData(FileEncodings.Utf16Be)]
    [InlineData(FileEncodings.Utf32Le)]
    [InlineData(FileEncodings.Utf32Be)]
    [InlineData(FileEncodings.Windows1252)]
    public async Task Enregistrer_puis_relire_rend_le_meme_texte_et_le_meme_encodage(string encodingName)
    {
        string text = "Élève à l'école : été 2026\nDeuxième ligne";

        await _service.SaveFileWithEncodingAsync(P("aller-retour.txt"), text, encodingName);
        var result = await _service.ReadFileWithEncodingAsync(P("aller-retour.txt"));

        Assert.Equal(text, result.Content);
        Assert.Equal(encodingName, result.EncodingName);
    }

    [Fact]
    public async Task UTF8_BOM_ecrit_bien_les_trois_octets_du_BOM()
    {
        await _service.SaveFileWithEncodingAsync(P("bom.txt"), "a", FileEncodings.Utf8Bom);

        byte[] bytes = await File.ReadAllBytesAsync(P("bom.txt"));

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0x61 }, bytes);
    }

    [Fact]
    public async Task Un_nom_d_encodage_inconnu_est_refuse_sans_creer_de_fichier()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveFileWithEncodingAsync(P("x.txt"), "abc", "KLINGON"));

        Assert.False(File.Exists(P("x.txt")));
    }

    [Fact]
    public async Task Un_caractere_impossible_dans_l_encodage_ne_detruit_pas_le_fichier()
    {
        await File.WriteAllTextAsync(P("ancien.txt"), "ancien contenu");

        // « 日本 » n'existe pas en Windows-1252.
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _service.SaveFileWithEncodingAsync(P("ancien.txt"), "日本", FileEncodings.Windows1252));

        Assert.Equal("ancien contenu", await File.ReadAllTextAsync(P("ancien.txt"))); // intact
    }
}
