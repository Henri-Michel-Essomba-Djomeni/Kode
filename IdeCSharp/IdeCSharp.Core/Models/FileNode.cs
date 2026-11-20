namespace IdeCSharp.Core.Models;

/// <summary>Un élément de l'arborescence : fichier ou dossier.</summary>
public record FileNode(string Name, string FullPath, bool IsDirectory);
