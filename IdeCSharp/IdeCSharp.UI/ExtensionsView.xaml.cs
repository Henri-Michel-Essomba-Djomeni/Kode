using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using IdeCSharp.Core.Extensions;

namespace IdeCSharp.UI
{
    public partial class ExtensionsView : UserControl
    {
        private IReadOnlyList<ExtensionInfo> _catalog = new List<ExtensionInfo>();
        private bool _loaded;

        public ExtensionsView()
        {
            InitializeComponent();

            // Le catalogue n'est chargé qu'au premier affichage du panneau :
            // pas de réseau au démarrage de Kode
            IsVisibleChanged += async (_, e) =>
            {
                if (e.NewValue is true && !_loaded)
                {
                    await LoadAsync();
                }
            };
        }

        private async Task LoadAsync()
        {
            _loaded = true;
            SourceText.Text = "Chargement du catalogue…";
            try
            {
                var result = await App.Extensions.LoadCatalogAsync();
                _catalog = result.Extensions;
                SourceText.Text = result.FromNetwork
                    ? "Catalogue en ligne"
                    : "Hors ligne : catalogue intégré à Kode";
                Refresh();
            }
            catch (Exception ex)
            {
                SourceText.Text = $"Catalogue indisponible : {ex.Message}";
                _loaded = false;   // on réessaiera à la prochaine ouverture
            }
        }

        // Reconstruit la liste (recherche, état installé / actif)
        private void Refresh()
        {
            var query = SearchBox.Text.Trim();
            var activeId = ThemeManager.ActivePackId;

            var items = _catalog
                .Where(e => query.Length == 0
                         || e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || e.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(e => new ExtensionItem(e, App.Extensions.IsInstalled(e.Id), e.Id == activeId))
                .ToList();

            ExtensionList.ItemsSource = items;
            EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_loaded) Refresh();
        }

        // « Installer » télécharge puis applique ; « Appliquer » réutilise l'extension déjà installée
        private async void OnPrimaryClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ExtensionItem item) return;

            try
            {
                if (!item.IsInstalled)
                {
                    await App.Extensions.InstallAsync(item.Info);
                }
                ThemeManager.ApplyPack(item.Info);
                Refresh();
            }
            catch (Exception ex)
            {
                SourceText.Text = $"Échec : {ex.Message}";
            }
        }

        private void OnUninstallClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not ExtensionItem item) return;

            try
            {
                App.Extensions.Uninstall(item.Info.Id);

                // Si c'était le thème en cours, on revient au thème du système
                if (ThemeManager.ActivePackId == item.Info.Id)
                {
                    ThemeManager.Apply(AppTheme.System);
                }
                Refresh();
            }
            catch (Exception ex)
            {
                SourceText.Text = $"Échec : {ex.Message}";
            }
        }
    }
}