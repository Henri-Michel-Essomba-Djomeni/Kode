namespace IdeCSharp.Core.Interfaces;

/// <summary>Contrat du module Éditeur (responsable : Valdez).</summary>
public interface IEditorService
{
    /// <summary>Ouvre un document dans un nouvel onglet (ou active l'onglet existant).</summary>
    void OpenDocument(string path, string content);

    /// <summary>Ferme l'onglet du document. Retourne false si l'utilisateur annule.</summary>
    bool CloseDocument(string path);

    string GetText(string path);
    void SetText(string path, string content);

    bool IsModified(string path);
    IReadOnlyList<string> GetModifiedDocuments();

    void Undo(string path);
    void Redo(string path);

    /// <summary>Place le curseur à une ligne (utilisé par les erreurs cliquables).</summary>
    void GoToLine(string path, int line, int column = 1);
}
