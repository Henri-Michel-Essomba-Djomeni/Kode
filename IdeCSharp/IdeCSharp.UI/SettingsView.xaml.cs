using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using IdeCSharp.Core.Settings;

namespace IdeCSharp.UI
{
    public partial class SettingsView : UserControl
    {
        // Vrai pendant le remplissage des contrôles : leurs événements ne doivent alors rien enregistrer
        private bool _loading = true;

        public SettingsView()
        {
            InitializeComponent();

            UiFontBox.ItemsSource = AppFonts.Installed(AppFonts.UiCandidates);
            EditorFontBox.ItemsSource = AppFonts.Installed(AppFonts.EditorCandidates);

            // Les valeurs sont relues à chaque ouverture du panneau
            IsVisibleChanged += (_, e) =>
            {
                if (e.NewValue is true) LoadValues();
            };
            LoadValues();
        }

        private void LoadValues()
        {
            _loading = true;
            var settings = UserSettings.Load();

            ThemeBox.SelectedIndex = ThemeManager.Current switch
            {
                AppTheme.System => 0,
                AppTheme.Light => 1,
                _ => 2
            };
            UiFontBox.SelectedItem = settings.UiFontFamily;
            UiSizeSlider.Value = settings.UiFontSize;
            EditorFontBox.SelectedItem = settings.EditorFontFamily;
            EditorSizeSlider.Value = settings.EditorFontSize;

            UpdateLabels();
            _loading = false;
        }

        private void UpdateLabels()
        {
            UiSizeText.Text = $"{UiSizeSlider.Value:0} px";
            EditorSizeText.Text = $"{EditorSizeSlider.Value:0} px";
        }

        // ------------------------------------------------------------------
        // Réactions aux contrôles
        // ------------------------------------------------------------------

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            ThemeManager.Apply(ThemeBox.SelectedIndex switch
            {
                1 => AppTheme.Light,
                2 => AppTheme.Dark,
                _ => AppTheme.System
            });
        }

        private void OnUiFontChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || UiFontBox.SelectedItem is not string name) return;
            Update(s => s.UiFontFamily = name);
        }

        private void OnUiSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            Update(s => s.UiFontSize = Math.Round(e.NewValue));
            UpdateLabels();
        }

        private void OnEditorFontChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || EditorFontBox.SelectedItem is not string name) return;
            Update(s => s.EditorFontFamily = name);
        }

        private void OnEditorSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            Update(s => s.EditorFontSize = Math.Round(e.NewValue));
            UpdateLabels();
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            var defaults = new UserSettings();
            Update(s =>
            {
                s.UiFontFamily = defaults.UiFontFamily;
                s.UiFontSize = defaults.UiFontSize;
                s.EditorFontFamily = defaults.EditorFontFamily;
                s.EditorFontSize = defaults.EditorFontSize;
            });
            LoadValues();
        }

        // On relit les réglages sur le disque à chaque fois : ThemeManager y écrit aussi
        // (thème, extension active), et une copie gardée en mémoire écraserait son travail.
        private static void Update(Action<UserSettings> change)
        {
            var settings = UserSettings.Load();
            change(settings);
            try
            {
                settings.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Enregistrer n'est pas vital : le changement reste appliqué pour cette session
            }
            AppFonts.Apply(settings);
        }
    }
}