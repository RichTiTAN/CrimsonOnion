using System.Windows;
using System.Windows.Controls;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class ExpertConfigDialog : Window
    {
        private readonly AppConfig _config;

        public ExpertConfigDialog(AppConfig config)
        {
            _config = config;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Loaded += (s, e) =>
            {
                SelectComboItem(cmbHW, config.ExpertHardwareAccel ? "Enabled" : "Disabled");
                SelectComboItem(cmbFF, config.ExpertFascistFirewall ? "Enabled" : "Disabled");
                SelectComboItem(cmbSN, config.ExpertStrictNodes ? "Enabled" : "Disabled");
                txtCBT.Text = config.ExpertCircuitBuildTimeout;
                txtKP.Text = config.ExpertKeepalivePeriod;
                txtNCP.Text = config.ExpertNewCircuitPeriod;
                txtMCD.Text = config.ExpertMaxCircuitDirtiness;
                txtNEG.Text = config.ExpertNumEntryGuards;
                txtEN.Text = config.ExpertEntryNodes;
                txtExit.Text = config.ExpertExitNodes;
                txtExNodes.Text = config.ExpertExcludeNodes;
                txtExExit.Text = config.ExpertExcludeExitNodes;
                txtRaw.Text = config.ExpertCustomTorrc;

                btnOk.Click += OnSave;
                btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
                btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };
            };
        }

        private static void SelectComboItem(System.Windows.Controls.ComboBox combo, string content)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if ((string)item.Content == content) { combo.SelectedItem = item; return; }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            _config.ExpertHardwareAccel = GetComboContent(cmbHW) == "Enabled";
            _config.ExpertFascistFirewall = GetComboContent(cmbFF) == "Enabled";
            _config.ExpertStrictNodes = GetComboContent(cmbSN) == "Enabled";
            _config.ExpertCircuitBuildTimeout = txtCBT.Text.Trim();
            _config.ExpertKeepalivePeriod = txtKP.Text.Trim();
            _config.ExpertNewCircuitPeriod = txtNCP.Text.Trim();
            _config.ExpertMaxCircuitDirtiness = txtMCD.Text.Trim();
            _config.ExpertNumEntryGuards = txtNEG.Text.Trim();
            _config.ExpertEntryNodes = txtEN.Text.Trim();
            _config.ExpertExitNodes = txtExit.Text.Trim();
            _config.ExpertExcludeNodes = txtExNodes.Text.Trim();
            _config.ExpertExcludeExitNodes = txtExExit.Text.Trim();
            _config.ExpertCustomTorrc = txtRaw.Text.Trim();
            DialogResult = true;
            Close();
        }

        private static string GetComboContent(System.Windows.Controls.ComboBox combo)
            => combo.SelectedItem is ComboBoxItem item ? (string)item.Content : "";
    }
}
