using System.Windows;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class DirectRulesDialog : Window
    {
        private readonly AppConfig _config;
        public DirectRulesDialog(AppConfig config, bool isVpnMode)
        {
            _config = config;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Loaded += (s, e) =>
            {
                txtDomains.Text = config.LastManualSplit;
                txtApps.Text = config.LastAppSplit;
                txtBlock.Text = config.LastBlockSplit;
                if (isVpnMode)
                {
                    txtDomains.IsEnabled = false;
                    txtDomains.Opacity = 0.3;
                    lblDomains.Text = "Domains & IPs (Disabled in VPN Mode - Use App Bypass below)";
                    lblDomains.Opacity = 0.5;
                    txtApps.Focus();
                }
                else
                {
                    txtDomains.Focus();
                }
                btnOk.Click += OnSave;
                btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
                btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };
            };
        }
        private void OnSave(object sender, RoutedEventArgs e)
        {
            _config.LastManualSplit = txtDomains.Text.Trim();
            _config.LastAppSplit = txtApps.Text.Trim();
            _config.LastBlockSplit = txtBlock.Text.Trim();
            DialogResult = true;
            Close();
        }
    }
}
