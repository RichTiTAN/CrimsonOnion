/*
 * CrimsonOnion - A GUI client that runs multiple Tor instances and load-balances them.
 * Copyright (C) 2026 RichTiTAN
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using CrimsonOnion.Localization;

namespace CrimsonOnion.Dialogs
{
    public partial class TrayWidget : Window
    {
        private MainWindow _main = null!;
        private DispatcherTimer _timer = null!;
        private bool _isClosing = false;

        public TrayWidget()
        {
            InitializeComponent();
        }

                public TrayWidget(MainWindow main)
        {
            InitializeComponent();
            _main = main;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateUI();
            _timer.Start();
            ApplyLanguage(CrimsonOnion.Localization.AppStrings.IsPersian);
            UpdateUI();
        }

        private void UpdateUI()
        {
            bool fa = AppStrings.IsPersian;
            if (_main.GetState().IsConnected)
            {
                lblStatus.Text = CrimsonOnion.Localization.AppStrings.ConnectedBtn;
                lblStatus.Foreground = new SolidColorBrush(Color.Parse("#68D391"));
                btnToggle.Content = CrimsonOnion.Localization.AppStrings.Disconnect;
            }
            else if (_main.GetState().IsEngineRunning)
            {
                lblStatus.Text = CrimsonOnion.Localization.AppStrings.BtnConnecting;
                lblStatus.Foreground = new SolidColorBrush(Color.Parse("#E2E8F0"));
                btnToggle.Content = CrimsonOnion.Localization.AppStrings.Disconnect;
            }
            else
            {
                lblStatus.Text = CrimsonOnion.Localization.AppStrings.TrayStatusNotConnected;
                lblStatus.Foreground = new SolidColorBrush(Color.Parse("#E2E8F0"));
                btnToggle.Content = CrimsonOnion.Localization.AppStrings.Connect;
            }
            lblSpeed.Text = _main.GetSpeedText();
        }

        public void ApplyLanguage(bool isPersian)
        {
            UpdateUI();

            bool fa = isPersian;
            btnClose.Content = CrimsonOnion.Localization.AppStrings.TrayBtnCloseApp;
            btnShowWindow.Content = CrimsonOnion.Localization.AppStrings.TrayBtnShowWindow;

            lblStatus.FlowDirection = fa
                ? global::Avalonia.Media.FlowDirection.RightToLeft
                : global::Avalonia.Media.FlowDirection.LeftToRight;
        }

        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            _main.ConnectDisconnect();
            UpdateUI();
        }

        private void BtnShowWindow_Click(object sender, RoutedEventArgs e)
        {
            _main.Show();
            _main.WindowState = WindowState.Normal;
            _main.Activate();
            SafeClose();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            _main.Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            SafeClose();
        }

        private void SafeClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            _timer.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}

