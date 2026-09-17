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

namespace CrimsonOnion.Localization
{
    internal enum LocalizeTarget
    {
        Text,
        Button,
        ToolTip
    }
    internal sealed class LocalizeStep
    {
        internal LocalizeStep(
            LocalizeTarget target,
            string name,
            Func<string> value,
            bool forceLtr = false,
            bool keepFont = false,
            bool disconnectedOnly = false,
            bool leftAlign = false)
        {
            Target = target;
            Name = name;
            Value = value;
            ForceLtr = forceLtr;
            KeepFont = keepFont;
            DisconnectedOnly = disconnectedOnly;
            LeftAlign = leftAlign;
        }

        internal LocalizeTarget Target { get; }

        internal string Name { get; }

        internal Func<string> Value { get; }
        internal bool ForceLtr { get; }
        internal bool KeepFont { get; }
        internal bool DisconnectedOnly { get; }
        internal bool LeftAlign { get; }
    }
    internal sealed class MainWindowLocalizer
    {
        private readonly Func<string, Control?> _find;
        internal MainWindowLocalizer(Func<string, Control?> find) => _find = find;
        internal void ApplyLanguage(string language, bool connected, bool engineRunning)
        {
            AppStrings.SetLanguage(language);

            foreach (var step in Steps)
            {
                if (step.DisconnectedOnly && connected) continue;

                switch (step.Target)
                {
                    case LocalizeTarget.Text:
                        AppStrings.Apply(_find(step.Name) as TextBlock, step.Value(), step.ForceLtr, step.KeepFont, step.LeftAlign);
                        break;

                    case LocalizeTarget.Button:
                        AppStrings.ApplyBtn(_find(step.Name) as Button, step.Value());
                        break;

                    default:
                        AppStrings.ApplyToolTip(_find(step.Name), step.Value());
                        break;
                }
            }

            ApplyConnectLabel(connected, engineRunning);
        }
        private void ApplyConnectLabel(bool connected, bool engineRunning)
        {
            var txtConnectBtn = _find("txtConnectBtn") as TextBlock;

            if (_find("btnConnect") != null && txtConnectBtn != null)
            {
                txtConnectBtn.FlowDirection = AppStrings.IsPersian
                    ? global::Avalonia.Media.FlowDirection.RightToLeft
                    : global::Avalonia.Media.FlowDirection.LeftToRight;
            }

            var txtConnectedBtn = _find("txtConnectedBtn") as TextBlock;
            if (connected)
            {
                if (txtConnectedBtn != null) txtConnectedBtn.Text = AppStrings.ConnectedBtn;
                if (txtConnectBtn != null) txtConnectBtn.Text = AppStrings.ConnectedBtn;
            }
            else if (engineRunning)
            {
                if (txtConnectBtn != null) txtConnectBtn.Text = AppStrings.BtnConnecting;
            }
            else
            {
                if (txtConnectBtn != null) txtConnectBtn.Text = AppStrings.Connect;
            }
        }
        internal static IReadOnlyList<LocalizeStep> Steps { get; } = new LocalizeStep[]
        {
            new(LocalizeTarget.Text, "lblSidebarConnection",        () => AppStrings.Connect),
            new(LocalizeTarget.Text, "lblSidebarCountries",         () => AppStrings.SidebarCountries),
            new(LocalizeTarget.Text, "lblSidebarSplitTunnel",       () => AppStrings.SidebarSplitTunnel),
            new(LocalizeTarget.Text, "lblSidebarSettings",          () => AppStrings.SidebarSettings),
            new(LocalizeTarget.Text, "lblSidebarAbout",             () => AppStrings.SidebarAbout),

            new(LocalizeTarget.Text, "lblThemesHeader",             () => AppStrings.SidebarThemes, forceLtr: true),
            new(LocalizeTarget.Text, "lblPauseGlow",                () => AppStrings.ThemesPauseGlow, leftAlign: true),
            new(LocalizeTarget.Text, "lblDisableGlow",              () => AppStrings.ThemesDisableGlow, leftAlign: true),

            new(LocalizeTarget.Text, "lblProxyMode",                () => AppStrings.ProxyMode),
            new(LocalizeTarget.Text, "lblVpnMode",                  () => AppStrings.VpnMode),
            new(LocalizeTarget.Text, "lblClearProxy",               () => AppStrings.ClearProxy),

            new(LocalizeTarget.ToolTip, "btnProxyMode",             () => AppStrings.TtProxyMode),
            new(LocalizeTarget.ToolTip, "btnVpnMode",               () => AppStrings.TtVpnMode),
            new(LocalizeTarget.ToolTip, "btnClearProxy",            () => AppStrings.TtClearProxy),

            new(LocalizeTarget.ToolTip, "btnLbLeastLoad",           () => AppStrings.TtLbLeastLoad),
            new(LocalizeTarget.ToolTip, "btnLbRoundRobin",          () => AppStrings.TtLbRoundRobin),
            new(LocalizeTarget.ToolTip, "btnLbLeastPing",           () => AppStrings.TtLbLeastPing),
            new(LocalizeTarget.ToolTip, "btnLbRandom",              () => AppStrings.TtLbRandom),

            new(LocalizeTarget.Text, "lblBridgeType",               () => AppStrings.BridgeType),
            new(LocalizeTarget.Text, "lblGetBridges",               () => AppStrings.GetBridges),
            new(LocalizeTarget.Text, "lblTorEngines",               () => AppStrings.TorEngines),
            new(LocalizeTarget.Text, "lblLogsStatus",               () => AppStrings.LogsStatus),
            new(LocalizeTarget.Text, "lblTorBootstrap",             () => AppStrings.TorBootstrap),
            new(LocalizeTarget.Text, "lblXrayLogHeader",            () => AppStrings.XrayLogHeader),
            new(LocalizeTarget.Text, "lblSessionLabel",             () => AppStrings.SessionLabel, leftAlign: true),
            new(LocalizeTarget.Text, "lblLocationLabel",            () => AppStrings.LocationLabel, leftAlign: true),
            new(LocalizeTarget.Text, "lblTimer",                    () => AppStrings.Disconnected, forceLtr: true, keepFont: true, disconnectedOnly: true),
            new(LocalizeTarget.Text, "lblCountryName",              () => AppStrings.Disconnected, forceLtr: true, keepFont: true, disconnectedOnly: true),
            new(LocalizeTarget.Text, "lblLocalPortLabel",           () => AppStrings.OpenLocalPort, leftAlign: true),
            new(LocalizeTarget.Text, "lblLanPortLabel",             () => AppStrings.OpenLanPort, leftAlign: true),
            new(LocalizeTarget.Text, "lblPingLabel",                () => AppStrings.PingLabel, leftAlign: true),
            new(LocalizeTarget.Text, "lblTotalLabel",               () => AppStrings.TotalLabel, leftAlign: true),
            new(LocalizeTarget.Text, "lblDownloadLabel",            () => AppStrings.DownloadLabel, leftAlign: true),
            new(LocalizeTarget.Text, "lblUploadLabel",              () => AppStrings.UploadLabel, leftAlign: true),

            new(LocalizeTarget.Text, "lblSectionStartup",           () => AppStrings.SectionStartup, forceLtr: true),
            new(LocalizeTarget.Text, "lblLaunchOnStartup",          () => AppStrings.LaunchOnStartup),
            new(LocalizeTarget.Text, "lblAutoConnect",              () => AppStrings.AutoConnect),
            new(LocalizeTarget.Text, "lblStartMinimized",           () => AppStrings.StartMinimized),
            new(LocalizeTarget.Text, "lblMinimizeToTray",           () => AppStrings.MinimizeToTray),

            new(LocalizeTarget.ToolTip, "lblLaunchOnStartup",       () => AppStrings.TtLaunchOnStartup),
            new(LocalizeTarget.ToolTip, "lblAutoConnect",           () => AppStrings.TtAutoConnect),
            new(LocalizeTarget.ToolTip, "lblStartMinimized",        () => AppStrings.TtStartMinimized),
            new(LocalizeTarget.ToolTip, "lblMinimizeToTray",        () => AppStrings.TtMinimizeToTray),
            new(LocalizeTarget.ToolTip, "btnRefreshPing",           () => AppStrings.TtPingRefresh),
            new(LocalizeTarget.ToolTip, "btnLocation",              () => AppStrings.TtLocationRefresh),

            new(LocalizeTarget.Text, "lblSectionConnection",        () => AppStrings.Connect, forceLtr: true),

            new(LocalizeTarget.Text, "lblLbPolicy",                 () => AppStrings.LbPolicy),
            new(LocalizeTarget.ToolTip, "lblLbPolicy",              () => AppStrings.TtLbPolicy),
            new(LocalizeTarget.ToolTip, "btnLbPolicy",              () => AppStrings.TtLbPolicy),

            new(LocalizeTarget.Text, "lblCustomXrayExit",           () => AppStrings.CustomXrayExit),
            new(LocalizeTarget.ToolTip, "lblCustomXrayExit",        () => AppStrings.TtCustomXray),

            new(LocalizeTarget.Text, "lblOutboundProxySetting",     () => AppStrings.OutboundProxy),
            new(LocalizeTarget.ToolTip, "lblOutboundProxySetting",  () => AppStrings.TtOutboundProxy),

            new(LocalizeTarget.Text, "lblAdapterBindingTitle",      () => AppStrings.AdapterBinding),
            new(LocalizeTarget.ToolTip, "lblAdapterBindingTitle",   () => AppStrings.TtAdapterBinding),
            new(LocalizeTarget.Button, "btnScanAdapters",           () => AppStrings.ScanAdapters),

            new(LocalizeTarget.Text, "lblDnsSettingTitle",          () => AppStrings.DnsSettings),
            new(LocalizeTarget.ToolTip, "lblDnsSettingTitle",       () => AppStrings.TtDnsSettings),

            new(LocalizeTarget.Text, "lblAdBlockerSetting",         () => AppStrings.AdBlocker),
            new(LocalizeTarget.ToolTip, "lblAdBlockerSetting",      () => AppStrings.TtAdBlocker),

            new(LocalizeTarget.Text, "lblAllowLanSetting",          () => AppStrings.AllowLan),
            new(LocalizeTarget.ToolTip, "lblAllowLanSetting",       () => AppStrings.TtAllowLan),
            new(LocalizeTarget.Text, "lblLanAuthTitle",             () => AppStrings.LanAuth),
            new(LocalizeTarget.ToolTip, "lblLanAuthTitle",          () => AppStrings.TtLanAuth),

            new(LocalizeTarget.Text, "lblOutboundType",             () => AppStrings.ProxyType),
            new(LocalizeTarget.Text, "lblOutboundAddress",          () => AppStrings.AddressIp),
            new(LocalizeTarget.Text, "lblOutboundPort",             () => AppStrings.Port),
            new(LocalizeTarget.Text, "lblOutboundAuth",             () => AppStrings.Authentication),
            new(LocalizeTarget.Text, "lblOutboundUsername",         () => AppStrings.Username),
            new(LocalizeTarget.Text, "lblOutboundPassword",         () => AppStrings.Password),
            new(LocalizeTarget.Text, "lblUpstreamDoh",              () => AppStrings.UpstreamDohUrl),
            new(LocalizeTarget.Text, "lblSysDnsTitle",              () => AppStrings.SystemDns),
            new(LocalizeTarget.ToolTip, "lblSysDnsTitle",           () => AppStrings.TtSystemDns),

            new(LocalizeTarget.Text, "lblSectionSystem",            () => AppStrings.SectionSystem, forceLtr: true),

            new(LocalizeTarget.Text, "lblLanguageSetting",          () => AppStrings.LanguageSetting),
            new(LocalizeTarget.ToolTip, "lblLanguageSetting",       () => AppStrings.TtLanguage),

            new(LocalizeTarget.Text, "lblDebugMode",                () => AppStrings.DebugMode),
            new(LocalizeTarget.ToolTip, "lblDebugMode",             () => AppStrings.TtDebugMode),

            new(LocalizeTarget.Text, "lblDesktopShortcut",          () => AppStrings.DesktopShortcut),
            new(LocalizeTarget.Text, "lblStartMenuShortcut",        () => AppStrings.StartMenuShortcut),

            new(LocalizeTarget.Button, "btnDesktopShortcut",        () => AppStrings.Create),
            new(LocalizeTarget.Button, "btnStartMenuShortcut",      () => AppStrings.Create),

            new(LocalizeTarget.Text, "lblAboutVersion",             () => AppStrings.AboutVersion),
            new(LocalizeTarget.Text, "lblAboutCreator",             () => AppStrings.AboutCreator),
            new(LocalizeTarget.Text, "lblAboutLicense",             () => AppStrings.AboutLicense),
            new(LocalizeTarget.Text, "lblOtherApps",                () => AppStrings.AboutOtherApps),
            new(LocalizeTarget.Text, "lblDonations",                () => AppStrings.DonationsTitle),
            new(LocalizeTarget.Text, "lblDonationsDesc",            () => AppStrings.DonationsDesc),
            new(LocalizeTarget.Button, "btnCheckUpdate",            () => AppStrings.CheckForUpdates),

            new(LocalizeTarget.Text, "lblExpertTitle",              () => AppStrings.ExpertTitle),

            new(LocalizeTarget.Button, "btnExpertSave",             () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnExpertCancel",           () => AppStrings.BtnCancel),
            new(LocalizeTarget.Button, "btnXraySave",               () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnXrayCancel",             () => AppStrings.BtnCancel),
            new(LocalizeTarget.Button, "btnOutboundSave",           () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnOutboundCancel",         () => AppStrings.BtnCancel),
            new(LocalizeTarget.Button, "btnDohSave",                () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnSysDnsSave",             () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnLanAuthSave",            () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnSaveDomains",            () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnCancelDomains",          () => AppStrings.BtnCancel),
            new(LocalizeTarget.Button, "btnSaveApps",               () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnCancelApps",             () => AppStrings.BtnCancel),
            new(LocalizeTarget.Button, "btnSaveBlock",              () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnCancelBlock",            () => AppStrings.BtnCancel),

            new(LocalizeTarget.Button, "btnCaptchaSubmit",          () => AppStrings.Submit),
            new(LocalizeTarget.Button, "btnCaptchaCancel",          () => AppStrings.BtnCancel),
            new(LocalizeTarget.Button, "btnCustomSave",             () => AppStrings.Save),
            new(LocalizeTarget.Button, "btnCustomCancel",           () => AppStrings.BtnCancel),

            new(LocalizeTarget.Text, "lblCountriesOptimized",       () => AppStrings.RoutingOptimized),
            new(LocalizeTarget.Text, "lblCountriesExpert",          () => AppStrings.RoutingExpert),
        };
    }
}
