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
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace CrimsonOnion.Views.Overlays
{
    public partial class AboutOverlay : UserControl
    {
        private MainWindow? _main;

        public AboutOverlay()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow main)
        {
            _main = main;

            var lblVer = this.FindControl<TextBlock>("lblVersion");
            if (lblVer != null) lblVer.Text = CrimsonOnion.Services.UpdateService.AppVersion;
        }

        private void CloseOverlay_Click(object? sender, RoutedEventArgs e) => _main?.TriggerCloseAllOverlays();

        private async void BtnCopyAddress_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string address)
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(address);
                    _main?.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastAddressCopied, true);
                }
            }
        }

        private void btnDonationsToggle_Click(object? sender, RoutedEventArgs e)
        {
            var pan = this.FindControl<Border>("panDonations");
            if (pan == null) return;

            var ico = this.FindControl<PathIcon>("icoDonationsExpander");
            var panToggle = this.FindControl<Border>("panDonationsToggle");
            var btnToggle = this.FindControl<Button>("btnDonationsToggle");

            if (pan.MaxHeight == 0)
            {
                pan.MaxHeight = 300;
                pan.Opacity = 1;
                if (ico != null) ico.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
            }
            else
            {
                pan.MaxHeight = 0;
                pan.Opacity = 0;
                if (ico != null) ico.RenderTransform = new global::Avalonia.Media.RotateTransform(0);
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8);
            }
        }

        private void BtnCheckUpdate_Click(object? sender, RoutedEventArgs e) => _main?.TriggerBtnCheckUpdate_Click(sender, e);

        private void BtnGithub_Click(object? sender, RoutedEventArgs e) => _main?.TriggerBtnGithub_Click(sender, e);

        private void BtnTelegram_Click(object? sender, RoutedEventArgs e) => _main?.TriggerBtnTelegram_Click(sender, e);

        private void BtnOtherApps_Click(object? sender, RoutedEventArgs e) => _main?.TriggerBtnOtherApps_Click(sender, e);
    }
}
