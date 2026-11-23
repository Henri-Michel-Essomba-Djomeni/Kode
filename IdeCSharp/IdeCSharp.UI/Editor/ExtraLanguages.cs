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

    private const string RustXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Rust" extensions=".rs" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="Green" />
  <Color name="String" foreground="Brown" />
  <Color name="Number" foreground="DarkBlue" />
  <Color name="Keyword" foreground="Blue" />
  <Color name="Function" foreground="DarkGoldenrod" />
  <RuleSet>
    <Span color="Comment" begin="//" />
    <Span color="Comment" multiline="true" begin="/\*" end="\*/" />
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Keywords color="Keyword">
      <Word>as</Word><Word>async</Word><Word>await</Word><Word>break</Word><Word>const</Word>
      <Word>continue</Word><Word>crate</Word><Word>dyn</Word><Word>else</Word><Word>enum</Word>
      <Word>extern</Word><Word>false</Word><Word>fn</Word><Word>for</Word><Word>if</Word>
      <Word>impl</Word><Word>in</Word><Word>let</Word><Word>loop</Word><Word>match</Word>
      <Word>mod</Word><Word>move</Word><Word>mut</Word><Word>pub</Word><Word>ref</Word>
      <Word>return</Word><Word>self</Word><Word>Self</Word><Word>static</Word><Word>struct</Word>
      <Word>super</Word><Word>trait</Word><Word>true</Word><Word>type</Word><Word>unsafe</Word>
      <Word>use</Word><Word>where</Word><Word>while</Word>
      <Word>i8</Word><Word>i16</Word><Word>i32</Word><Word>i64</Word><Word>i128</Word><Word>isize</Word>
      <Word>u8</Word><Word>u16</Word><Word>u32</Word><Word>u64</Word><Word>u128</Word><Word>usize</Word>
      <Word>f32</Word><Word>f64</Word><Word>bool</Word><Word>char</Word><Word>str</Word>
      <Word>String</Word><Word>Vec</Word><Word>Option</Word><Word>Result</Word><Word>Some</Word>
      <Word>None</Word><Word>Ok</Word><Word>Err</Word>
    </Keywords>
    <Rule color="Function">\b[a-zA-Z_][a-zA-Z0-9_]*!</Rule>
    <Rule color="Number">\b0[xX][0-9a-fA-F_]+|\b\d[\d_]*(\.\d+)?([eE][+-]?\d+)?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    private const string GoXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Go" extensions=".go" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="Green" />
  <Color name="String" foreground="Brown" />
  <Color name="Number" foreground="DarkBlue" />
  <Color name="Keyword" foreground="Blue" />
  <Color name="Function" foreground="DarkGoldenrod" />
  <RuleSet>
    <Span color="Comment" begin="//" />
    <Span color="Comment" multiline="true" begin="/\*" end="\*/" />
    <Span color="String" multiline="true">
      <Begin>`</Begin>
      <End>`</End>
    </Span>
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Span color="String">
      <Begin>'</Begin>
      <End>'</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Keywords color="Keyword">
      <Word>break</Word><Word>case</Word><Word>chan</Word><Word>const</Word><Word>continue</Word>
      <Word>default</Word><Word>defer</Word><Word>else</Word><Word>fallthrough</Word><Word>for</Word>
      <Word>func</Word><Word>go</Word><Word>goto</Word><Word>if</Word><Word>import</Word>
      <Word>interface</Word><Word>map</Word><Word>package</Word><Word>range</Word><Word>return</Word>
      <Word>select</Word><Word>struct</Word><Word>switch</Word><Word>type</Word><Word>var</Word>
      <Word>true</Word><Word>false</Word><Word>nil</Word><Word>iota</Word>
      <Word>bool</Word><Word>byte</Word><Word>error</Word><Word>float32</Word><Word>float64</Word>
      <Word>int</Word><Word>int8</Word><Word>int16</Word><Word>int32</Word><Word>int64</Word>
      <Word>rune</Word><Word>string</Word><Word>uint</Word><Word>uint8</Word><Word>uint16</Word>
      <Word>uint32</Word><Word>uint64</Word><Word>uintptr</Word>
    </Keywords>
    <Keywords color="Function">
      <Word>append</Word><Word>cap</Word><Word>close</Word><Word>copy</Word><Word>delete</Word>
      <Word>len</Word><Word>make</Word><Word>new</Word><Word>panic</Word><Word>print</Word>
      <Word>println</Word><Word>recover</Word>
    </Keywords>
    <Rule color="Number">\b0[xX][0-9a-fA-F_]+|\b\d[\d_]*(\.\d+)?([eE][+-]?\d+)?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    private const string RubyXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Ruby" extensions=".rb" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="Green" />
  <Color name="String" foreground="Brown" />
  <Color name="Number" foreground="DarkBlue" />
  <Color name="Keyword" foreground="Blue" />
  <Color name="Function" foreground="DarkGoldenrod" />
  <Color name="Variable" foreground="DarkCyan" />
  <RuleSet>
    <Span color="Comment" begin="#" />
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Span color="String">
      <Begin>'</Begin>
      <End>'</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Keywords color="Keyword">
      <Word>alias</Word><Word>and</Word><Word>begin</Word><Word>break</Word><Word>case</Word>
      <Word>class</Word><Word>def</Word><Word>do</Word><Word>else</Word><Word>elsif</Word>
      <Word>end</Word><Word>ensure</Word><Word>false</Word><Word>for</Word><Word>if</Word>
      <Word>in</Word><Word>module</Word><Word>next</Word><Word>nil</Word><Word>not</Word>
      <Word>or</Word><Word>redo</Word><Word>rescue</Word><Word>retry</Word><Word>return</Word>
      <Word>self</Word><Word>super</Word><Word>then</Word><Word>true</Word><Word>undef</Word>
      <Word>unless</Word><Word>until</Word><Word>when</Word><Word>while</Word><Word>yield</Word>
    </Keywords>
    <Keywords color="Function">
      <Word>puts</Word><Word>print</Word><Word>p</Word><Word>require</Word><Word>require_relative</Word>
      <Word>include</Word><Word>extend</Word><Word>attr_accessor</Word><Word>attr_reader</Word>
      <Word>attr_writer</Word><Word>lambda</Word><Word>proc</Word><Word>raise</Word>
    </Keywords>
    <Rule color="Variable">@@?[a-zA-Z_][a-zA-Z0-9_]*|\$[a-zA-Z_][a-zA-Z0-9_]*</Rule>
    <Rule color="Number">\b0[xX][0-9a-fA-F_]+|\b\d[\d_]*(\.\d+)?([eE][+-]?\d+)?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    private const string LuaXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Lua" extensions=".lua" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="Green" />
  <Color name="String" foreground="Brown" />
  <Color name="Number" foreground="DarkBlue" />
  <Color name="Keyword" foreground="Blue" />
  <Color name="Function" foreground="DarkGoldenrod" />
  <RuleSet>
    <Span color="Comment" multiline="true" begin="--\[\[" end="\]\]" />
    <Span color="Comment" begin="--" />
    <Span color="String" multiline="true" begin="\[\[" end="\]\]" />
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Span color="String">
      <Begin>'</Begin>
      <End>'</End>
      <RuleSet>
        <Span begin="\\" end="." />
      </RuleSet>
    </Span>
    <Keywords color="Keyword">
      <Word>and</Word><Word>break</Word><Word>do</Word><Word>else</Word><Word>elseif</Word>
      <Word>end</Word><Word>false</Word><Word>for</Word><Word>function</Word><Word>goto</Word>
      <Word>if</Word><Word>in</Word><Word>local</Word><Word>nil</Word><Word>not</Word>
      <Word>or</Word><Word>repeat</Word><Word>return</Word><Word>then</Word><Word>true</Word>
      <Word>until</Word><Word>while</Word>
    </Keywords>
    <Keywords color="Function">
      <Word>print</Word><Word>require</Word><Word>pairs</Word><Word>ipairs</Word><Word>type</Word>
      <Word>tostring</Word><Word>tonumber</Word><Word>setmetatable</Word><Word>getmetatable</Word>
      <Word>pcall</Word><Word>error</Word><Word>assert</Word><Word>select</Word>
      <Word>table</Word><Word>string</Word><Word>math</Word><Word>os</Word><Word>io</Word>
    </Keywords>
    <Rule color="Number">\b0[xX][0-9a-fA-F]+|\b\d+(\.\d+)?([eE][+-]?\d+)?</Rule>
  </RuleSet>
</SyntaxDefinition>
""";

    private const string BatchXshd = """
<?xml version="1.0"?>
<SyntaxDefinition name="Batch" extensions=".bat;.cmd" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="Green" />
  <Color name="String" foreground="Brown" />
  <Color name="Keyword" foreground="Blue" />
  <Color name="Function" foreground="DarkGoldenrod" />
  <Color name="Variable" foreground="DarkCyan" />
  <RuleSet ignoreCase="true">
    <Span color="Comment" begin="^\s*::" />
    <Span color="Comment" begin="\brem\b" />
    <Span color="String">
      <Begin>"</Begin>
      <End>"</End>
    </Span>
    <Keywords color="Keyword">
      <Word>if</Word><Word>else</Word><Word>for</Word><Word>in</Word><Word>do</Word>
      <Word>goto</Word><Word>call</Word><Word>exit</Word><Word>set</Word><Word>echo</Word>
      <Word>off</Word><Word>on</Word><Word>pause</Word><Word>cd</Word><Word>md</Word>
      <Word>rd</Word><Word>del</Word><Word>copy</Word><Word>move</Word><Word>ren</Word>
      <Word>type</Word><Word>cls</Word><Word>title</Word><Word>setlocal</Word>
      <Word>endlocal</Word><Word>shift</Word><Word>not</Word><Word>exist</Word>
      <Word>defined</Word><Word>errorlevel</Word><Word>start</Word><Word>equ</Word>
      <Word>neq</Word><Word>lss</Word><Word>leq</Word><Word>gtr</Word><Word>geq</Word>
    </Keywords>
    <Rule color="Function">^\s*:[a-zA-Z_][a-zA-Z0-9_]*</Rule>
    <Rule color="Variable">%[a-zA-Z0-9_~:\-\*]+%|%[0-9\*]|![a-zA-Z0-9_]+!</Rule>
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
            RegisterLanguage("Rust", new[] { ".rs" }, RustXshd);
            RegisterLanguage("Go", new[] { ".go" }, GoXshd);
            RegisterLanguage("Ruby", new[] { ".rb" }, RubyXshd);
            RegisterLanguage("Lua", new[] { ".lua" }, LuaXshd);
            RegisterLanguage("Batch", new[] { ".bat", ".cmd" }, BatchXshd);

            // Extensions du catalogue d'exécution que les définitions d'AvalonEdit ignorent
            AddExtensions("C++", ".cxx", ".hxx");
            AddExtensions("JavaScript", ".mjs", ".cjs");

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

    // Associe des extensions supplémentaires à une définition déjà enregistrée
    private static void AddExtensions(string name, params string[] extensions)
    {
        var definition = HighlightingManager.Instance.GetDefinition(name);
        if (definition != null)
            HighlightingManager.Instance.RegisterHighlighting(name, extensions, definition);
    }

}
