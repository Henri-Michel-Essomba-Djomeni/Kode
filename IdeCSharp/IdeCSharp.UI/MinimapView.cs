using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace IdeCSharp.UI
{
    /// <summary>
    /// Aperçu réduit du fichier, comme la « minimap » de VS Code : chaque ligne est dessinée par
    /// de petits traits qui suivent la longueur des mots. Un clic ou un glissé demande à
    /// l'éditeur d'aller à la ligne correspondante.
    /// </summary>
    public class MinimapView : FrameworkElement
    {
        private const double MaxRowHeight = 3;     // hauteur maximale d'une ligne, en pixels
        private const double CharWidth = 1;        // largeur d'un caractère, en pixels
        private const int MaxColumns = 240;        // on ne dessine pas au-delà de cette colonne
        private const int MaxChars = 5_000_000;    // au-delà, l'aperçu est désactivé

        private string[] _lines = Array.Empty<string>();
        private int _firstVisibleLine;
        private int _visibleLineCount;

        /// <summary>Levé quand l'utilisateur clique ou glisse : numéro de ligne demandé (à partir de 1).</summary>
        public event Action<int>? LineRequested;

        public MinimapView()
        {
            ClipToBounds = true;
            Loaded += (_, _) => ThemeManager.ThemeChanged += OnThemeChanged;
            Unloaded += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        // Les couleurs viennent du thème : on redessine quand il change
        private void OnThemeChanged(bool dark) => InvalidateVisual();

        /// <summary>Le texte à représenter. Un texte vide ou trop gros efface l'aperçu.</summary>
        public void SetText(string? text)
        {
            _lines = string.IsNullOrEmpty(text) || text.Length > MaxChars
                ? Array.Empty<string>()
                : text.Split('\n');
            InvalidateVisual();
        }

        /// <summary>La partie visible dans l'éditeur (première ligne à partir de 1, et nombre de lignes).</summary>
        public void SetViewport(int firstVisibleLine, int visibleLineCount)
        {
            _firstVisibleLine = firstVisibleLine;
            _visibleLineCount = visibleLineCount;
            InvalidateVisual();
        }

        // Si le fichier est court, chaque ligne fait 3 px ; s'il est long, tout est réduit pour tenir
        private double RowHeight =>
            _lines.Length == 0 ? MaxRowHeight : Math.Min(MaxRowHeight, ActualHeight / _lines.Length);

        protected override void OnRender(DrawingContext dc)
        {
            // Rectangle invisible : sans lui, la souris ne détecte pas les zones vides
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            if (_lines.Length == 0 || ActualHeight <= 0) return;

            var textBrush = (Brush)(TryFindResource("MutedForegroundBrush") ?? Brushes.Gray);
            var accentBrush = (Brush)(TryFindResource("AccentBrush") ?? Brushes.MediumPurple);

            double rowHeight = RowHeight;

            // Fichier très long : on ne dessine qu'une ligne sur « step », sinon on dessinerait
            // des milliers de traits invisibles
            int step = rowHeight >= 1 ? 1 : (int)Math.Ceiling(1.0 / rowHeight);
            double barHeight = Math.Max(1.0, rowHeight * step * 0.7);

            dc.PushOpacity(0.55);
            for (int i = 0; i < _lines.Length; i += step)
            {
                DrawLine(dc, textBrush, _lines[i], i * rowHeight, barHeight);
            }
            dc.Pop();

            // Zone actuellement visible dans l'éditeur
            if (_visibleLineCount > 0)
            {
                double top = (_firstVisibleLine - 1) * rowHeight;
                double height = Math.Max(4, _visibleLineCount * rowHeight);
                dc.PushOpacity(0.2);
                dc.DrawRectangle(accentBrush, null, new Rect(0, top, ActualWidth, height));
                dc.Pop();
            }
        }

        // Un trait par mot : sa position suit l'indentation, sa longueur celle du mot
        private static void DrawLine(DrawingContext dc, Brush brush, string line, double y, double barHeight)
        {
            int column = 0;       // colonne courante (une tabulation vaut 4 colonnes)
            int wordStart = -1;
            int limit = Math.Min(line.Length, MaxColumns);

            for (int c = 0; c <= limit; c++)
            {
                // Un espace fictif en fin de ligne permet de fermer le dernier mot
                char ch = c < limit ? line[c] : ' ';
                bool isSpace = char.IsWhiteSpace(ch);

                if (!isSpace && wordStart < 0)
                {
                    wordStart = column;
                }
                else if (isSpace && wordStart >= 0)
                {
                    dc.DrawRectangle(brush, null,
                        new Rect(4 + wordStart * CharWidth, y, (column - wordStart) * CharWidth, barHeight));
                    wordStart = -1;
                }

                column += ch == '\t' ? 4 : 1;
            }
        }

        // ------------------------------------------------------------------
        // Souris : clic ou glissé pour aller à une ligne
        // ------------------------------------------------------------------

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            CaptureMouse();   // on continue à suivre la souris même si elle sort du cadre
            RequestLine(e.GetPosition(this).Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (IsMouseCaptured) RequestLine(e.GetPosition(this).Y);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => ReleaseMouseCapture();

        private void RequestLine(double y)
        {
            if (_lines.Length == 0) return;
            int line = (int)(y / RowHeight) + 1;
            LineRequested?.Invoke(Math.Clamp(line, 1, _lines.Length));
        }
    }
}