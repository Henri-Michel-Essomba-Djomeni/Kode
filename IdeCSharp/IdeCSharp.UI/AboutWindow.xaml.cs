using System.Reflection;
using System.Windows;

namespace IdeCSharp.UI
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => WindowStyler.SetDarkTitleBar(this, ThemeManager.IsDark);

            // La version vient de l'assemblage : un seul endroit à changer plus tard
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = version is null
                ? "Version inconnue"
                : $"Version {version.Major}.{version.Minor}.{version.Build}";
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}