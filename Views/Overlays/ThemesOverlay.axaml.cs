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

namespace CrimsonOnion.Views.Overlays
{
    public partial class ThemesOverlay : UserControl
    {
        private MainWindow? _main;
        private bool _isInitializing = false;

        public ThemesOverlay()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow main)
        {
            _main = main;
            _isInitializing = true;
            
            var togPauseGlow = this.FindControl<ToggleSwitch>("togPauseGlow");
            if (togPauseGlow != null) togPauseGlow.IsChecked = _main.Cfg.PauseGlow;
            
            var togDisableGlow = this.FindControl<ToggleSwitch>("togDisableGlow");
            if (togDisableGlow != null) togDisableGlow.IsChecked = _main.Cfg.DisableGlow;

            _isInitializing = false;
        }

        private void CloseOverlay_Click(object? sender, RoutedEventArgs e)
        {
            _main?.TriggerCloseAllOverlays();
        }

        private void ThemeSelect_Click(object? sender, RoutedEventArgs e)
        {
            _main?.TriggerThemeSelect_Click(sender, e);
        }

        private void togPauseGlow_IsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing || _main == null) return;
            var tog = sender as ToggleSwitch;
            if (tog != null && tog.IsChecked.HasValue)
            {
                _main.Cfg.PauseGlow = tog.IsChecked.Value;
                _main.TriggerRequestConfigSave();
                _main.ApplyGlowSettings();
            }
        }

        private void togDisableGlow_IsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing || _main == null) return;
            var tog = sender as ToggleSwitch;
            if (tog != null && tog.IsChecked.HasValue)
            {
                _main.Cfg.DisableGlow = tog.IsChecked.Value;
                _main.TriggerRequestConfigSave();
                _main.ApplyGlowSettings();
            }
        }
    }
}
