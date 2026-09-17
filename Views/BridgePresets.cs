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

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CrimsonOnion.Localization;
using CrimsonOnion.Models;
using CrimsonOnion.Services;

namespace CrimsonOnion.Views
{
    internal sealed class BridgePresets
    {
        internal const string Direct    = BridgeNames.Direct;
        internal const string Obfs4     = BridgeNames.Obfs4;
        internal const string Snowflake = BridgeNames.Snowflake;
        internal const string MeekLite  = BridgeNames.MeekLite;
        internal const string Conjure   = BridgeNames.Conjure;
        internal const string Custom    = BridgeNames.Custom;
        private const string DirectButton    = "btnBridgeDirect";
        private const string Obfs4Button     = "btnBridgeObfs4";
        private const string SnowflakeButton = "btnBridgeSnowflake";
        private const string MeekButton      = "btnBridgeMeek";
        private const string ConjureButton   = "btnBridgeConjure";
        private const string CustomButton    = "btnBridgeCustom";
        private static readonly (string Bridge, string Button)[] Presets =
        {
            (Direct,    DirectButton),
            (Obfs4,     Obfs4Button),
            (Snowflake, SnowflakeButton),
            (MeekLite,  MeekButton),
            (Conjure,   ConjureButton),
            (Custom,    CustomButton)
        };
        private const string CustomPanel = "panCustomBridge";
        private const string CustomBox   = "txtCustomBridge";
        private const string AdvancedPanel  = "panAdvancedBridges";
        private const string AmpCacheButton = "btnAmpCacheMode";
        private const string DnsRegDiv      = "panDnsRegDiv";
        private const string DnsRegButton   = "btnDnsRegMode";
        private static readonly string[] AdvancedOptions = { AmpCacheButton, DnsRegButton };
        private const string WebTunnelButton  = "btnGetWebTunnel";
        private const string Obfs4FetchButton = "btnGetObfs4";
        private const string CaptchaPanel     = "panCaptcha";
        private const string CaptchaImage     = "imgCaptcha";
        private const string CaptchaBox       = "txtCaptchaSol";
        private const string CaptchaSubmit    = "btnCaptchaSubmit";
        private const string ActiveOptionClass = "activeOpt";
        private const string ActiveModeClass   = "activeMode";
        private const string NoTransitionClass = "notrans";
        private const string WebTunnelRequest = MoatTransports.WebTunnel;
        private const string Obfs4Request     = MoatTransports.Obfs4;
        private const string FetchingLabel  = "FETCHING...";
        private const string WebTunnelLabel = "WEBTUNNEL";
        private const string Obfs4Label     = "OBFS4";
        private const double CustomPanelOpenHeight = 500;
        private const int AdvancedPanelRepaintMs = 150;
        private const int AdvancedPanelTransitionMs = 50;

        private readonly AppConfig _cfg;
        private readonly AppState _state;
        private readonly MoatBridgeClient _moat;

        private readonly Func<string, Control?> _find;
        private readonly Action<string> _toast;
        private readonly Action _save;
        private readonly Action _reloadSettings;
        private readonly Func<string> _mode;
        private readonly Action<string> _setMode;
        private readonly Action<string> _applyMode;
        private readonly Func<bool> _vpnModeUnavailable;
        internal string Active { get; private set; }
        internal BridgePresets(
            AppConfig cfg,
            AppState state,
            MoatBridgeClient moat,
            Func<string, Control?> find,
            Action<string> toast,
            Action save,
            Action reloadSettings,
            Func<string> mode,
            Action<string> setMode,
            Action<string> applyMode,
            Func<bool> vpnModeUnavailable)
        {
            _cfg                = cfg;
            _state              = state;
            _moat               = moat;
            _find               = find;
            _toast              = toast;
            _save               = save;
            _reloadSettings     = reloadSettings;
            _mode               = mode;
            _setMode            = setMode;
            _applyMode          = applyMode;
            _vpnModeUnavailable = vpnModeUnavailable;

            Active = string.IsNullOrEmpty(cfg.LastBridge) ? Direct : cfg.LastBridge;
        }
        internal void UpdateBridgeUI()
        {
            Highlight(ButtonOf(Active));

            if (_find(CustomBox) is TextBox box) box.Text = _cfg.CustomBridgeLine;
        }
        private void Highlight(string buttonName)
        {
            foreach (var (_, button) in Presets)
                if (_find(button) is Button preset) preset.Classes.Remove(ActiveOptionClass);

            if (_find(buttonName) is Button lit) lit.Classes.Add(ActiveOptionClass);
        }
        private static string ButtonOf(string bridge)
        {
            foreach (var (name, button) in Presets)
                if (name == bridge) return button;

            return DirectButton;
        }
        private static string? BridgeOf(string? buttonName)
        {
            foreach (var (bridge, button) in Presets)
                if (button == buttonName) return bridge;

            return null;
        }
        internal void SelectBridge(Button? clicked)
        {
            if (clicked is null) return;

            string target = BridgeOf(clicked.Name) ?? Active;

            if (clicked.Name != CustomButton && Active == target)
            {
                if (_find(CustomPanel) is Border pane && pane.MaxHeight > 0)
                {
                    CloseCustomPanel();
                    Highlight(clicked.Name!);
                }
                return;
            }

            Highlight(clicked.Name!);

            if (clicked.Name == CustomButton)
            {
                if (_find(CustomPanel) is Border openPane && openPane.MaxHeight > 0)
                {
                    SaveCustom();
                    return;
                }

                OpenCustomPanel();
                UpdateAdvancedUI(forceHide: true);
                return;
            }

            Active          = target;
            _cfg.LastBridge = Active;
            _save();

            UpdateAdvancedUI();

            UpdateModeUI();

            if (Active == Snowflake && _cfg.EnableAdapterBinding)
            {
                _toast(AppStrings.ToastAdapterBindingSnowflake);
            }

            if (Active == Custom) OpenCustomPanel(); else CloseCustomPanel();

            if (_state.IsEngineRunning) _toast(AppStrings.ToastReconnectChanges);
        }
        internal void UpdateModeUI()
        {
            if (_vpnModeUnavailable() && _cfg.LastXrayMode == XraySupervisor.VpnMode)
            {
                _cfg.LastXrayMode = XraySupervisor.ProxyMode;
                _setMode(XraySupervisor.ProxyMode);
                _toast(AppStrings.ToastVpnDisabledSnowflake);
            }

            _applyMode(_mode());
        }
        private void OpenCustomPanel()
        {
            if (_find(CustomPanel) is Border pane)
            {
                pane.MaxHeight       = CustomPanelOpenHeight;
                pane.Opacity         = 1;
                pane.BorderThickness = new Thickness(1);
            }

            if (_find(CustomBox) is TextBox box) box.Text = _cfg.CustomBridgeLine;
        }
        private void CloseCustomPanel()
        {
            if (_find(CustomPanel) is Border pane)
            {
                pane.MaxHeight       = 0;
                pane.Opacity         = 0;
                pane.BorderThickness = new Thickness(0);
            }
        }
        internal void SaveCustom()
        {
            if (_find(CustomBox) is not TextBox box || string.IsNullOrWhiteSpace(box.Text))
            {
                CloseCustomPanel();
                _reloadSettings();
                UpdateAdvancedUI(forceHide: false);
                return;
            }

            _cfg.CustomBridgeLine = box.Text.Trim();
            Active                = Custom;
            _cfg.LastBridge       = Active;
            ConfigService.Save(_cfg, _state, _cfg.CfgFile, _cfg.LastConfig, _cfg.LastBridge, _cfg.LastCount);
            _save();

            UpdateAdvancedUI();

            bool dnsttBypass = (_cfg.EnableAdapterBinding || _cfg.EnableOutboundProxy)
                            && _cfg.CustomBridgeLine.IndexOf("dnstt", StringComparison.OrdinalIgnoreCase) >= 0;

            if (_state.IsEngineRunning)
                _toast(dnsttBypass ? AppStrings.ToastDnsttBridgeWarning1 : AppStrings.ToastReconnectChanges);
            else if (dnsttBypass)
                _toast(AppStrings.ToastDnsttBridgeWarning2);

            CloseCustomPanel();
        }
        internal void CancelCustom()
        {
            CloseCustomPanel();
            _reloadSettings();
            UpdateAdvancedUI(forceHide: false);
        }
        internal void ToggleAdvancedMode(Button? clicked)
        {
            if (clicked is null) return;

            if (clicked.Classes.Contains(ActiveModeClass))
            {
                clicked.Classes.Remove(ActiveModeClass);
            }
            else if (AdvancedOptions.Contains(clicked.Name))
            {
                clicked.Classes.Add(ActiveModeClass);

                foreach (string name in AdvancedOptions)
                    if (name != clicked.Name && _find(name) is Button other) other.Classes.Remove(ActiveModeClass);
            }

            if (Active == Snowflake)
            {
                _cfg.EnableSnowflakeAmpCache = IsLit(AmpCacheButton);
            }
            else if (Active == Conjure)
            {
                _cfg.EnableConjureAmpCache        = IsLit(AmpCacheButton);
                _cfg.EnableConjureDnsRegistration = IsLit(DnsRegButton);
            }

            _save();

            if (_state.IsEngineRunning) _toast(AppStrings.ToastReconnectChanges);
        }
        private bool IsLit(string name) =>
            _find(name) is Button option && option.Classes.Contains(ActiveModeClass);
        private static void SetLit(Button option, bool lit)
        {
            if (lit) option.Classes.Add(ActiveModeClass);
            else option.Classes.Remove(ActiveModeClass);
        }
        private void SetNoTransition(bool off)
        {
            foreach (string name in AdvancedOptions)
                if (_find(name) is Button option)
                {
                    if (off) option.Classes.Add(NoTransitionClass);
                    else option.Classes.Remove(NoTransitionClass);
                }
        }
        internal async void UpdateAdvancedUI(bool forceHide = false)
        {
            if (_find(AdvancedPanel) is not Border panel
                || _find(DnsRegDiv) is not Border dnsRegDiv
                || _find(DnsRegButton) is not Button dnsRegButton
                || _find(AmpCacheButton) is not Button ampCacheButton) return;

            bool show      = !forceHide && (Active == Snowflake || Active == Conjure);
            bool isConjure = Active == Conjure;

            void ApplyState()
            {
                SetNoTransition(true);

                if (isConjure)
                {
                    SetLit(ampCacheButton, _cfg.EnableConjureAmpCache);
                    SetLit(dnsRegButton, _cfg.EnableConjureDnsRegistration);
                }
                else
                {
                    SetLit(ampCacheButton, _cfg.EnableSnowflakeAmpCache);
                }

                dnsRegDiv.IsVisible    = isConjure;
                dnsRegButton.IsVisible = isConjure;
                ampCacheButton.SetValue(Grid.ColumnSpanProperty, isConjure ? 1 : 2);
            }

            async void FinishState()
            {
                await Task.Delay(AdvancedPanelTransitionMs);
                SetNoTransition(false);
            }

            if (show)
            {
                if (panel.Opacity > 0 && dnsRegDiv.IsVisible != isConjure)
                {
                    panel.Opacity = 0.0;
                    await Task.Delay(AdvancedPanelRepaintMs);
                    ApplyState();
                    panel.Opacity = 1.0;
                    FinishState();
                }
                else
                {
                    ApplyState();
                    panel.Opacity = 1.0;
                    FinishState();
                }

                panel.IsHitTestVisible = true;
            }
            else
            {
                panel.Opacity          = 0.0;
                panel.IsHitTestVisible = false;
            }
        }
        internal void FetchWebTunnel() => StartFetch(webTunnel: true);
        internal void FetchObfs4() => StartFetch(webTunnel: false);
        private void StartFetch(bool webTunnel)
        {
            if (_moat.IsFetching) { CancelFetch(); return; }

            if (_find(WebTunnelButton) is Button webTunnelButton && _find(Obfs4FetchButton) is Button obfs4Button)
            {
                if (webTunnel)
                {
                    webTunnelButton.Content = FetchingLabel;
                    obfs4Button.IsEnabled   = false;
                }
                else
                {
                    obfs4Button.Content       = FetchingLabel;
                    webTunnelButton.IsEnabled = false;
                }
            }

            _moat.Begin(webTunnel ? WebTunnelRequest : Obfs4Request);
            _ = FetchBridgesAsync();
        }
        private async Task FetchBridgesAsync()
        {
            var status = await _moat.FetchChallengeAsync();

            if (status == MoatStatus.Ok && _moat.ChallengeImage is byte[] image)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_find(CaptchaImage) is Image picture)
                    {
                        (picture.Source as Bitmap)?.Dispose();
                        using var ms = new MemoryStream(image);
                        picture.Source = new Bitmap(ms);
                    }

                    if (_find(CaptchaPanel) is Border pane)
                    {
                        pane.MaxHeight       = 300;
                        pane.MaxWidth        = 160;
                        pane.Margin          = new Thickness(0, 0, 10, 0);
                        pane.Opacity         = 1;
                        pane.BorderThickness = new Thickness(1);
                    }

                    if (_find(CaptchaBox) is TextBox box) { box.Text = ""; box.Focus(); }
                    if (_find(CaptchaSubmit) is Button submit) { submit.Content = AppStrings.Submit; submit.IsEnabled = true; }
                });
                return;
            }

            if (status == MoatStatus.Unreachable)
            {
                CancelFetch();
                _ = Dispatcher.UIThread.InvokeAsync(() => _toast(AppStrings.ToastFailedToReachTor));
            }
        }
        private void CancelFetch()
        {
            _moat.Cancel();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_find(WebTunnelButton) is Button webTunnelButton)
                {
                    webTunnelButton.Content   = WebTunnelLabel;
                    webTunnelButton.IsEnabled = true;
                }

                if (_find(Obfs4FetchButton) is Button obfs4Button)
                {
                    obfs4Button.Content   = Obfs4Label;
                    obfs4Button.IsEnabled = true;
                }

                if (_find(CaptchaSubmit) is Button submit)
                {
                    submit.Content   = AppStrings.Submit;
                    submit.IsEnabled = true;
                }

                if (_find(CaptchaPanel) is Border pane)
                {
                    pane.MaxHeight       = 0;
                    pane.MaxWidth        = 0;
                    pane.Margin          = new Thickness(0);
                    pane.Opacity         = 0;
                    pane.BorderThickness = new Thickness(0);
                }
            });
        }
        internal void CancelCaptcha() => CancelFetch();
        internal void SubmitCaptcha() => _ = SubmitCaptchaAsync();
        internal void CaptchaKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                _ = SubmitCaptchaAsync();
                e.Handled = true;
            }
        }
        private async Task SubmitCaptchaAsync()
        {
            string solution = _find(CaptchaBox) is TextBox captchaBox ? captchaBox.Text?.Trim() ?? "" : "";
            if (string.IsNullOrWhiteSpace(solution)) return;

            if (_find(CaptchaSubmit) is Button submit)
            {
                submit.Content   = AppStrings.CaptchaVerifying;
                submit.IsEnabled = false;
            }

            var status = await _moat.SubmitSolutionAsync(solution);

            if (status == MoatStatus.Cancelled) return;

            if (status == MoatStatus.Ok && _moat.BridgeLines is string lines)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_find(CustomBox) is TextBox customBox)
                    {
                        string existing = customBox.Text?.Trim() ?? "";
                        customBox.Text     = string.IsNullOrWhiteSpace(existing) ? lines : $"{existing}\n{lines}";
                        customBox.CaretIndex = customBox.Text.Length;
                    }

                    CancelFetch();
                });
                return;
            }

            CancelFetch();
            if (status == MoatStatus.Unreachable)
                _ = Dispatcher.UIThread.InvokeAsync(() => _toast(AppStrings.ToastFailedToReachTor));
        }
    }
}
