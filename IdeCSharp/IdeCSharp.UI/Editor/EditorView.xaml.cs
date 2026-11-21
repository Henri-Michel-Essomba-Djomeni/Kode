using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Conteneur d'onglets pour les documents ouverts.
/// Chaque onglet héberge un <see cref="DocumentView"/>.
/// </summary>
public partial class EditorView : UserControl
{
    /// <summary>
    /// Service gérant les opérations sur les documents (implémente IEditorService).
    /// </summary>
    public EditorService Service { get; }

    /// <summary>
    /// Initialise le conteneur d'onglets et crée le service associé.
    /// </summary>
    public EditorView()
    {
        InitializeComponent();
        Service = new EditorService(this);
    }

    // ----------------------------------------------------------------
    // Gestion interne des onglets (appelée par EditorService)
    // ----------------------------------------------------------------

    /// <summary>
    /// Ajoute un onglet pour le document donné et l'active.
    /// </summary>
    internal TabItem AddTab(string fullPath, DocumentView documentView)
    {
        var tab = new TabItem
        {
            Content = documentView,
            Tag = fullPath,
            ToolTip = fullPath
        };

        // Construction de l'en-tête : nom du fichier + bouton de fermeture
        tab.Header = CreateTabHeader(fullPath, tab);

        TabDocuments.Items.Add(tab);
        TabDocuments.SelectedItem = tab;
        UpdateVisibility();
        return tab;
    }

    /// <summary>
    /// Supprime l'onglet donné et active un onglet voisin si possible.
    /// </summary>
    internal void RemoveTab(TabItem tab)
    {
        int index = TabDocuments.Items.IndexOf(tab);
        TabDocuments.Items.Remove(tab);

        // Activer un onglet voisin
        if (TabDocuments.Items.Count > 0)
        {
            int newIndex = Math.Min(index, TabDocuments.Items.Count - 1);
            TabDocuments.SelectedIndex = newIndex;
        }

        UpdateVisibility();
    }

    /// <summary>
    /// Active l'onglet donné.
    /// </summary>
    internal void ActivateTab(TabItem tab)
    {
        TabDocuments.SelectedItem = tab;
    }

    /// <summary>
    /// Retourne l'onglet actuellement sélectionné, ou null.
    /// </summary>
    internal TabItem? ActiveTab => TabDocuments.SelectedItem as TabItem;

    // ----------------------------------------------------------------
    // Visibilité : message vide ou onglets
    // ----------------------------------------------------------------

    private void UpdateVisibility()
    {
        bool hasItems = TabDocuments.Items.Count > 0;
        TabDocuments.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyMessage.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    // ----------------------------------------------------------------
    // En-tête d'onglet avec bouton de fermeture
    // ----------------------------------------------------------------

    private object CreateTabHeader(string fullPath, TabItem tab)
    {
        string fileName = Path.GetFileName(fullPath);

        var textBlock = new TextBlock
        {
            Text = fileName,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("PanelForegroundBrush")
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, "PanelForegroundBrush");

        var closeButton = new Button
        {
            Content = "✕",
            FontSize = 10,
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        closeButton.SetResourceReference(ForegroundProperty, "MutedForegroundBrush");
        closeButton.Click += (_, _) =>
        {
            string path = (string)tab.Tag;
            Service.CloseDocument(path);
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(textBlock);
        panel.Children.Add(closeButton);
        return panel;
    }

    // ----------------------------------------------------------------
    // Changement d'onglet actif
    // ----------------------------------------------------------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Service.NotifyActiveTabChanged();
    }
}
