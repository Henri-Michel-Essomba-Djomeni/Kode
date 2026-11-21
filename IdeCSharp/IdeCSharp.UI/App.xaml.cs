using System;
using System.IO;
using System.Windows;
using IdeCSharp.Core.Extensions;
using IdeCSharp.Core.Settings;

namespace IdeCSharp.UI
{
    public partial class App : Application
    {
        // Catalogue en ligne : il faudra publier ce fichier dans le dépôt (voir plus bas).
        // Tant qu'il est absent, Kode utilise le catalogue livré avec l'application.
        private const string CatalogUrl =
            "https://raw.githubusercontent.com/Henri-Michel-Essomba-Djomeni/Kode/main/catalog/catalog.json";

        /// <summary>Service d'extensions partagé par toute l'application.</summary>
        public static ExtensionService Extensions { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            Extensions = new ExtensionService(
                installFolder: Path.Combine(UserSettings.Folder, "extensions"),
                bundledCatalogPath: Path.Combine(AppContext.BaseDirectory, "Assets", "catalog.json"),
                catalogUrl: CatalogUrl);

            // Thème et extension de la dernière session, avant d'afficher la fenêtre
            ThemeManager.Restore(Extensions);
            base.OnStartup(e);
        }
    }
}