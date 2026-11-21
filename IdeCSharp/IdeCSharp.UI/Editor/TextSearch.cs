using System;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Fonctions pures de recherche et comptage textuel sans dépendance graphique.
/// </summary>
public static class TextSearch
{
    /// <summary>
    /// Recherche la prochaine occurrence dans un texte à partir d'une position donnée.
    /// </summary>
    /// <param name="text">Texte source.</param>
    /// <param name="search">Texte recherché.</param>
    /// <param name="start">Position de départ.</param>
    /// <param name="matchCase">True pour respecter la casse, false sinon.</param>
    /// <param name="forward">True pour chercher vers l'avant, false vers l'arrière.</param>
    /// <param name="wrap">True pour boucler aux extrémités du texte.</param>
    /// <returns>Position de l'occurrence trouvée, ou -1 si aucune.</returns>
    public static int IndexOfMatch(string text, string search, int start, bool matchCase, bool forward, bool wrap)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
            return -1;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (forward)
        {
            int safeStart = Math.Clamp(start, 0, text.Length);
            if (safeStart < text.Length)
            {
                int index = text.IndexOf(search, safeStart, comparison);
                if (index >= 0)
                    return index;
            }

            if (wrap)
            {
                return text.IndexOf(search, 0, comparison);
            }

            return -1;
        }
        else
        {
            // Vers l'arrière : on cherche une occurrence commençant avant start
            int maxStartIndex = start - 1;
            if (maxStartIndex >= 0)
            {
                int safeIndex = Math.Min(maxStartIndex, text.Length - 1);
                int index = text.LastIndexOf(search, safeIndex, comparison);
                if (index >= 0)
                    return index;
            }

            if (wrap)
            {
                return text.LastIndexOf(search, text.Length - 1, comparison);
            }

            return -1;
        }
    }

    /// <summary>
    /// Compte le nombre d'occurrences non chevauchantes du texte recherché.
    /// </summary>
    public static int CountMatches(string text, string search, bool matchCase)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
            return 0;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(search, index, comparison)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    /// <summary>
    /// Retourne le rang (1-basé) de l'occurrence commençant à la position spécifiée, ou 0 s'il n'y en a pas.
    /// </summary>
    public static int MatchIndexAt(string text, string search, int position, bool matchCase)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search) || position < 0 || position + search.Length > text.Length)
            return 0;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (string.Compare(text, position, search, 0, search.Length, comparison) != 0)
            return 0;

        int rank = 0;
        int index = 0;

        while ((index = text.IndexOf(search, index, comparison)) >= 0)
        {
            rank++;
            if (index == position)
                return rank;
            if (index > position)
                return 0;

            index += search.Length;
        }

        return 0;
    }
}
