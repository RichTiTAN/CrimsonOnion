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
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CrimsonOnion.Views.Overlays
{
    public partial class SidebarOverlay : UserControl
    {
        private MainWindow? _main;

        public SidebarOverlay()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow main) => _main = main;

        private void SidebarConnection_Click(object? sender, RoutedEventArgs e) => _main?.TriggerCloseAllOverlays();

        private void SidebarCountries_Click(object? sender, RoutedEventArgs e) => _main?.TriggerToggleSidebarCountriesPopup();

        private void SidebarThemes_Click(object? sender, RoutedEventArgs e) => _main?.TriggerOpenThemesPane();

        private void btnSplitTunnel_Click(object? sender, RoutedEventArgs e) => _main?.TriggerOpenSplitTunnelPane();

        private void btnSettings_Click(object? sender, RoutedEventArgs e) => _main?.TriggerOpenSettingsPane();

        private void SidebarAbout_Click(object? sender, RoutedEventArgs e) => _main?.TriggerOpenAboutPane();

        private void SidebarBorder_PointerEntered(object? sender, PointerEventArgs e)
        {
            var rectAllThemes = this.FindControl<Border>("rectAllThemes");
            if (rectAllThemes != null) rectAllThemes.Opacity = 1.0;
            var panCurrentThemeIcon = this.FindControl<Panel>("panCurrentThemeIcon");
            if (panCurrentThemeIcon != null) panCurrentThemeIcon.Opacity = 0.0;

            _main?.TriggerSidebarHover(entered: true);
        }

        private void SidebarBorder_PointerExited(object? sender, PointerEventArgs e)
        {
            var rectAllThemes = this.FindControl<Border>("rectAllThemes");
            if (rectAllThemes != null) rectAllThemes.Opacity = 0.0;
            var panCurrentThemeIcon = this.FindControl<Panel>("panCurrentThemeIcon");
            if (panCurrentThemeIcon != null) panCurrentThemeIcon.Opacity = 1.0;

            _main?.TriggerSidebarHover(entered: false);
        }
    }
}
