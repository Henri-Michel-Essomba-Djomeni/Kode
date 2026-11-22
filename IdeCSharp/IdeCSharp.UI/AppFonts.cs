using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using IdeCSharp.Core.Settings;

namespace IdeCSharp.UI
{
    /// <summary>Polices de l'interface et de l'éditeur : choix proposés et application en direct.</summary>
    public static class AppFonts
    {
        public static readonly string[] UiCandidates =
            { "Segoe UI", "Segoe UI Variable Text", "Calibri", "Arial", "Verdana", "Tahoma", "Trebuchet MS", "Candara" };

        public static readonly string[] EditorCandidates =
            { "Cascadia Code", "Cascadia Mono", "Consolas", "JetBrains Mono", "Fira Code",
              "Source Code Pro", "Lucida Console", "Courier New" };

        // On ne propose que les polices réellement installées sur cette machine
        public static List<string> Installed(IEnumerable<string> candidates)
        {
            var installed = new HashSet<string>(
                Fonts.SystemFontFamilies.Select(f => f.Source), StringComparer.OrdinalIgnoreCase);
            return candidates.Where(installed.Contains).ToList();
        }

        /// <summary>
        /// Publie les polices comme ressources de l'application. Tout contrôle qui les lit avec
        /// {DynamicResource ...} se met à jour tout seul, sans redémarrer.
        /// </summary>
        public static void Apply(UserSettings settings)
        {
            var resources = Application.Current.Resources;
            resources["UiFontFamily"] = new FontFamily(settings.UiFontFamily);
            resources["UiFontSize"] = Math.Clamp(settings.UiFontSize, 10, 20);
            resources["EditorFontFamily"] = new FontFamily(settings.EditorFontFamily);
            resources["EditorFontSize"] = Math.Clamp(settings.EditorFontSize, 8, 32);
        }
    }
}