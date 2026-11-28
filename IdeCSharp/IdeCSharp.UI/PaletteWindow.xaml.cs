using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace IdeCSharp.UI;

/// <summary>Une ligne de la palette : un titre, une indication (raccourci ou chemin) et l'action.</summary>
public record PaletteItem(string Title, string? Hint, Action Run);

/// <summary>
/// Fenêtre de recherche rapide, utilisée pour la palette de commandes et l'ouverture de fichier.
/// Flèches pour naviguer, Entrée pour lancer, Échap ou clic ailleurs pour fermer.
/// </summary>
public partial class PaletteWindow : Window
{
    private readonly IReadOnlyList<PaletteItem> _items;
    private bool _closing;

    public PaletteWindow(IReadOnlyList<PaletteItem> items, string title, Window owner)
    {
        InitializeComponent();
        _items = items;
        Owner = owner;
        TitleText.Text = title.ToUpperInvariant();

        // En haut, au centre de la fenêtre principale
        bool maximized = owner.WindowState == WindowState.Maximized;
        double ownerLeft = maximized ? 0 : owner.Left;
        double ownerTop = maximized ? 0 : owner.Top;
        double ownerWidth = maximized ? SystemParameters.WorkArea.Width : owner.ActualWidth;
        Left = ownerLeft + (ownerWidth - Width) / 2;
        Top = ownerTop + 70;

        SourceInitialized += (_, _) => WindowStyler.SetDarkTitleBar(this, ThemeManager.IsDark);
        Loaded += (_, _) => { SearchBox.Focus(); Filter(); };

        // Un clic ailleurs ferme la palette
        Deactivated += (_, _) => CloseOnce();
    }

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    // Tous les mots tapés doivent se trouver dans le titre ou l'indication
    private void Filter()
    {
        var words = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var results = _items
            .Where(item => words.All(w =>
                item.Title.Contains(w, StringComparison.OrdinalIgnoreCase)
                || (item.Hint?.Contains(w, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(item => words.Length > 0 && item.Title.StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(item => item.Title.Length)
            .Take(50)
            .ToList();

        ResultList.ItemsSource = results;
        if (results.Count > 0) ResultList.SelectedIndex = 0;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) Filter();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: Move(1); e.Handled = true; break;
            case Key.Up: Move(-1); e.Handled = true; break;
            case Key.Enter: RunSelected(); e.Handled = true; break;
            case Key.Escape: CloseOnce(); e.Handled = true; break;
        }
    }

    private void Move(int step)
    {
        if (ResultList.Items.Count == 0) return;
        int index = Math.Clamp(ResultList.SelectedIndex + step, 0, ResultList.Items.Count - 1);
        ResultList.SelectedIndex = index;
        ResultList.ScrollIntoView(ResultList.SelectedItem);
    }

    private void OnListClick(object sender, MouseButtonEventArgs e)
    {
        // On ne réagit que si le clic tombe sur une ligne
        if (ItemsControl.ContainerFromElement(ResultList, e.OriginalSource as DependencyObject) is ListBoxItem)
            RunSelected();
    }

    // On ferme d'abord, puis on lance l'action quand la fenêtre principale a repris le focus
    private void RunSelected()
    {
        if (ResultList.SelectedItem is not PaletteItem item) return;

        var dispatcher = Owner.Dispatcher;
        CloseOnce();
        dispatcher.BeginInvoke(DispatcherPriority.Background, item.Run);
    }
}