using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private MainWindow? _parentMainWindow;
    private string _lastSearchText = string.Empty;

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

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // Connexion des événements de la barre de recherche / remplacement
        FindBar.SearchChanged += OnFindBarSearchChanged;
        FindBar.FindRequested += OnFindBarFindRequested;
        FindBar.ReplaceRequested += OnFindBarReplaceRequested;
        FindBar.ReplaceAllRequested += OnFindBarReplaceAllRequested;
        FindBar.CloseRequested += OnFindBarCloseRequested;
    }

    // ----------------------------------------------------------------
    // Intégration des commandes de MainWindow
    // ----------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            // Se désabonner d'abord pour éviter tout double abonnement si Loaded se redéclenche
            mainWindow.CommandInvoked -= OnCommandInvoked;
            mainWindow.CommandInvoked += OnCommandInvoked;
            _parentMainWindow = mainWindow;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_parentMainWindow != null)
        {
            _parentMainWindow.CommandInvoked -= OnCommandInvoked;
            _parentMainWindow = null;
        }
        else if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.CommandInvoked -= OnCommandInvoked;
        }
    }

    private void OnCommandInvoked(ICommand command)
    {
        // Traitement des commandes d'édition : navigation et recherche / remplacement
        if (command == AppCommands.GoToLine)
        {
            ShowGoToLineDialog();
        }
        else if (command == ApplicationCommands.Find)
        {
            OpenFindReplace(replaceMode: false);
        }
        else if (command == ApplicationCommands.Replace)
        {
            OpenFindReplace(replaceMode: true);
        }
    }

    // ----------------------------------------------------------------
    // Recherche et remplacement
    // ----------------------------------------------------------------

    private void OpenFindReplace(bool replaceMode)
    {
        var window = Window.GetWindow(this) as MainWindow;

        if (ActiveTab?.Content is not DocumentView activeDoc || Service.ActiveDocumentPath == null)
        {
            window?.SetStatus("Aucun fichier ouvert.");
            return;
        }

        string initialText = activeDoc.SelectedText;
        if (string.IsNullOrEmpty(initialText) || initialText.Contains('\n') || initialText.Contains('\r'))
        {
            initialText = _lastSearchText;
        }

        FindBar.SetReadOnly(activeDoc.IsReadOnly);
        FindBar.Open(replaceMode, initialText);
        UpdateFindResults(activeDoc);
    }

    private void UpdateFindResults(DocumentView? doc = null)
    {
        doc ??= ActiveTab?.Content as DocumentView;
        if (doc == null || string.IsNullOrEmpty(FindBar.SearchText))
        {
            FindBar.SetResult(string.Empty);
            return;
        }

        _lastSearchText = FindBar.SearchText;
        var (index, total) = doc.GetMatchInfo(FindBar.SearchText, FindBar.MatchCase);

        if (total == -1)
        {
            FindBar.SetResult("Plus de 2 millions de caractères : décompte désactivé");
        }
        else if (total == 0)
        {
            FindBar.SetResult("Aucun résultat");
        }
        else if (index > 0)
        {
            FindBar.SetResult($"{index} sur {total}");
        }
        else
        {
            FindBar.SetResult($"{total} résultat{(total > 1 ? "s" : "")}");
        }
    }

    private void OnFindBarSearchChanged(object? sender, EventArgs e)
    {
        if (ActiveTab?.Content is DocumentView activeDoc)
        {
            _lastSearchText = FindBar.SearchText;
            if (!string.IsNullOrEmpty(FindBar.SearchText))
            {
                activeDoc.FindFromSelectionStart(FindBar.SearchText, FindBar.MatchCase);
            }
            UpdateFindResults(activeDoc);
        }
    }

    private void OnFindBarFindRequested(bool forward)
    {
        if (ActiveTab?.Content is DocumentView activeDoc)
        {
            _lastSearchText = FindBar.SearchText;
            bool found = activeDoc.FindNext(FindBar.SearchText, FindBar.MatchCase, forward);
            if (!found)
            {
                FindBar.SetResult("Aucun résultat");
            }
            else
            {
                UpdateFindResults(activeDoc);
            }
        }
    }

    private void OnFindBarReplaceRequested(object? sender, EventArgs e)
    {
        if (ActiveTab?.Content is DocumentView activeDoc)
        {
            activeDoc.ReplaceCurrent(FindBar.SearchText, FindBar.ReplaceText, FindBar.MatchCase);
            UpdateFindResults(activeDoc);
        }
    }

    private void OnFindBarReplaceAllRequested(object? sender, EventArgs e)
    {
        if (ActiveTab?.Content is DocumentView activeDoc)
        {
            int count = activeDoc.ReplaceAll(FindBar.SearchText, FindBar.ReplaceText, FindBar.MatchCase);
            FindBar.SetResult($"{count} remplacement{(count > 1 ? "s" : "")}");
        }
    }

    private void OnFindBarCloseRequested(object? sender, EventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        DeferFocusToActiveDocument();
    }

    // ----------------------------------------------------------------
    // Aller à la ligne
    // ----------------------------------------------------------------

    /// <summary>
    /// Affiche la boîte de dialogue Aller à la ligne pour le document actif.
    /// </summary>
    public void ShowGoToLineDialog()
    {
        var window = Window.GetWindow(this) as MainWindow;

        if (ActiveTab?.Content is not DocumentView activeDoc || Service.ActiveDocumentPath == null)
        {
            window?.SetStatus("Aucun fichier ouvert.");
            return;
        }

        var dialog = new GoToLineWindow(activeDoc.CurrentLine, activeDoc.LineCount)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.Line.HasValue)
        {
            Service.GoToLine(Service.ActiveDocumentPath, dialog.Line.Value, 1);
        }
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

        // Mise à jour du compteur de recherche si le texte change pendant que la barre est visible
        documentView.TextChanged += (_, _) =>
        {
            if (FindBar.Visibility == Visibility.Visible && ActiveTab?.Content == documentView)
            {
                UpdateFindResults(documentView);
            }
        };

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
        else
        {
            // Fermeture de la barre de recherche si le dernier onglet est fermé
            FindBar.Visibility = Visibility.Collapsed;
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

            if (FindBar.Visibility == Visibility.Visible)
            {
                if (ActiveTab?.Content is DocumentView activeDoc)
                {
                    FindBar.SetReadOnly(activeDoc.IsReadOnly);
                    UpdateFindResults(activeDoc);
                }
                else
                {
                    FindBar.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
