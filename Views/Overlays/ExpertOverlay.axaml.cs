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
using CrimsonOnion.Models;

namespace CrimsonOnion.Views.Overlays
{
    public partial class ExpertOverlay : UserControl
    {
        private MainWindow? _main;

        public ExpertOverlay()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow main) => _main = main;

        internal void LoadFromConfig(AppConfig cfg)
        {
            Toggle("cmbHW", cfg.ExpertHardwareAccel);
            Toggle("cmbFF", cfg.ExpertFascistFirewall);
            Toggle("cmbSN", cfg.ExpertStrictNodes);
            Field("txtCBT", cfg.ExpertCircuitBuildTimeout);
            Field("txtKP", cfg.ExpertKeepalivePeriod);
            Field("txtNCP", cfg.ExpertNewCircuitPeriod);
            Field("txtMCD", cfg.ExpertMaxCircuitDirtiness);
            Field("txtNEG", cfg.ExpertNumEntryGuards);
            Field("txtEN", cfg.ExpertEntryNodes);
            Field("txtExit", cfg.ExpertExitNodes);
            Field("txtExNodes", cfg.ExpertExcludeNodes);
            Field("txtExExit", cfg.ExpertExcludeExitNodes);
            Field("txtRaw", cfg.ExpertCustomTorrc);
        }

        internal void SaveOntoConfig(AppConfig cfg)
        {
            cfg.ExpertHardwareAccel = ToggleIsOn("cmbHW");
            cfg.ExpertFascistFirewall = ToggleIsOn("cmbFF");
            cfg.ExpertStrictNodes = ToggleIsOn("cmbSN");
            cfg.ExpertCircuitBuildTimeout = FieldText("txtCBT");
            cfg.ExpertKeepalivePeriod = FieldText("txtKP");
            cfg.ExpertNewCircuitPeriod = FieldText("txtNCP");
            cfg.ExpertMaxCircuitDirtiness = FieldText("txtMCD");
            cfg.ExpertNumEntryGuards = FieldText("txtNEG");
            cfg.ExpertEntryNodes = FieldText("txtEN");
            cfg.ExpertExitNodes = FieldText("txtExit");
            cfg.ExpertExcludeNodes = FieldText("txtExNodes");
            cfg.ExpertExcludeExitNodes = FieldText("txtExExit");
            cfg.ExpertCustomTorrc = FieldText("txtRaw");
        }

        private void btnExpertSave_Click(object? sender, RoutedEventArgs e) => _main?.TriggerExpertSave(this);

        private void btnExpertCancel_Click(object? sender, RoutedEventArgs e) => _main?.TriggerCloseAllOverlays();

        private void Toggle(string name, bool on)
        {
            var combo = this.FindControl<ComboBox>(name);
            if (combo != null) combo.SelectedIndex = on ? 1 : 0;
        }

        private bool ToggleIsOn(string name) => this.FindControl<ComboBox>(name)?.SelectedIndex == 1;

        private void Field(string name, string value)
        {
            var box = this.FindControl<TextBox>(name);
            if (box != null) box.Text = value;
        }

        private string FieldText(string name) => this.FindControl<TextBox>(name)?.Text?.Trim() ?? "";
    }
}
