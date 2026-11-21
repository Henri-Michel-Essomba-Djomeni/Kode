using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Conteneur d'onglets pour les documents ouverts.
/// Chaque onglet héberge un <see cref="DocumentView"/>.
/// </summary>
public partial class EditorView : UserControl
{
    // Maintient les abonnements pour pouvoir s'en détacher proprement à la fermeture de l'onglet
    private readonly Dictionary<TabItem, EventHandler> _modifiedHandlers = new();

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

        // Construction de l'en-tête : nom du fichier + astérisque si modifié + bouton de fermeture
        tab.Header = CreateTabHeader(fullPath, tab, documentView);

        TabDocuments.Items.Add(tab);
        TabDocuments.SelectedItem = tab;
        UpdateVisibility();
        DeferFocusToActiveDocument();
        return tab;
    }

    /// <summary>
    /// Supprime l'onglet donné et active un onglet voisin si possible.
    /// </summary>
    internal void RemoveTab(TabItem tab)
    {
        // Désabonnement de l'événement ModifiedChanged
        if (_modifiedHandlers.TryGetValue(tab, out var handler))
        {
            if (tab.Content is DocumentView docView)
            {
                docView.ModifiedChanged -= handler;
            }
            _modifiedHandlers.Remove(tab);
        }

        int index = TabDocuments.Items.IndexOf(tab);
        TabDocuments.Items.Remove(tab);

        // Activer un onglet voisin
        if (TabDocuments.Items.Count > 0)
        {
            int newIndex = Math.Min(index, TabDocuments.Items.Count - 1);
            TabDocuments.SelectedIndex = newIndex;
        }

        UpdateVisibility();
        DeferFocusToActiveDocument();
    }

    /// <summary>
    /// Active l'onglet donné.
    /// </summary>
    internal void ActivateTab(TabItem tab)
    {
        TabDocuments.SelectedItem = tab;
        DeferFocusToActiveDocument();
    }

    /// <summary>
    /// Retourne l'onglet actuellement sélectionné, ou null.
    /// </summary>
    internal TabItem? ActiveTab => TabDocuments.SelectedItem as TabItem;

    /// <summary>
    /// Donne le focus à l'éditeur du document actif de manière différée,
    /// garantissant que les raccourcis clavier et le menu Édition s'y appliquent.
    /// Ne fait rien s'il n'y a aucun onglet.
    /// </summary>
    internal void DeferFocusToActiveDocument()
    {
        if (TabDocuments.Items.Count == 0)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ActiveTab?.Content is DocumentView docView)
            {
                docView.FocusEditor();
            }
        }), DispatcherPriority.Input);
    }

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

    private object CreateTabHeader(string fullPath, TabItem tab, DocumentView documentView)
    {
        string fileName = Path.GetFileName(fullPath);

        var textBlock = new TextBlock
        {
            Text = documentView.IsModified ? $"{fileName} *" : fileName,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, "PanelForegroundBrush");

        // Met à jour l'en-tête (ajout/retrait de l'astérisque) en fonction des modifications
        EventHandler modifiedHandler = (_, _) =>
        {
            textBlock.Text = documentView.IsModified ? $"{fileName} *" : fileName;
        };
        documentView.ModifiedChanged += modifiedHandler;
        _modifiedHandlers[tab] = modifiedHandler;

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
        if (e.Source == TabDocuments)
        {
            Service.NotifyActiveTabChanged();
            DeferFocusToActiveDocument();
        }
    }
}
