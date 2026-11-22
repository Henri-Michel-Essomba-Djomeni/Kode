using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Enregistre les définitions de coloration syntaxique supplémentaires (Python, JSON)
/// directement depuis des chaînes XSHD intégrées au code source.
/// </summary>
public static class ExtraLanguages
{
    private static bool _isRegistered;
    private static readonly object _lock = new();

    private const string PythonXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Python" extensions=".py;.pyw" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="Green" />
  <Color name="String" foreground="Brown" />
  <Color name="Number" foreground="DarkBlue" />
  <Color name="Keyword" foreground="Blue" />
  <RuleSet>
    <Span color="Comment" begin="#" />
    <Span color="String" multiline="true">
      <Begin>&quot;&quot;&quot;</Begin>
      <End>&quot;&quot;&quot;</End>
    </Span>
    <Span color="String" multiline="true">
      <Begin>'''</Begin>
      <End>'''</End>
    </Span>
    <Span color="String">
      <Begin>"(?!"")</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Span color="String">
      <Begin>'(?!'')</Begin>
      <End>'</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Keywords color="Keyword">
      <Word>and</Word><Word>as</Word><Word>assert</Word><Word>async</Word><Word>await</Word>
      <Word>break</Word><Word>class</Word><Word>continue</Word><Word>def</Word><Word>del</Word>
      <Word>elif</Word><Word>else</Word><Word>except</Word><Word>False</Word><Word>finally</Word>
      <Word>for</Word><Word>from</Word><Word>global</Word><Word>if</Word><Word>import</Word>
      <Word>in</Word><Word>is</Word><Word>lambda</Word><Word>None</Word><Word>nonlocal</Word>
      <Word>not</Word><Word>or</Word><Word>pass</Word><Word>raise</Word><Word>return</Word>
      <Word>True</Word><Word>try</Word><Word>while</Word><Word>with</Word><Word>yield</Word>
    </Keywords>
    <Rule color="Number">\b0[xX][0-9a-fA-F_]+|\b\d[\d_]*(\.\d+)?([eE][+-]?\d+)?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    private const string JsonXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="JSON" extensions=".json;.geojson" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Property" foreground="DarkBlue" />
  <Color name="String" foreground="Brown" />
  <Color name="Number" foreground="DarkGreen" />
  <Color name="Keyword" foreground="Blue" />
  <RuleSet>
    <Span color="Property">
      <Begin>"(?=(?:[^"\\]|\\.)*"\s*:)</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Keywords color="Keyword">
      <Word>true</Word><Word>false</Word><Word>null</Word>
    </Keywords>
    <Rule color="Number">-?\d+(\.\d+)?([eE][+-]?\d+)?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    /// <summary>
    /// Charge et enregistre les définitions de syntaxe pour Python et JSON.
    /// Méthode idempotente et thread-safe.
    /// </summary>
    public static void Register()
    {
        if (_isRegistered)
            return;

        lock (_lock)
        {
            if (_isRegistered)
                return;

            RegisterLanguage("Python", new[] { ".py", ".pyw" }, PythonXshd);
            RegisterLanguage("JSON", new[] { ".json", ".geojson" }, JsonXshd);

            _isRegistered = true;
        }
    }

    private static void RegisterLanguage(string name, string[] extensions, string xshd)
    {
        using var stringReader = new StringReader(xshd);
        using var xmlReader = XmlReader.Create(stringReader);
        var definition = HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
        HighlightingManager.Instance.RegisterHighlighting(name, extensions, definition);
    }
}
