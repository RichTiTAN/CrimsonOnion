using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class OutboundProxyDialog : Window
    {
        private readonly AppConfig _config;
        private bool _isFirstLoad = true;

        public OutboundProxyDialog(AppConfig config)
        {
            _config = config;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Pre-fill values
            txtAddr.Text = _config.OutboundProxyAddress;
            txtPort.Text = _config.OutboundProxyPort;
            txtUser.Text = _config.OutboundProxyUser;
            txtPass.Text = _config.OutboundProxyPass;

            SelectComboItem(cmbProxyType, string.IsNullOrEmpty(_config.OutboundProxyType) ? "SOCKS5" : _config.OutboundProxyType);
            SelectComboItem(cmbAuth, _config.EnableOutboundAuth ? "Enabled" : "Disabled");

            // Apply initial height without animation
            EvaluateAuth();
            _isFirstLoad = false;

            cmbAuth.SelectionChanged += (s2, e2) => EvaluateAuth();
            cmbProxyType.SelectionChanged += (s2, e2) => { /* type is read at save */ };

            btnOk.Click += OnSave;
            btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
            btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };

            txtAddr.Focus();
            txtAddr.CaretIndex = txtAddr.Text.Length;
        }

        private void EvaluateAuth()
        {
            bool isEnabled = cmbAuth.SelectedItem is ComboBoxItem item && (string)item.Content == "Enabled";
            double targetH = isEnabled ? 320.0 : 263.0;
            double tBorderH = isEnabled ? 267.0 : 210.0;
            double tBtnTop = isEnabled ? 225.0 : 168.0;
            double tOpac = isEnabled ? 1.0 : 0.0;

            if (_isFirstLoad)
            {
                Height = targetH;
                borderMain.Height = tBorderH;
                Canvas.SetTop(btnOk, tBtnTop);
                Canvas.SetTop(btnCancel, tBtnTop);
                panAuth.Opacity = tOpac;
                panAuth.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                if (isEnabled) panAuth.Visibility = Visibility.Visible;
                var dur = new Duration(TimeSpan.FromMilliseconds(250));
                this.BeginAnimation(HeightProperty, new DoubleAnimation(targetH, dur));
                borderMain.BeginAnimation(HeightProperty, new DoubleAnimation(tBorderH, dur));
                // Animate button positions
                var btnOkAnim = new DoubleAnimation(tBtnTop, dur);
                btnOk.BeginAnimation(Canvas.TopProperty, btnOkAnim);
                var btnCancelAnim = new DoubleAnimation(tBtnTop, dur);
                btnCancel.BeginAnimation(Canvas.TopProperty, btnCancelAnim);
                var opacAnim = new DoubleAnimation(tOpac, dur);
                if (!isEnabled)
                {
                    opacAnim.Completed += (s, e) =>
                    {
                        if (cmbAuth.SelectedItem is ComboBoxItem ci && (string)ci.Content == "Disabled")
                            panAuth.Visibility = Visibility.Collapsed;
                    };
                }
                panAuth.BeginAnimation(OpacityProperty, opacAnim);
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            _config.OutboundProxyAddress = txtAddr.Text.Trim();
            _config.OutboundProxyPort = txtPort.Text.Trim();
            _config.OutboundProxyType = cmbProxyType.SelectedItem is ComboBoxItem pt ? (string)pt.Content : "SOCKS5";
            _config.EnableOutboundAuth = cmbAuth.SelectedItem is ComboBoxItem ai && (string)ai.Content == "Enabled";
            _config.OutboundProxyUser = txtUser.Text.Trim();
            _config.OutboundProxyPass = txtPass.Text.Trim();
            DialogResult = true;
            Close();
        }

        private static void SelectComboItem(System.Windows.Controls.ComboBox combo, string content)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if ((string)item.Content == content) { combo.SelectedItem = item; return; }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
    }
}
