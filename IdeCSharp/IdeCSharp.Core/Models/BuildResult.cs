namespace IdeCSharp.Core.Models;

/// <summary>Une erreur ou un avertissement de compilation.</summary>
public record BuildError(string FilePath, int Line, int Column, string Message);

/// <summary>Résultat d'une compilation.</summary>
public record BuildResult(bool Success, IReadOnlyList<BuildError> Errors);
