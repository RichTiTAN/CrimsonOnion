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
using CrimsonOnion.Services;
using S = CrimsonOnion.Localization.AppStrings;

namespace CrimsonOnion.Views.Overlays
{
    public partial class SplitTunnelOverlay : UserControl
    {
        private MainWindow? _main;
        private string _tempDomains = "";
        private string _tempApps = "";
        private string _tempBlock = "";

        public SplitTunnelOverlay()
        {
            InitializeComponent();
        }

    public bool HasUnsavedInput
    {
        get
        {
            var txtDomains = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitDomains");
            var txtApps = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitApps");
            var txtBlock = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitBlock");

            return (txtDomains != null && !string.IsNullOrWhiteSpace(txtDomains.Text)) ||
                   (txtApps != null && !string.IsNullOrWhiteSpace(txtApps.Text)) ||
                   (txtBlock != null && !string.IsNullOrWhiteSpace(txtBlock.Text));
        }
    }

    public void Initialize(MainWindow main)
        {
            _main = main;
            UpdateSplitTunnelUI();
        }

    private void InitPanel(Border panel, Border togglePanel, TextBox tb, Button btnToggle)
    {
        bool hasText = !string.IsNullOrWhiteSpace(tb.Text);
        panel.Height = hasText ? 34 : 0;
        btnToggle.Content = hasText ? CrimsonOnion.Localization.AppStrings.Edit : CrimsonOnion.Localization.AppStrings.Add;
        togglePanel.CornerRadius = hasText ? new global::Avalonia.CornerRadius(4, 4, 0, 0) : new global::Avalonia.CornerRadius(4);

        if (hasText)
        {
            tb.Height = 17;
            tb.Margin = new global::Avalonia.Thickness(0);
            tb.IsHitTestVisible = false;
            tb.IsReadOnly = true;
            tb.Focusable = false;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow);
        }
        else
        {
            tb.Height = 46;
            tb.IsHitTestVisible = true;
            tb.IsReadOnly = false;
            tb.Focusable = true;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Ibeam);
        }
    }
    private void TogglePanel(Border panel, Border togglePanel, TextBox tb, Button btnToggle, ref string tempStore)
    {
        if (panel.Height < 110)
        {
            tempStore = tb.Text ?? "";

            tb.Height = 56;
            tb.Margin = new global::Avalonia.Thickness(0, 5, 0, 0);
            tb.IsHitTestVisible = true;
            tb.IsReadOnly = false;
            tb.Focusable = true;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Ibeam);
            btnToggle.Content = CrimsonOnion.Localization.AppStrings.Edit;
            togglePanel.CornerRadius = new global::Avalonia.CornerRadius(4, 4, 0, 0);

            panel.Height = 110;
            tb.Focus();
        }
        else
        {
            ClosePanel(panel, togglePanel, tb, btnToggle);
        }
    }
    private void ClosePanel(Border panel, Border togglePanel, TextBox tb, Button btnToggle)
    {
        bool hasText = !string.IsNullOrWhiteSpace(tb.Text);

        btnToggle.Content = hasText ? CrimsonOnion.Localization.AppStrings.Edit : CrimsonOnion.Localization.AppStrings.Add;
        togglePanel.CornerRadius = hasText ? new global::Avalonia.CornerRadius(4, 4, 0, 0) : new global::Avalonia.CornerRadius(4);

        if (hasText)
        {
            tb.Height = 17;
            tb.Margin = new global::Avalonia.Thickness(0);
            tb.IsHitTestVisible = false;
            tb.IsReadOnly = true;
            tb.Focusable = false;
            tb.Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Arrow);
        }

        panel.Height = hasText ? 34 : 0;
    }

﻿
private void ApplyLanguage()
{
    var lblSplitTunnelingHeader = this.FindControl<TextBlock>("lblSplitTunnelingHeader");
    S.Apply(lblSplitTunnelingHeader, S.SplitTunneling, forceLtr: true);

    S.Apply(this.FindControl<TextBlock>("lblDomainsAndIps"), S.DomainsAndIps);
    S.Apply(this.FindControl<TextBlock>("lblApplications"), S.Applications);
    S.Apply(this.FindControl<TextBlock>("lblBlockedDomainsIps"), S.BlockedDomains);

    var lblSplitAppsWarning = this.FindControl<TextBlock>("lblSplitAppsWarning");
    if (lblSplitAppsWarning != null) lblSplitAppsWarning.Text = S.LblSplitAppsWarning;

    var lblDirectUdpHeader = this.FindControl<TextBlock>("lblDirectUdpHeader");
    S.Apply(lblDirectUdpHeader, S.SplitTunnelDirectUDP);
    S.ApplyToolTip(lblDirectUdpHeader, S.SplitTunnelDirectUDPTooltip);

    var btnSplitDisabled  = this.FindControl<Button>("btnSplitDisabled");
    var btnSplitExclusive = this.FindControl<Button>("btnSplitExclusive");
    var btnSplitInclusive = this.FindControl<Button>("btnSplitInclusive");

    S.ApplyToolTip(btnSplitDisabled, S.TtSplitDis);
    S.ApplyToolTip(btnSplitExclusive, S.TtSplitExc);
    S.ApplyToolTip(btnSplitInclusive, S.TtSplitInc);

    if (btnSplitDisabled?.Content  is TextBlock tbDis) S.Apply(tbDis, S.Disabled);
    if (btnSplitExclusive?.Content is TextBlock tbEx)  S.Apply(tbEx, S.Exclusive);
    if (btnSplitInclusive?.Content is TextBlock tbIn)  S.Apply(tbIn, S.Inclusive);
}

public void UpdateSplitTunnelUI()
    {
        if (_main == null) return;
        ApplyLanguage();
        this.FindControl<global::Avalonia.Controls.Button>("btnSplitDisabled")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnSplitExclusive")?.Classes.Remove("activeOpt");
        this.FindControl<global::Avalonia.Controls.Button>("btnSplitInclusive")?.Classes.Remove("activeOpt");

        var modeStr = _main!.Cfg.SplitTunnelMode ?? "DISABLED";
        if (modeStr == "EXCLUSIVE") this.FindControl<global::Avalonia.Controls.Button>("btnSplitExclusive")?.Classes.Add("activeOpt");
        else if (modeStr == "INCLUSIVE") this.FindControl<global::Avalonia.Controls.Button>("btnSplitInclusive")?.Classes.Add("activeOpt");
        else this.FindControl<global::Avalonia.Controls.Button>("btnSplitDisabled")?.Classes.Add("activeOpt");

        var panSplitConfig = this.FindControl<global::Avalonia.Controls.Border>("panSplitConfig");
        if (panSplitConfig != null)
        {
            if (modeStr != "EXCLUSIVE" && modeStr != "INCLUSIVE")
            {
                panSplitConfig.MaxHeight = 0;
                panSplitConfig.Opacity = 0;
            }
            else
            {
                panSplitConfig.MaxHeight = 800;
                panSplitConfig.Opacity = 1;
            }
        }

        var lblSplitExplanation = this.FindControl<global::Avalonia.Controls.TextBlock>("lblSplitExplanation");
        if (lblSplitExplanation != null)
        {
            if (modeStr == "EXCLUSIVE")
                lblSplitExplanation.Text = CrimsonOnion.Localization.AppStrings.TtSplitExc;
            else if (modeStr == "INCLUSIVE")
                lblSplitExplanation.Text = CrimsonOnion.Localization.AppStrings.TtSplitInc;

            lblSplitExplanation.FlowDirection = CrimsonOnion.Localization.AppStrings.IsPersian 
                ? global::Avalonia.Media.FlowDirection.RightToLeft 
                : global::Avalonia.Media.FlowDirection.LeftToRight;
        }

        var panSplitDomains = this.FindControl<global::Avalonia.Controls.StackPanel>("panSplitDomains");
        var panSplitApps = this.FindControl<global::Avalonia.Controls.StackPanel>("panSplitApps");

        if (panSplitDomains != null && panSplitApps != null)
        {
            if (_main!.Cfg.LastXrayMode == XrayModes.VpnMode)
            {
                panSplitDomains.IsEnabled = false;
                panSplitDomains.Opacity = 0.3;

                panSplitApps.IsEnabled = true;
                panSplitApps.Opacity = 1.0;
            }
            else
            {
                panSplitDomains.IsEnabled = true;
                panSplitDomains.Opacity = 1.0;

                panSplitApps.IsEnabled = false;
                panSplitApps.Opacity = 0.3;
            }
        }

        var txtSplitDomains = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitDomains");
        if (txtSplitDomains != null)
        {
            if (txtSplitDomains.Text != _main!.Cfg.LastManualSplit) txtSplitDomains.Text = _main!.Cfg.LastManualSplit;
            InitPanel(this.FindControl<global::Avalonia.Controls.Border>("panDomainsEdit")!, this.FindControl<global::Avalonia.Controls.Border>("panDomainsToggle")!, txtSplitDomains, this.FindControl<global::Avalonia.Controls.Button>("btnToggleDomains")!);
        }

        var txtSplitApps = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitApps");
        if (txtSplitApps != null)
        {
            if (txtSplitApps.Text != _main!.Cfg.LastAppSplit) txtSplitApps.Text = _main!.Cfg.LastAppSplit;
            InitPanel(this.FindControl<global::Avalonia.Controls.Border>("panAppsEdit")!, this.FindControl<global::Avalonia.Controls.Border>("panAppsToggle")!, txtSplitApps, this.FindControl<global::Avalonia.Controls.Button>("btnToggleApps")!);
        }

        var txtSplitBlock = this.FindControl<global::Avalonia.Controls.TextBox>("txtSplitBlock");
        if (txtSplitBlock != null)
        {
            if (txtSplitBlock.Text != _main!.Cfg.LastBlockSplit) txtSplitBlock.Text = _main!.Cfg.LastBlockSplit;
            InitPanel(this.FindControl<global::Avalonia.Controls.Border>("panBlockEdit")!, this.FindControl<global::Avalonia.Controls.Border>("panBlockToggle")!, txtSplitBlock, this.FindControl<global::Avalonia.Controls.Button>("btnToggleBlock")!);
        }
    }

private void SplitTunnel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Button clickedBtn)
        {
            string oldMode = _main!.Cfg.SplitTunnelMode ?? "DISABLED";

            if (clickedBtn.Name == "btnSplitExclusive") _main!.Cfg.SplitTunnelMode = "EXCLUSIVE";
            else if (clickedBtn.Name == "btnSplitInclusive") _main!.Cfg.SplitTunnelMode = "INCLUSIVE";
            else _main!.Cfg.SplitTunnelMode = "DISABLED";

            if (oldMode == _main!.Cfg.SplitTunnelMode) return;

            _main!.Cfg.EnableDirect = _main!.Cfg.SplitTunnelMode != "DISABLED";

            UpdateSplitTunnelUI();
            _main!.TriggerRequestConfigSave();

            if (_main!.State.IsEngineRunning && SplitTunnelService.HasAnySplitInput(_main!.Cfg))
            {
                _main!.TriggerSmartRestartXray();
            }
        }
    }

private void SplitToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Name == "btnToggleDomains") TogglePanel(this.FindControl<Border>("panDomainsEdit")!, this.FindControl<Border>("panDomainsToggle")!, this.FindControl<TextBox>("txtSplitDomains")!, btn, ref _tempDomains);
            else if (btn.Name == "btnToggleApps") TogglePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, this.FindControl<TextBox>("txtSplitApps")!, btn, ref _tempApps);
            else if (btn.Name == "btnToggleBlock") TogglePanel(this.FindControl<Border>("panBlockEdit")!, this.FindControl<Border>("panBlockToggle")!, this.FindControl<TextBox>("txtSplitBlock")!, btn, ref _tempBlock);
        }
    }

private void SplitSave_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            bool changed = false;
            if (btn.Name == "btnSaveDomains")
            {
                var tb = this.FindControl<TextBox>("txtSplitDomains")!;
                string newVal = tb.Text?.Trim() ?? "";
                if (_main!.Cfg.LastManualSplit != newVal)
                {
                    _main!.Cfg.LastManualSplit = newVal;
                    changed = true;
                }
                ClosePanel(this.FindControl<Border>("panDomainsEdit")!, this.FindControl<Border>("panDomainsToggle")!, tb, this.FindControl<Button>("btnToggleDomains")!);
            }
            else if (btn.Name == "btnSaveApps")
            {
                var tb = this.FindControl<TextBox>("txtSplitApps")!;
                string newVal = tb.Text?.Trim() ?? "";
                if (_main!.Cfg.LastAppSplit != newVal)
                {
                    _main!.Cfg.LastAppSplit = newVal;
                    changed = true;
                }
                ClosePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, tb, this.FindControl<Button>("btnToggleApps")!);
            }
            else if (btn.Name == "btnSaveBlock")
            {
                var tb = this.FindControl<TextBox>("txtSplitBlock")!;
                string newVal = tb.Text?.Trim() ?? "";
                if (_main!.Cfg.LastBlockSplit != newVal)
                {
                    _main!.Cfg.LastBlockSplit = newVal;
                    changed = true;
                }
                ClosePanel(this.FindControl<Border>("panBlockEdit")!, this.FindControl<Border>("panBlockToggle")!, tb, this.FindControl<Button>("btnToggleBlock")!);
            }

            if (changed)
            {
                _main!.TriggerRequestConfigSave();
                if (_main!.State.IsEngineRunning)
                    _main!.TriggerSmartRestartXray();
            }
        }
    }

private void SplitCancel_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Name == "btnCancelDomains")
            {
                var tb = this.FindControl<TextBox>("txtSplitDomains")!;
                tb.Text = _tempDomains;
                ClosePanel(this.FindControl<Border>("panDomainsEdit")!, this.FindControl<Border>("panDomainsToggle")!, tb, this.FindControl<Button>("btnToggleDomains")!);
            }
            else if (btn.Name == "btnCancelApps")
            {
                var tb = this.FindControl<TextBox>("txtSplitApps")!;
                tb.Text = _tempApps;
                ClosePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, tb, this.FindControl<Button>("btnToggleApps")!);
            }
            else if (btn.Name == "btnCancelBlock")
            {
                var tb = this.FindControl<TextBox>("txtSplitBlock")!;
                tb.Text = _tempBlock;
                ClosePanel(this.FindControl<Border>("panBlockEdit")!, this.FindControl<Border>("panBlockToggle")!, tb, this.FindControl<Button>("btnToggleBlock")!);
            }
        }
    }

private async void BrowseApp_Click(object? sender, RoutedEventArgs e)
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow == null) return;

            var storageProvider = mainWindow.StorageProvider;
            var fileOptions = new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Application",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new global::Avalonia.Platform.Storage.FilePickerFileType("Executables") { Patterns = new[] { "*.exe" } },
                    new global::Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            };

            var result = await storageProvider.OpenFilePickerAsync(fileOptions);
            if (result != null && result.Count > 0)
            {
                var file = result[0];
                var exeName = file.Name;

                var txtSplitApps = this.FindControl<TextBox>("txtSplitApps");
                if (txtSplitApps != null)
                {
                    if (string.IsNullOrWhiteSpace(txtSplitApps.Text))
                        txtSplitApps.Text = exeName;
                    else if (!txtSplitApps.Text.Split(',').Any(a => a.Trim().Equals(exeName, StringComparison.OrdinalIgnoreCase)))
                        txtSplitApps.Text += $", {exeName}";

                    _main!.Cfg.LastAppSplit = txtSplitApps.Text;
                    _main!.TriggerRequestConfigSave();

                    ClosePanel(this.FindControl<Border>("panAppsEdit")!, this.FindControl<Border>("panAppsToggle")!, txtSplitApps, this.FindControl<Button>("btnToggleApps")!);

                    if (_main!.State.IsEngineRunning)
                        _main!.TriggerSmartRestartXray();
                }
            }
        }
    }

private void togDirectUDP_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_main!.IsInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            _main!.Cfg.EnableDirectUDP = tog.IsChecked == true;
            _main!.TriggerRequestConfigSave();

            string runningMode = _main!.Cfg.LastXrayMode;

            _main!.TriggerUpdateModeUI();

            if (_main!.State.IsEngineRunning)
            {
                if (SplitTunnelService.RestartAppliesChange(runningMode))
                    _main!.TriggerSmartRestartXray();
                else
                    _main!.ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            }
        }
    }
private void btnDirectUdpToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togDirectUDP") return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var pan = this.FindControl<global::Avalonia.Controls.Border>("panDirectUdpSettings");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoDirectUdpExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panDirectUdpToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnDirectUdpToggle");
        if (pan != null)
        {
            if (pan.MaxHeight == 0)
            {
                pan.MaxHeight = 200;
                pan.Opacity = 1;
                if (ico != null) ico.RenderTransform = new global::Avalonia.Media.RotateTransform(180);
                if (panToggle != null) panToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);
                if (btnToggle != null) btnToggle.CornerRadius = new global::Avalonia.CornerRadius(8, 8, 0, 0);

                var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDirectUdpAdapters");
                if (cmb != null && cmb.Items.Count == 0)
                {
                    btnScanDirectUdpAdapters_Click(null, null);
                }
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
    }
private void cmbDirectUdpAdapters_SelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        var cmb = sender as global::Avalonia.Controls.ComboBox;
        if (cmb != null && cmb.SelectedItem is string selectedText && !string.IsNullOrWhiteSpace(selectedText))
        {
            if (selectedText == SplitTunnelService.DefaultAdapter)
            {
                bool changed = _main!.Cfg.DirectUdpAdapterIp != "";
                _main!.Cfg.DirectUdpAdapterName = SplitTunnelService.DefaultAdapter;
                _main!.Cfg.DirectUdpAdapterIp = "";
                _main!.TriggerRequestConfigSave();
                if (changed && _main!.Cfg.EnableDirectUDP && _main!.State.IsEngineRunning)
                {
                    if (SplitTunnelService.RestartAppliesChange(_main!.Cfg.LastXrayMode))
                        _main!.TriggerSmartRestartXray();
                    else
                        _main!.ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
                return;
            }

            if (SplitTunnelService.TryParseAdapterEntry(selectedText, out var newName, out var newIp))
            {
                bool changed = newIp != _main!.Cfg.DirectUdpAdapterIp;

                _main!.Cfg.DirectUdpAdapterName = newName;
                _main!.Cfg.DirectUdpAdapterIp = newIp;
                _main!.TriggerRequestConfigSave();

                if (changed && _main!.Cfg.EnableDirectUDP && _main!.State.IsEngineRunning)
                {
                    if (SplitTunnelService.RestartAppliesChange(_main!.Cfg.LastXrayMode))
                        _main!.TriggerSmartRestartXray();
                    else
                        _main!.ShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
        }
    }
private void btnScanDirectUdpAdapters_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs? e = null)
    {
        var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDirectUdpAdapters");
        if (cmb == null) return;

        cmb.Items.Clear();
        foreach (var entry in SplitTunnelService.ListUsableAdapters(includeDefault: true)) cmb.Items.Add(entry);

        if (_main!.Cfg.DirectUdpAdapterName == SplitTunnelService.DefaultAdapter || string.IsNullOrWhiteSpace(_main!.Cfg.DirectUdpAdapterIp))
        {
            cmb.SelectedIndex = 0;
        }
        else if (!string.IsNullOrWhiteSpace(_main!.Cfg.DirectUdpAdapterName) && !string.IsNullOrWhiteSpace(_main!.Cfg.DirectUdpAdapterIp))
        {
            var index = SplitTunnelService.FindAdapterIndex(cmb.Items.Cast<string>(), _main!.Cfg.DirectUdpAdapterName, _main!.Cfg.DirectUdpAdapterIp);
            if (index >= 0)
            {
                cmb.SelectedIndex = index;
            }
            else
            {
                _main!.ShowToast(CrimsonOnion.Localization.AppStrings.ToastDirectUdpAdapterLost);
                _main!.Cfg.DirectUdpAdapterName = SplitTunnelService.DefaultAdapter;
                _main!.Cfg.DirectUdpAdapterIp = "";
                _main!.TriggerRequestConfigSave();

                cmb.SelectedIndex = 0;
            }
        }
        else if (cmb.Items.Count > 0)
        {
            cmb.SelectedIndex = 0;
        }
    }

    private void CloseOverlay_Click(object? sender, RoutedEventArgs e)
    {
        _main?.TriggerCloseAllOverlays();
    }
}
}

