using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class DirectRulesDialog : Window
    {
        private readonly AppConfig _config;
        private readonly bool _isVpnMode;
        private bool _isFullyLoaded = false;

        public DirectRulesDialog(AppConfig config, bool isVpnMode)
        {
            _config = config;
            _isVpnMode = isVpnMode;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Loaded += (s, e) =>
            {
                txtDomains.Text = config.LastManualSplit;
                txtApps.Text = config.LastAppSplit;
                txtBlock.Text = config.LastBlockSplit;

                btnModeInclusive.Click += (s2, e2) => SetMode("INCLUSIVE");
                btnModeExclusive.Click += (s2, e2) => SetMode("EXCLUSIVE");

                if (string.IsNullOrEmpty(config.SplitTunnelMode))
                {
                    CollapseOptions(false);
                }
                else
                {
                    ShowConfig(config.SplitTunnelMode, false);
                }
                _isFullyLoaded = true;

                btnOk.Click += OnSave;
                btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
                btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };
            };
        }

        private void CollapseOptions(bool animate)
        {
            cvsConfig.Visibility = Visibility.Collapsed;
            if (animate && _isFullyLoaded) AnimateHeight(100, 76);
            else { this.BeginAnimation(Window.HeightProperty, null); this.Height = 100; MainBorder.BeginAnimation(Border.HeightProperty, null); MainBorder.Height = 76; }
        }

        private void SetMode(string mode)
        {
            _config.SplitTunnelMode = mode;
            ShowConfig(mode, true);
        }

        private void ShowConfig(string mode, bool animate)
        {
            cvsConfig.Visibility = Visibility.Visible;
            if (animate && _isFullyLoaded) AnimateHeight(385, 361);
            else { this.BeginAnimation(Window.HeightProperty, null); this.Height = 385; MainBorder.BeginAnimation(Border.HeightProperty, null); MainBorder.Height = 361; }

            var activeBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#25FFFFFF"));
            var inactiveBrush = new SolidColorBrush(Colors.Transparent);

            if (mode == "INCLUSIVE")
            {
                btnModeInclusive.Background = activeBrush;
                btnModeExclusive.Background = inactiveBrush;
                
                lblDomains.Text = _isVpnMode 
                    ? "Domains & IPs (Disabled in VPN Mode - Use App routing below)" 
                    : "Domains or IPs to go through Tor (Comma Separated | Proxy Mode):";
                lblApps.Text = "Applications to go through Tor (e.g., spotify.exe | VPN Mode):";
            }
            else
            {
                btnModeInclusive.Background = inactiveBrush;
                btnModeExclusive.Background = activeBrush;

                lblDomains.Text = _isVpnMode 
                    ? "Domains & IPs (Disabled in VPN Mode - Use App bypass below)" 
                    : "Domains or IPs to bypass Tor (Comma Separated | Proxy Mode):";
                lblApps.Text = "Applications to bypass VPN Tunnel (e.g., spotify.exe | VPN Mode):";
            }

            if (_isVpnMode)
            {
                txtDomains.IsEnabled = false;
                txtDomains.Opacity = 0.3;
                lblDomains.Opacity = 0.5;
                
                txtApps.IsEnabled = true;
                txtApps.Opacity = 1.0;
                lblApps.Opacity = 1.0;
                
                txtApps.Focus();
            }
            else
            {
                txtDomains.IsEnabled = true;
                txtDomains.Opacity = 1.0;
                lblDomains.Opacity = 1.0;
                
                txtApps.IsEnabled = false;
                txtApps.Opacity = 0.3;
                lblApps.Opacity = 0.5;
                
                txtDomains.Focus();
            }
        }

        private void AnimateHeight(double windowHeight, double borderHeight)
        {
            var animWindow = new DoubleAnimation
            {
                To = windowHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            var animBorder = new DoubleAnimation
            {
                To = borderHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.HeightProperty, animWindow);
            MainBorder.BeginAnimation(Border.HeightProperty, animBorder);
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_config.SplitTunnelMode))
                _config.SplitTunnelMode = "EXCLUSIVE"; // default if somehow saved

            _config.LastManualSplit = txtDomains.Text.Trim();
            _config.LastAppSplit = txtApps.Text.Trim();
            _config.LastBlockSplit = txtBlock.Text.Trim();
            DialogResult = true;
            Close();
        }
    }
}
