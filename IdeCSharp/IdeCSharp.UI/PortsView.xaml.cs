using System;
using System.Windows;
using System.Windows.Controls;
using IdeCSharp.Core.Network;

namespace IdeCSharp.UI
{
    public partial class PortsView : UserControl
    {
        public PortsView()
        {
            InitializeComponent();

            // La liste se met à jour chaque fois que l'onglet devient visible
            IsVisibleChanged += async (_, e) =>
            {
                if (e.NewValue is true) await RefreshAsync();
            };
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e) => await RefreshAsync();

        public async System.Threading.Tasks.Task RefreshAsync()
        {
            InfoText.Text = "Analyse des ports…";
            try
            {
                // netstat est lancé hors du thread de l'interface pour ne rien figer
                var ports = await System.Threading.Tasks.Task.Run(() => PortScanner.ScanAsync());
                PortList.ItemsSource = ports;
                InfoText.Text = $"{ports.Count} port(s) en écoute";
            }
            catch (Exception ex)
            {
                InfoText.Text = $"Échec : {ex.Message}";
            }
        }
    }
}