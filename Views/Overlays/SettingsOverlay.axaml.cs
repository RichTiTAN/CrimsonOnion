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
using Avalonia.Markup.Xaml;
using CrimsonOnion.Models;
using CrimsonOnion.Services;

namespace CrimsonOnion.Views.Overlays
{
    public partial class SettingsOverlay : UserControl
    {
        private MainWindow? _main;
        private bool _lanPassVisible = false;

        public SettingsOverlay()
        {
            AvaloniaXamlLoader.Load(this);
        }

    internal void TriggerScanAdapters() => btnScanAdapters_Click(null, null);
    internal void TriggerUpdateAdapterBindingMutualExclusivity() => UpdateAdapterBindingMutualExclusivity();

        public void Initialize(MainWindow main)
        {
            _main = main;
            UpdateSettingsUI();
        }

private void BtnLanguage_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _main?.TriggerBtnLanguage_Click(sender, e);

private async void SettingTog_CheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;

        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        bool val = tog.IsChecked ?? false;

        switch (tog.Name)
        {
            case "btnBootTog":
                try {
                    string exe = System.Environment.ProcessPath ?? "";
                    await CrimsonOnion.Services.ProcessService.UpdateBootScheduledTask(val, exe);
                    _main!.Cfg.LaunchOnBoot = val;
                } catch (System.Exception ex) {
                    _main!.Cfg.LaunchOnBoot = false;
                    tog.IsChecked = false;
                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastTaskFailed + ex.Message);
                }
                break;
            case "btnAutoTog":
                _main!.Cfg.AutoStart = val;
                break;
            case "btnStartMinTog":
                _main!.Cfg.StartMinimized = val;
                break;
            case "btnTrayTog":
                _main!.Cfg.MinimizeToTray = val;
                break;
            case "btnAdBlockTog":
                _main!.Cfg.EnableAdBlock = val;
                if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();
                break;
            case "btnLanTog":
                _main!.Cfg.AllowLanConnections = val;

                _main!.TriggerSmartRestartXray();
                break;
            case "btnDebugTog":
                _main!.Cfg.DebugMode = val;
                CrimsonOnion.Services.SimpleLogger.EnableLogging = val;
                break;
        }

        _main!.TriggerRequestConfigSave();
    }

private void SettingsLightDismiss_PointerPressed(object? sender, global::Avalonia.Input.PointerPressedEventArgs e) => _main?.TriggerSettingsLightDismiss_PointerPressed(sender, e);

private void Shortcut_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var btn = sender as global::Avalonia.Controls.Button;
        if (btn == null) return;

        try
        {
            Type? wshType = Type.GetTypeFromProgID("WScript.Shell");
            if (wshType == null) return;
            var ws = (dynamic)Activator.CreateInstance(wshType)!;

            string destPath = "";
            if (btn.Name == "btnDesktopShortcut")
            {
                destPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "CrimsonOnion.lnk");
            }
            else if (btn.Name == "btnStartMenuShortcut")
            {
                string programsPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.StartMenu), "Programs");
                if (!System.IO.Directory.Exists(programsPath)) System.IO.Directory.CreateDirectory(programsPath);
                destPath = System.IO.Path.Combine(programsPath, "CrimsonOnion.lnk");
            }

            dynamic sc = ws.CreateShortcut(destPath);
            sc.TargetPath = System.Environment.ProcessPath ?? "";
            sc.WorkingDirectory = _main!.Cfg.BaseDir;
            sc.Save();

            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastShortcutCreated, success: true);
        }
        catch
        {
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastShortcutFailed);
        }
    }

private void btnAdapterBindingToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togAdapterBinding") return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var pan = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBinding");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoAdapterBindingExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBindingToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnAdapterBindingToggle");
        if (pan != null)
        {
            if (pan.MaxHeight == 0)
            {
                AnimateExpander(pan, ico, true, 200, panToggle, btnToggle);

                var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbAdapters");
                if (cmb != null && cmb.Items.Count == 0)
                {
                    btnScanAdapters_Click(null, null);
                }
            }
            else
            {
                AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
            }
        }
    }

private void btnDnsToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togDnsSettings" || src.Name == "togSysDns")
                return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panDnsToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnDnsToggle");
        var pan       = this.FindControl<global::Avalonia.Controls.Border>("panDnsSettings");
        var ico       = this.FindControl<global::Avalonia.Controls.PathIcon>("icoDnsExpander");
        var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");

        if (pan != null && ico != null && cmbDohUrl != null)
        {
            if (pan.MaxHeight == 0)
            {
                cmbDohUrl.Text = _main!.Cfg.UpstreamDohUrl;

                var txtPrimary   = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
                var txtSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
                if (txtPrimary   != null) txtPrimary.Text   = _main!.Cfg.SystemDnsPrimary;
                if (txtSecondary != null) txtSecondary.Text = _main!.Cfg.SystemDnsSecondary;

                AnimateExpander(pan, ico, true, 340, panToggle, btnToggle);
            }
            else
            {
                CloseDnsPanel();
            }
        }
    }

private void btnDohSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");
        var tog       = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDnsSettings");

        if (cmbDohUrl != null && tog != null)
        {
            var url = cmbDohUrl.Text?.Trim() ?? "";
            _main!.Cfg.UpstreamDohUrl    = url;
            _main!.Cfg.EnableUpstreamDoh = true;

            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                tog.IsChecked = true;
            });

            _main!.TriggerRequestConfigSave();
            if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();
        }
    }

private void btnLanAuthSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanUser");
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
        var tog     = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togLanAuth");

        var user = txtUser?.Text?.Trim() ?? "";
        var pass = txtPass?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(user))
        {
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastEnterUsername);
            return;
        }

        _main!.Cfg.LanAuthUsername = user;
        _main!.Cfg.LanAuthPassword = pass;
        _main!.Cfg.EnableLanAuth   = true;

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            if (tog != null) tog.IsChecked = true;
        });

        _main!.TriggerRequestConfigSave();

        if (_main!.State.IsEngineRunning)
        {
            if (_main!.Cfg.LastXrayMode == XrayModes.VpnMode)
                _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
            else
                _main!.TriggerSmartRestartXray();
        }
        else
        {
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastCredentialsSaved, success: true);
        }
    }

private void btnLanPassEye_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
        var ico     = this.FindControl<global::Avalonia.Controls.PathIcon>("icoLanPassEye");
        if (txtPass == null) return;

        _lanPassVisible = !_lanPassVisible;
        txtPass.PasswordChar = _lanPassVisible ? '\0' : '\u2022';

        if (ico != null)
            ico.Data = _lanPassVisible
                ? global::Avalonia.Media.Geometry.Parse("M12 7c2.76 0 5 2.24 5 5 0 .65-.13 1.26-.36 1.83l2.92 2.92c1.51-1.26 2.7-2.89 3.43-4.75-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.16 2.16C10.74 7.13 11.35 7 12 7zM2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z")
                : global::Avalonia.Media.Geometry.Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");
    }

private void btnLanToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "btnLanTog" || src.Name == "togLanAuth") return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panLanToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnLanToggle");
        var pan       = this.FindControl<global::Avalonia.Controls.Border>("panLanSettings");
        var ico       = this.FindControl<global::Avalonia.Controls.PathIcon>("icoLanExpander");

        if (pan == null || ico == null) return;

        if (pan.MaxHeight == 0)
        {
            var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanUser");
            var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
            var tog     = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togLanAuth");
            if (txtUser != null) txtUser.Text = _main!.Cfg.LanAuthUsername;
            if (txtPass != null) txtPass.Text = _main!.Cfg.LanAuthPassword;
            if (tog     != null) tog.IsChecked = _main!.Cfg.EnableLanAuth;

            AnimateExpander(pan, ico, true, 160, panToggle, btnToggle);
        }
        else
        {
            AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
        }
    }

private void btnOutboundCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panOutboundToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnOutboundToggle");
        if (pan != null && ico != null)
        {
            AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
        }
    }

private void btnOutboundSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        var txtPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        var cmbType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        var togAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");

        if (txtAddr != null && tog != null)
        {
            var addr = txtAddr.Text?.Trim() ?? "";
            var port = txtPort?.Text?.Trim() ?? "";
            bool enable = tog.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(addr))
            {
                _main!.Cfg.OutboundProxyAddress = "";
                _main!.Cfg.OutboundProxyPort = "";
                _main!.Cfg.EnableOutboundProxy = enable;
                _main!.TriggerRequestConfigSave();
                btnOutboundCancel_Click(sender, e);
                if (_main!.State.IsEngineRunning) _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                return;
            }

            _main!.Cfg.OutboundProxyAddress = addr;
            _main!.Cfg.OutboundProxyPort = port;
            _main!.Cfg.OutboundProxyType = cmbType?.SelectedItem is global::Avalonia.Controls.ComboBoxItem pt ? (string?)pt.Content ?? "SOCKS5" : "SOCKS5";
            _main!.Cfg.EnableOutboundAuth = togAuth?.IsChecked ?? false;
            _main!.Cfg.OutboundProxyUser = txtUser?.Text?.Trim() ?? "";
            _main!.Cfg.OutboundProxyPass = txtPass?.Text?.Trim() ?? "";

            _main!.Cfg.EnableOutboundProxy = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                tog.IsChecked = true;
            });

            _main!.TriggerRequestConfigSave();
            btnOutboundCancel_Click(sender, e);
            if (_main!.State.IsEngineRunning) _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
        }
    }

private void btnOutboundToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togOutboundProxy")
                return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }

        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panOutboundToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnOutboundToggle");
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");

        var txtAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        var txtPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        var cmbType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        var togAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");
        var panAuth = this.FindControl<global::Avalonia.Controls.Border>("panOutboundAuth");

        if (pan != null && ico != null && txtAddr != null && tog != null && txtPort != null)
        {
            if (pan.MaxHeight == 0)
            {
                txtAddr.Text = _main!.Cfg.OutboundProxyAddress;
                txtPort.Text = _main!.Cfg.OutboundProxyPort;
                SelectComboItem(cmbType, string.IsNullOrEmpty(_main!.Cfg.OutboundProxyType) ? "SOCKS5" : _main!.Cfg.OutboundProxyType);
                if (togAuth != null) togAuth.IsChecked = _main!.Cfg.EnableOutboundAuth;
                if (txtUser != null) txtUser.Text = _main!.Cfg.OutboundProxyUser;
                if (txtPass != null) txtPass.Text = _main!.Cfg.OutboundProxyPass;
                tog.IsChecked = _main!.Cfg.EnableOutboundProxy;

                if (panAuth != null)
                {
                    if (_main!.Cfg.EnableOutboundAuth)
                    {
                        AnimateSubPanel(panAuth, true, 150);
                    }
                    else
                    {
                        AnimateSubPanel(panAuth, false, 0);
                    }
                }

                AnimateExpander(pan, ico, true, 350, panToggle, btnToggle);
            }
            else
            {
                AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
            }
        }
    }

private void btnScanAdapters_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs? e = null)
    {
        var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbAdapters");
        if (cmb == null) return;

        cmb.Items.Clear();
        foreach (var entry in SplitTunnelService.ListUsableAdapters(includeDefault: false)) cmb.Items.Add(entry);

        if (!string.IsNullOrWhiteSpace(_main!.Cfg.SelectedAdapterName) && !string.IsNullOrWhiteSpace(_main!.Cfg.SelectedAdapterIp))
        {
            var index = SplitTunnelService.FindAdapterIndex(cmb.Items.Cast<string>(), _main!.Cfg.SelectedAdapterName, _main!.Cfg.SelectedAdapterIp);
            if (index >= 0)
            {
                cmb.SelectedIndex = index;
            }
            else
            {
                _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastSelectedAdapterLost);
                _main!.Cfg.SelectedAdapterName = "";
                _main!.Cfg.SelectedAdapterIp = "";
                _main!.TriggerRequestConfigSave();

                if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            }
        }
        else if (cmb.Items.Count > 0)
        {
            cmb.SelectedIndex = 0;
        }
    }

private void btnSettingsClose_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _main?.TriggerCloseAllOverlays();
    }

private void btnSysDnsSave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txtPrimary   = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
        var txtSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
        var tog          = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togSysDns");

        var primary   = txtPrimary?.Text?.Trim()   ?? "";
        var secondary = txtSecondary?.Text?.Trim() ?? "";

        if (!DnsService.IsValidIpv4(primary))
        {
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastInvalidPrimaryDns);
            return;
        }
        if (!string.IsNullOrWhiteSpace(secondary) && !DnsService.IsValidIpv4(secondary))
        {
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastInvalidSecondaryDns);
            return;
        }

        _main!.Cfg.SystemDnsPrimary   = primary;
        _main!.Cfg.SystemDnsSecondary = secondary;
        _main!.Cfg.EnableSystemDns    = true;

        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            if (tog != null) tog.IsChecked = true;
        });

        _main!.TriggerRequestConfigSave();
        if (_main!.State.IsEngineRunning)
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectDns);
    }

private void cmbAdapters_SelectionChanged(object? sender, global::Avalonia.Controls.SelectionChangedEventArgs e)
    {
        var cmb = sender as global::Avalonia.Controls.ComboBox;
        if (cmb != null && cmb.SelectedItem is string selectedText && !string.IsNullOrWhiteSpace(selectedText))
        {
            if (SplitTunnelService.TryParseAdapterEntry(selectedText, out var newName, out var newIp))
            {
                bool changed = newIp != _main!.Cfg.SelectedAdapterIp;

                _main!.Cfg.SelectedAdapterName = newName;
                _main!.Cfg.SelectedAdapterIp = newIp;
                _main!.TriggerRequestConfigSave();

                if (changed && _main!.Cfg.EnableAdapterBinding && _main!.State.IsEngineRunning)
                {
                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
        }
    }

private void togAdapterBinding_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            if (tog.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_main!.Cfg.SelectedAdapterIp))
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                    var pan = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBinding");
                    if (pan != null && pan.MaxHeight == 0)
                    {
                        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoAdapterBindingExpander");
                        AnimateExpander(pan, ico, true, 200);

                        var cmb = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbAdapters");
                        if (cmb != null && cmb.Items.Count == 0)
                        {
                            btnScanAdapters_Click(null, null);
                        }
                    }
                    return;
                }
                else if (!_main!.Cfg.EnableAdapterBinding)
                {
                    _main!.Cfg.EnableAdapterBinding = true;
                    _main!.TriggerRequestConfigSave();
                    if (_main!.Cfg.LastBridge == BridgeNames.Snowflake)
                        _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastAdapterBindingSnowflake);
                    else if (_main!.State.IsEngineRunning)
                        _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            else
            {
                if (_main!.Cfg.EnableAdapterBinding)
                {
                    _main!.Cfg.EnableAdapterBinding = false;
                    _main!.TriggerRequestConfigSave();
                    if (_main!.State.IsEngineRunning) _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            UpdateAdapterBindingMutualExclusivity();
        }
    }

private void togDnsSettings_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        if (tog.IsChecked == true)
        {
            var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");
            var liveUrl   = cmbDohUrl?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(liveUrl))
                _main!.Cfg.UpstreamDohUrl = liveUrl;

            if (string.IsNullOrWhiteSpace(_main!.Cfg.UpstreamDohUrl))
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                return;
            }

            _main!.Cfg.EnableUpstreamDoh = true;
            _main!.TriggerRequestConfigSave();
            if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();
        }
        else
        {
            if (_main!.Cfg.EnableUpstreamDoh)
            {
                _main!.Cfg.EnableUpstreamDoh = false;
                _main!.TriggerRequestConfigSave();
                if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();
            }
        }
    }

private void togLanAuth_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        if (tog.IsChecked == true)
        {
            var txtUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanUser");
            var txtPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtLanPass");
            var liveUser = txtUser?.Text?.Trim() ?? "";
            var livePass = txtPass?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(liveUser))
            {
                _main!.Cfg.LanAuthUsername = liveUser;
                _main!.Cfg.LanAuthPassword = livePass;
            }

            if (string.IsNullOrWhiteSpace(_main!.Cfg.LanAuthUsername))
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                return;
            }

            _main!.Cfg.EnableLanAuth = true;
            _main!.TriggerRequestConfigSave();

            if (_main!.State.IsEngineRunning)
            {
                if (_main!.Cfg.LastXrayMode == XrayModes.VpnMode)
                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                else
                    _main!.TriggerSmartRestartXray();
            }
        }
        else
        {
            if (_main!.Cfg.EnableLanAuth)
            {
                _main!.Cfg.EnableLanAuth = false;
                _main!.TriggerRequestConfigSave();

                if (_main!.State.IsEngineRunning)
                {
                    if (_main!.Cfg.LastXrayMode == XrayModes.VpnMode)
                        _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                    else
                        _main!.TriggerSmartRestartXray();
                }
            }
        }
    }

private void togOutboundAuth_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        var panAuth = this.FindControl<global::Avalonia.Controls.Border>("panOutboundAuth");
        if (tog != null && panAuth != null)
        {
            if (tog.IsChecked == true)
            {
                AnimateSubPanel(panAuth, true, 150);
            }
            else
            {
                AnimateSubPanel(panAuth, false, 0);
            }
        }
    }

private void togOutboundProxy_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            if (tog.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_main!.Cfg.OutboundProxyAddress))
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                        tog.IsChecked = false;
                    });

                    var pan = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
                    var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");

                    if (pan != null && pan.MaxHeight == 0)
                    {
                        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panOutboundToggle");
                        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnOutboundToggle");
                        AnimateExpander(pan, ico, true, 350, panToggle, btnToggle);
                    }
                    return;
                }
                else if (!_main!.Cfg.EnableOutboundProxy)
                {
                    _main!.Cfg.EnableOutboundProxy = true;
                    _main!.TriggerRequestConfigSave();
                    if (_main!.State.IsEngineRunning) _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            else
            {
                if (_main!.Cfg.EnableOutboundProxy)
                {
                    _main!.Cfg.EnableOutboundProxy = false;
                    _main!.TriggerRequestConfigSave();
                    if (_main!.State.IsEngineRunning) _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectChanges);
                }
            }
            UpdateAdapterBindingMutualExclusivity();
        }
    }

private void togSysDns_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog == null) return;

        if (tog.IsChecked == true)
        {
            var txtPrimary   = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
            var txtSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
            var livePrimary   = txtPrimary?.Text?.Trim()   ?? "";
            var liveSecondary = txtSecondary?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(livePrimary))
            {
                if (!DnsService.IsValidIpv4(livePrimary))
                {
                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastInvalidPrimaryDns);
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                    return;
                }
                if (!string.IsNullOrWhiteSpace(liveSecondary) && !DnsService.IsValidIpv4(liveSecondary))
                {
                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastInvalidSecondaryDns);
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                    return;
                }
                _main!.Cfg.SystemDnsPrimary   = livePrimary;
                _main!.Cfg.SystemDnsSecondary = liveSecondary;
            }

            if (string.IsNullOrWhiteSpace(_main!.Cfg.SystemDnsPrimary))
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => { tog.IsChecked = false; });
                return;
            }

            _main!.Cfg.EnableSystemDns = true;
            _main!.TriggerRequestConfigSave();
            if (_main!.State.IsEngineRunning)
                _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectDns);
        }
        else
        {
            if (_main!.Cfg.EnableSystemDns)
            {
                _main!.Cfg.EnableSystemDns = false;
                _main!.TriggerRequestConfigSave();
                if (_main!.State.IsEngineRunning)
                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastReconnectDns);
            }
        }
    }

private void UpdateAdapterBindingMutualExclusivity()
    {
    }

    private bool _isInitializingSettings = false;

    public void UpdateSettingsUI()
    {
        if (_main == null) return;

        _isInitializingSettings = true;

        var btnBootTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnBootTog");
        if (btnBootTog != null) btnBootTog.IsChecked = _main.Cfg.LaunchOnBoot;

        var btnAutoTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnAutoTog");
        if (btnAutoTog != null) btnAutoTog.IsChecked = _main.Cfg.AutoStart;

        var btnStartMinTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnStartMinTog");
        if (btnStartMinTog != null) btnStartMinTog.IsChecked = _main.Cfg.StartMinimized;

        var btnTrayTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnTrayTog");
        if (btnTrayTog != null) btnTrayTog.IsChecked = _main.Cfg.MinimizeToTray;

        var togDnsSettings = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togDnsSettings");
        if (togDnsSettings != null) togDnsSettings.IsChecked = _main.Cfg.EnableUpstreamDoh;

        var cmbDohUrl = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbDohUrl");
        if (cmbDohUrl != null) cmbDohUrl.Text = _main.Cfg.UpstreamDohUrl;

        var togSysDns = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togSysDns");
        if (togSysDns != null) togSysDns.IsChecked = _main.Cfg.EnableSystemDns;

        var txtSysDnsPrimary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsPrimary");
        if (txtSysDnsPrimary != null) txtSysDnsPrimary.Text = _main.Cfg.SystemDnsPrimary;

        var txtSysDnsSecondary = this.FindControl<global::Avalonia.Controls.TextBox>("txtSysDnsSecondary");
        if (txtSysDnsSecondary != null) txtSysDnsSecondary.Text = _main.Cfg.SystemDnsSecondary;

        var btnAdBlockTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnAdBlockTog");
        if (btnAdBlockTog != null) btnAdBlockTog.IsChecked = _main.Cfg.EnableAdBlock;

        var btnLanTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnLanTog");
        if (btnLanTog != null) btnLanTog.IsChecked = _main.Cfg.AllowLanConnections;

        var togLanAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togLanAuth");
        if (togLanAuth != null) togLanAuth.IsChecked = _main.Cfg.EnableLanAuth;

        var btnDebugTog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("btnDebugTog");
        if (btnDebugTog != null) btnDebugTog.IsChecked = _main.Cfg.DebugMode;

        var togOutboundProxy = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundProxy");
        if (togOutboundProxy != null) togOutboundProxy.IsChecked = _main.Cfg.EnableOutboundProxy;

        var togXrayExitNode = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");
        if (togXrayExitNode != null) togXrayExitNode.IsChecked = _main.Cfg.EnableV2rayChain;

        var panOutboundProxy = this.FindControl<global::Avalonia.Controls.Border>("panOutboundProxy");
        var icoOutboundExpander = this.FindControl<global::Avalonia.Controls.PathIcon>("icoOutboundExpander");
        var cmbOutboundType = this.FindControl<global::Avalonia.Controls.ComboBox>("cmbOutboundType");
        var txtOutboundAddr = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundAddr");
        var txtOutboundPort = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPort");
        var togOutboundAuth = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togOutboundAuth");
        var panOutboundAuth = this.FindControl<global::Avalonia.Controls.Border>("panOutboundAuth");
        var txtOutboundUser = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundUser");
        var txtOutboundPass = this.FindControl<global::Avalonia.Controls.TextBox>("txtOutboundPass");
        var togAdapterBinding = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togAdapterBinding");
        var panAdapterBinding = this.FindControl<global::Avalonia.Controls.Border>("panAdapterBinding");
        var icoAdapterBindingExpander = this.FindControl<global::Avalonia.Controls.PathIcon>("icoAdapterBindingExpander");

        if (togOutboundProxy != null)
        {
            togOutboundProxy.IsChecked = _main.Cfg.EnableOutboundProxy;
            if (_main.Cfg.EnableOutboundProxy && panOutboundProxy != null && icoOutboundExpander != null)
            {
            }
            if (cmbOutboundType != null) cmbOutboundType.SelectedIndex = _main.Cfg.OutboundProxyType == "HTTPS" ? 1 : 0;
            if (txtOutboundAddr != null) txtOutboundAddr.Text = _main.Cfg.OutboundProxyAddress;
            if (txtOutboundPort != null) txtOutboundPort.Text = _main.Cfg.OutboundProxyPort;
            if (togOutboundAuth != null)
            {
                togOutboundAuth.IsChecked = _main.Cfg.EnableOutboundAuth;
                if (_main.Cfg.EnableOutboundAuth && panOutboundAuth != null)
                {
                }
            }
            if (txtOutboundUser != null) txtOutboundUser.Text = _main.Cfg.OutboundProxyUser;
            if (txtOutboundPass != null) txtOutboundPass.Text = _main.Cfg.OutboundProxyPass;
        }

        if (togAdapterBinding != null)
        {
            togAdapterBinding.IsChecked = _main.Cfg.EnableAdapterBinding;
            UpdateAdapterBindingMutualExclusivity();
        }

        _isInitializingSettings = false;
    }

private static void AnimateExpander(
    global::Avalonia.Controls.Border? pan,
    global::Avalonia.Controls.PathIcon? ico,
    bool expand,
    double expandedHeight,
    global::Avalonia.Controls.Border? panToggle = null,
    global::Avalonia.Controls.Button? btnToggle = null)
{
    if (pan == null) return;

    pan.MaxHeight = expand ? expandedHeight : 0;
    pan.Opacity   = expand ? 1 : 0;

    if (ico != null)
        ico.RenderTransform = new global::Avalonia.Media.RotateTransform(expand ? 180 : 0);

    var toggleCorner = expand
        ? new global::Avalonia.CornerRadius(8, 8, 0, 0)
        : new global::Avalonia.CornerRadius(8);
    if (panToggle != null) panToggle.CornerRadius = toggleCorner;
    if (btnToggle != null) btnToggle.CornerRadius = toggleCorner;
}

private static void AnimateSubPanel(global::Avalonia.Controls.Border? pan, bool expand, double expandedHeight)
{
    if (pan == null) return;

    pan.MaxHeight = expand ? expandedHeight : 0;
    pan.Opacity   = expand ? 1 : 0;
}

private void CloseDnsPanel()
    {
        var pan       = this.FindControl<global::Avalonia.Controls.Border>("panDnsSettings");
        var ico       = this.FindControl<global::Avalonia.Controls.PathIcon>("icoDnsExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panDnsToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnDnsToggle");
        if (pan != null && ico != null)
        {
            AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
        }
    }

private void SelectComboItem(global::Avalonia.Controls.ComboBox? combo, string content)
    {
        if (combo == null) return;
        foreach (var itemObj in combo.Items)
        {
            if (itemObj is global::Avalonia.Controls.ComboBoxItem item)
            {
                if ((string?)item.Content == content) { combo.SelectedItem = item; return; }
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void BtnLbPolicy_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => _main?.TriggerBtnLbPolicy_Click(sender, e);

private void btnXrayExitNodeToggle_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var src = e.Source as global::Avalonia.Controls.Control;
        while (src != null)
        {
            if (src.Name == "togXrayExitNode")
                return;
            src = src.Parent as global::Avalonia.Controls.Control;
        }
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNodeToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnXrayExitNodeToggle");
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNode");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoXrayExitNodeExpander");
        var txt = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayJson");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");

        if (pan != null && ico != null && txt != null && tog != null)
        {
            if (pan.MaxHeight == 0)
            {
                txt.Text = _main!.Cfg.V2rayChainJson;
                tog.IsChecked = _main!.Cfg.EnableV2rayChain;

                AnimateExpander(pan, ico, true, 350, panToggle, btnToggle);
            }
            else
            {
                AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
            }
        }
    }

private void txtXrayJson_TextChanged(object? sender, global::Avalonia.Controls.TextChangedEventArgs e)
    {
        var txt = sender as global::Avalonia.Controls.TextBox;
        if (txt == null || string.IsNullOrWhiteSpace(txt.Text)) return;

        string text = txt.Text.Trim();

        if (text.StartsWith("vless://") || text.StartsWith("vmess://") || text.StartsWith("trojan://") || text.StartsWith("ss://") || text.StartsWith("socks://"))
        {
            if (text.Contains("security=reality", StringComparison.OrdinalIgnoreCase))
            {
                _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastRealityNotSupported);
                return;
            }
            if (text.Contains("type=kcp", StringComparison.OrdinalIgnoreCase) || text.Contains("net=kcp", StringComparison.OrdinalIgnoreCase) || text.Contains("type=quic", StringComparison.OrdinalIgnoreCase) || text.Contains("net=quic", StringComparison.OrdinalIgnoreCase))
            {
                _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastKcpQuicNotSupported);
                return;
            }
        }

        if (CrimsonOnion.Services.XrayLinkParser.TryParseLink(text, out string json))
        {
            txt.Text = json;
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastLinkConverted, success: true);
        }
    }

private async void btnXrayImport_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = global::Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new global::Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Xray JSON File",
                AllowMultiple = false,
                FileTypeFilter = new[] { new global::Avalonia.Platform.Storage.FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } }, new global::Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } } }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                var path = file.Path.LocalPath;
                if (System.IO.File.Exists(path))
                {
                    var txt = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayJson");
                    if (txt != null)
                        txt.Text = System.IO.File.ReadAllText(path);
                }
            }
        }
        catch
        {
            _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastFailedImport);
        }
    }

private async void btnXraySave_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var txt = this.FindControl<global::Avalonia.Controls.TextBox>("txtXrayJson");
        var tog = this.FindControl<global::Avalonia.Controls.ToggleSwitch>("togXrayExitNode");

        if (txt != null && tog != null)
        {
            var text = txt.Text ?? "";
            bool enable = tog.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(text))
            {
                _main!.Cfg.V2rayChainJson = "";
                _main!.Cfg.EnableV2rayChain = enable;
                ConfigService.Save(_main!.Cfg, _main!.State, _main!.Cfg.CfgFile, _main!.Cfg.LastConfig, _main!.Cfg.LastBridge, _main!.Cfg.LastCount);

                btnXrayCancel_Click(sender, e);
                return;
            }

            try
            {
                var parsed = Newtonsoft.Json.Linq.JObject.Parse(text);
                Newtonsoft.Json.Linq.JToken? testNode = parsed["outbounds"] is Newtonsoft.Json.Linq.JArray arr ? arr.FirstOrDefault() : parsed;
                if (testNode?["protocol"] == null)
                    throw new Exception("Missing 'protocol' field.");

                var streamSettings = testNode["streamSettings"];
                if (streamSettings != null)
                {
                    if (streamSettings["security"]?.ToString()?.ToLowerInvariant() == "reality")
                    {
                        _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastRealityNotSupported);
                        return;
                    }

                    var net = streamSettings["network"]?.ToString()?.ToLowerInvariant();
                    if (net == "kcp" || net == "quic")
                    {
                        _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastKcpQuicNotSupported);
                        return;
                    }
                }

                var settings = testNode["settings"];
                if (settings != null)
                {
                    var ports = settings.SelectTokens("..port").ToList();
                    foreach (var portToken in ports)
                    {
                        if (int.TryParse(portToken.ToString(), out int port))
                        {
                            if (port != 80 && port != 443)
                            {
                                bool isLocal = false;
                                var parentObj = portToken.Parent?.Parent as Newtonsoft.Json.Linq.JObject;
                                if (parentObj != null && parentObj["address"] != null)
                                {
                                    string addr = parentObj["address"]?.ToString()?.ToLowerInvariant() ?? "";
                                    if (addr == "localhost" || addr == "127.0.0.1" || addr == "::1")
                                    {
                                        isLocal = true;
                                    }
                                    else if (System.Net.IPAddress.TryParse(addr, out var ip))
                                    {
                                        byte[] bytes = ip.GetAddressBytes();
                                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                        {
                                            if (bytes[0] == 10 || 
                                                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || 
                                                (bytes[0] == 192 && bytes[1] == 168))
                                            {
                                                isLocal = true;
                                            }
                                        }
                                        else if (System.Net.IPAddress.IsLoopback(ip))
                                        {
                                            isLocal = true;
                                        }
                                    }
                                }

                                if (!isLocal)
                                {
                                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastPortsSupported);
                                    return;
                                }
                            }
                        }
                    }
                }

                string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
                try
                {
                    System.IO.File.WriteAllText(tempFile, text);

                    string xrayExe = System.IO.Path.Combine(_main!.Cfg.BaseDir, "Data", "xray", "xray.exe");
                    if (System.IO.File.Exists(xrayExe))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = xrayExe,
                            Arguments = $"-test -config \"{tempFile}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var proc = System.Diagnostics.Process.Start(psi))
                        {
                            if (proc != null)
                            {
                                var outTask = proc.StandardOutput.ReadToEndAsync();
                                var errTask = proc.StandardError.ReadToEndAsync();
                                await proc.WaitForExitAsync();
                                if (proc.ExitCode != 0)
                                {
                                    string err = await errTask;
                                    string outStr = await outTask;
                                    string msg = string.IsNullOrWhiteSpace(err) ? outStr : err;
                                    var lines = msg.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    msg = string.Join(" ", lines.Where(l => !l.Contains("Xray, Penetrates Everything") && !l.Contains("unified platform")));
                                    msg = msg.Trim();
                                    _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastXrayRejected + msg.Substring(0, System.Math.Min(msg.Length, 150)));
                                    return;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    try { if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile); } catch (Exception ex) { CrimsonOnion.Services.SimpleLogger.Log(ex); }
                }

                _main!.Cfg.V2rayChainJson = text.Trim();
                _main!.Cfg.EnableV2rayChain = true;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    tog.IsChecked = true;
                });

                ConfigService.Save(_main!.Cfg, _main!.State, _main!.Cfg.CfgFile, _main!.Cfg.LastConfig, _main!.Cfg.LastBridge, _main!.Cfg.LastCount);
                if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();

                btnXrayCancel_Click(sender, e);
            }
            catch (Exception ex)
            {
                CrimsonOnion.Services.SimpleLogger.Log(ex);
                _main!.TriggerShowToast(CrimsonOnion.Localization.AppStrings.ToastInvalidJson + " " + ex.Message);
            }
        }
    }

private void btnXrayCancel_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var pan = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNode");
        var ico = this.FindControl<global::Avalonia.Controls.PathIcon>("icoXrayExitNodeExpander");
        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNodeToggle");
        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnXrayExitNodeToggle");
        if (pan != null && ico != null)
        {
            AnimateExpander(pan, ico, false, 0, panToggle, btnToggle);
        }
    }

private void togXrayExitNode_IsCheckedChanged(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isInitializingSettings) return;
        var tog = sender as global::Avalonia.Controls.ToggleSwitch;
        if (tog != null)
        {
            if (tog.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_main!.Cfg.V2rayChainJson))
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() => tog.IsChecked = false);

                    var panXrayExitNode = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNode");
                    var icoXrayExitNodeExpander = this.FindControl<global::Avalonia.Controls.PathIcon>("icoXrayExitNodeExpander");

                    if (panXrayExitNode != null && panXrayExitNode.MaxHeight == 0)
                    {
                        var panToggle = this.FindControl<global::Avalonia.Controls.Border>("panXrayExitNodeToggle");
                        var btnToggle = this.FindControl<global::Avalonia.Controls.Button>("btnXrayExitNodeToggle");
                        AnimateExpander(panXrayExitNode, icoXrayExitNodeExpander, true, 500, panToggle, btnToggle);
                    }
                    return;
                }
                else if (!_main!.Cfg.EnableV2rayChain)
                {
                    _main!.Cfg.EnableV2rayChain = true;
                    _main!.TriggerRequestConfigSave();
                    if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();
                }
            }
            else
            {
                if (_main!.Cfg.EnableV2rayChain)
                {
                    _main!.Cfg.EnableV2rayChain = false;
                    _main!.TriggerRequestConfigSave();
                    if (_main!.State.IsEngineRunning) _main!.TriggerSmartRestartXray();
                }
            }
        }
    }
    }
}




