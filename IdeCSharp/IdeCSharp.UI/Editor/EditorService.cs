using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using IdeCSharp.Core.Interfaces;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Implémentation du contrat <see cref="IEditorService"/>.
/// Gère l'ensemble des documents ouverts dans le conteneur d'onglets.
/// </summary>
public class EditorService : IEditorService
{
    private readonly EditorView _view;

    // Clé = chemin complet normalisé, valeur = onglet et vue du document
    private readonly Dictionary<string, (TabItem Tab, DocumentView Doc)> _documents = new(StringComparer.OrdinalIgnoreCase);

    // Chemin du document actif précédent, pour éviter les notifications redondantes
    private string? _previousActivePath;

    internal EditorService(EditorView view)
    {
        _view = view;
    }

    // ----------------------------------------------------------------
    // Normalisation du chemin (clé unique insensible à la casse)
    // ----------------------------------------------------------------

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    // ----------------------------------------------------------------
    // IEditorService.OpenDocument
    // ----------------------------------------------------------------

    /// <summary>
    /// Ouvre un document dans un nouvel onglet, ou active l'onglet existant
    /// sans toucher au contenu si le document est déjà ouvert.
    /// </summary>
    public void OpenDocument(string path, string content)
    {
        string key = NormalizePath(path);

        if (_documents.TryGetValue(key, out var existing))
        {
            // Le document est déjà ouvert : on active simplement son onglet
            _view.ActivateTab(existing.Tab);
            existing.Doc.FocusEditor();
            return;
        }

        // Création d'un nouveau document
        var docView = new DocumentView();

        var tab = _view.AddTab(key, docView);
        _documents[key] = (tab, docView);

        // Chargement du contenu (non modifié, historique vidé)
        docView.LoadText(content);
        docView.FocusEditor();

        // Écouter les changements de position du curseur
        docView.CaretPositionChanged += (line, col) =>
        {
            // Ne relayer que si ce document est l'onglet actif
            if (ActiveDocumentPath != null
                && string.Equals(ActiveDocumentPath, key, StringComparison.OrdinalIgnoreCase))
            {
                CaretPositionChanged?.Invoke(line, col);
            }
        };
    }

    // ----------------------------------------------------------------
    // IEditorService.CloseDocument
    // ----------------------------------------------------------------

    /// <summary>
    /// Ferme l'onglet du document. Retourne false si l'utilisateur annule.
    /// </summary>
    public bool CloseDocument(string path)
    {
        string key = NormalizePath(path);

        if (!_documents.TryGetValue(key, out var entry))
            return true; // Pas ouvert, rien à faire

        // T19 : demander confirmation si le document est modifié
        if (!ConfirmClose(key, entry.Doc))
            return false;

        _documents.Remove(key);
        _view.RemoveTab(entry.Tab);
        return true;
    }

    /// <summary>
    /// Vérifie si la fermeture est autorisée.
    /// Retourne toujours true pour l'instant.
    /// </summary>
    // T19 : demander confirmation si le document est modifié
    private bool ConfirmClose(string key, DocumentView doc)
    {
        return true;
    }

    // ----------------------------------------------------------------
    // IEditorService.GetText / SetText
    // ----------------------------------------------------------------

    /// <summary>
    /// Retourne le texte du document ouvert.
    /// Lève une exception si le document n'est pas ouvert.
    /// </summary>
    public string GetText(string path)
    {
        string key = NormalizePath(path);

        if (!_documents.TryGetValue(key, out var entry))
            throw new InvalidOperationException($"Le document n'est pas ouvert : {path}");

        return entry.Doc.Text;
    }

    /// <summary>
    /// Remplace le contenu du document via LoadText (non modifié, historique vidé).
    /// Lève une exception si le document n'est pas ouvert.
    /// </summary>
    public void SetText(string path, string content)
    {
        string key = NormalizePath(path);

        if (!_documents.TryGetValue(key, out var entry))
            throw new InvalidOperationException($"Le document n'est pas ouvert : {path}");

        entry.Doc.LoadText(content);
    }

    // ----------------------------------------------------------------
    // IEditorService.IsModified / GetModifiedDocuments
    // ----------------------------------------------------------------

    /// <summary>
    /// Indique si le document est modifié. Retourne false si non ouvert.
    /// </summary>
    public bool IsModified(string path)
    {
        string key = NormalizePath(path);
        return _documents.TryGetValue(key, out var entry) && entry.Doc.IsModified;
    }

    /// <summary>
    /// Retourne la liste des chemins complets des documents modifiés.
    /// </summary>
    public IReadOnlyList<string> GetModifiedDocuments()
    {
        return _documents
            .Where(kv => kv.Value.Doc.IsModified)
            .Select(kv => kv.Key)
            .ToList()
            .AsReadOnly();
    }

    // ----------------------------------------------------------------
    // IEditorService.Undo / Redo
    // ----------------------------------------------------------------

    /// <summary>
    /// Annule la dernière action dans le document. Ne fait rien si non ouvert.
    /// </summary>
    public void Undo(string path)
    {
        string key = NormalizePath(path);
        if (_documents.TryGetValue(key, out var entry))
            entry.Doc.Undo();
    }

    /// <summary>
    /// Rétablit la dernière action annulée dans le document. Ne fait rien si non ouvert.
    /// </summary>
    public void Redo(string path)
    {
        string key = NormalizePath(path);
        if (_documents.TryGetValue(key, out var entry))
            entry.Doc.Redo();
    }

    // ----------------------------------------------------------------
    // IEditorService.GoToLine
    // ----------------------------------------------------------------

    /// <summary>
    /// Active l'onglet du document et place le curseur à la ligne et colonne données.
    /// Ne fait rien si le document n'est pas ouvert.
    /// </summary>
    public void GoToLine(string path, int line, int column = 1)
    {
        string key = NormalizePath(path);

        if (!_documents.TryGetValue(key, out var entry))
            return;

        _view.ActivateTab(entry.Tab);
        entry.Doc.GoTo(line, column);
    }

    // ----------------------------------------------------------------
    // Membres publics supplémentaires (hors interface)
    // ----------------------------------------------------------------

    /// <summary>
    /// Chemin complet du document actuellement actif, ou null si aucun onglet.
    /// </summary>
    public string? ActiveDocumentPath
    {
        get
        {
            var tab = _view.ActiveTab;
            return tab?.Tag as string;
        }
    }

    /// <summary>
    /// Déclenché quand le document actif change (nouveau chemin, ou null si plus d'onglet).
    /// </summary>
    public event Action<string?>? ActiveDocumentChanged;

    /// <summary>
    /// Déclenché quand la position du curseur change dans le document actif (ligne, colonne).
    /// Destiné à la barre d'état.
    /// </summary>
    public event Action<int, int>? CaretPositionChanged;

    /// <summary>
    /// Marque le document comme sauvegardé (non modifié).
    /// Ne fait rien si le document n'est pas ouvert.
    /// </summary>
    public void MarkAsSaved(string path)
    {
        string key = NormalizePath(path);
        if (_documents.TryGetValue(key, out var entry))
            entry.Doc.MarkAsSaved();
    }

    // ----------------------------------------------------------------
    // Notification interne de changement d'onglet actif
    // ----------------------------------------------------------------

    /// <summary>
    /// Appelé par EditorView quand l'onglet sélectionné change.
    /// </summary>
    internal void NotifyActiveTabChanged()
    {
        string? currentPath = ActiveDocumentPath;

        // Éviter les notifications redondantes
        if (string.Equals(_previousActivePath, currentPath, StringComparison.OrdinalIgnoreCase))
            return;

        _previousActivePath = currentPath;
        ActiveDocumentChanged?.Invoke(currentPath);
    }
}
