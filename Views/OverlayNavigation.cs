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
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace CrimsonOnion.Views
{
    internal sealed class PopupSpec
    {
        internal PopupSpec(
            string popup,
            string anchor,
            PlacementMode placement,
            double horizontalOffset,
            double verticalOffset,
            bool closesPanes,
            bool settingsLightDismiss)
        {
            Popup = popup;
            Anchor = anchor;
            Placement = placement;
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
            ClosesPanes = closesPanes;
            SettingsLightDismiss = settingsLightDismiss;
        }
        internal string Popup { get; }
        internal string Anchor { get; }

        internal PlacementMode Placement { get; }

        internal double HorizontalOffset { get; }

        internal double VerticalOffset { get; }
        internal bool ClosesPanes { get; }
        internal bool SettingsLightDismiss { get; }
    }
    internal sealed class OverlayNavigation
    {
        internal const string OpenClass = "popupOpen";
        internal const int CloseAnimationMs = 200;
        internal const int OpenStaggerMs = 10;
        internal const string LightDismissControl = "LightDismissOverlay";
        internal const string SettingsLightDismissControl = "SettingsLightDismiss";

        internal const string SplitTunnelPane = "panSplitOverlay";
        internal const string SettingsPane = "panSettingsOverlay";
        internal const string ExpertPane = "panExpertOverlay";
        internal const string ThemesPane = "panThemesOverlay";
        internal const string AboutPane = "panAboutOverlay";

        internal const string CountriesPopupControl = "CountriesPopup";
        internal const string LanguagePopupControl = "LanguagePopup";
        internal const string LbPolicyPopupControl = "LbPolicyPopup";
        internal static readonly string[] Panes =
        {
            SplitTunnelPane, SettingsPane, ExpertPane, ThemesPane, AboutPane
        };
        internal static readonly string[] PopupControls =
        {
            LanguagePopupControl, CountriesPopupControl, LbPolicyPopupControl
        };
        internal static readonly string[] SettingsPopupControls =
        {
            LanguagePopupControl, LbPolicyPopupControl
        };
        internal static readonly PopupSpec LanguagePopup = new(
            LanguagePopupControl, "btnLanguage", PlacementMode.Bottom, 0, 5,
            closesPanes: false, settingsLightDismiss: true);

        internal static readonly PopupSpec LbPolicyPopup = new(
            LbPolicyPopupControl, "btnLbPolicy", PlacementMode.Bottom, 0, 5,
            closesPanes: false, settingsLightDismiss: true);

        internal static readonly PopupSpec SidebarCountriesPopup = new(
            CountriesPopupControl, "SidebarBorder", PlacementMode.RightEdgeAlignedTop, 10, 0,
            closesPanes: true, settingsLightDismiss: false);

        internal static readonly PopupSpec MainCountriesPopup = new(
            CountriesPopupControl, "btnCurrentCountry", PlacementMode.Bottom, 0, 5,
            closesPanes: true, settingsLightDismiss: false);
        internal static readonly PopupSpec[] PopupSpecs =
        {
            LanguagePopup, LbPolicyPopup, SidebarCountriesPopup, MainCountriesPopup
        };

        private readonly Func<string, Control?> _find;
        private readonly Action _onSplitTunnelPaneClosing;

        private bool _wasLanguagePopupOpen;
        private bool _wasCountriesPopupOpen;
        private bool _wasLbPolicyPopupOpen;
        internal OverlayNavigation(Func<string, Control?> find, Action onSplitTunnelPaneClosing)
        {
            _find = find;
            _onSplitTunnelPaneClosing = onSplitTunnelPaneClosing;
        }
        internal bool AnyPopupOpen => AnyOpen(PopupControls);
        internal void CloseAll()
        {
            if (_find(SplitTunnelPane) is Border split)
            {
                if (split.IsVisible) _onSplitTunnelPaneClosing();
                ClosePane(split);
            }

            foreach (var pane in Panes)
            {
                if (pane != SplitTunnelPane) ClosePane(pane);
            }

            if (_find(LightDismissControl) is Border lightDismiss) lightDismiss.IsVisible = false;

            if (AnyPopupOpen)
            {
                _ = ClosePopupsAsync();
            }
        }
        internal void ClosePane(string paneName)
        {
            if (_find(paneName) is Border pane) ClosePane(pane);
        }
        internal void ShowPane(string paneName)
        {
            if (_find(paneName) is Border pane) ShowPane(pane);
        }
        internal void OpenPane(string paneName)
        {
            if (_find(paneName) is not Border pane || pane.IsVisible) return;

            CloseAll();
            ShowPane(pane);
        }
        internal async Task TogglePopupAsync(PopupSpec spec)
        {
            var popup = FindPopup(spec.Popup);

            if (popup != null && popup.IsOpen && popup.PlacementTarget?.Name == spec.Anchor)
            {
                _ = ClosePopupsAsync();
                return;
            }

            if (popup is { IsOpen: true })
            {
                popup.IsOpen = false;
                if (popup.Child is Border stale) stale.Classes.Remove(OpenClass);
            }

            _ = ClosePopupsAsync();

            if (spec.ClosesPanes) CloseAll();

            if (popup == null) return;

            popup.PlacementTarget  = _find(spec.Anchor);
            popup.Placement        = spec.Placement;
            popup.HorizontalOffset = spec.HorizontalOffset;
            popup.VerticalOffset   = spec.VerticalOffset;
            popup.IsOpen           = true;

            if (spec.SettingsLightDismiss && _find(SettingsLightDismissControl) is Border settingsLightDismiss)
            {
                settingsLightDismiss.IsVisible = true;
            }

            await Task.Delay(OpenStaggerMs);
            if (popup.Child is Border child) child.Classes.Add(OpenClass);

            if (spec.ClosesPanes && _find(LightDismissControl) is Border lightDismiss)
            {
                lightDismiss.IsVisible = true;
            }
        }
        internal async Task ClosePopupsAsync()
        {
            var popups = new Popup?[PopupControls.Length];
            bool anyOpen = false;

            for (int i = 0; i < PopupControls.Length; i++)
            {
                var popup = FindPopup(PopupControls[i]);
                popups[i] = popup != null && popup.IsOpen ? popup : null;
                anyOpen |= popups[i] != null;

                if (popups[i] is { Child: Border child }) child.Classes.Remove(OpenClass);
            }

            if (!anyOpen) return;

            await Task.Delay(CloseAnimationMs);

            foreach (var popup in popups)
            {
                if (popup != null) popup.IsOpen = false;
            }

            if (AnyPopupOpen) return;

            if (_find(SettingsLightDismissControl) is Border settingsLightDismiss) settingsLightDismiss.IsVisible = false;

            foreach (var pane in Panes)
            {
                if (_find(pane) is Border open && open.IsVisible) return;
            }

            if (_find(LightDismissControl) is Border lightDismiss) lightDismiss.IsVisible = false;
        }
        internal void CloseSettingsPopups()
        {
            if (AnyOpen(SettingsPopupControls))
            {
                _ = ClosePopupsAsync();
            }
        }
        internal void RememberPopups()
        {
            _wasLanguagePopupOpen = TakeDown(LanguagePopupControl);
            _wasCountriesPopupOpen = TakeDown(CountriesPopupControl);
            _wasLbPolicyPopupOpen = TakeDown(LbPolicyPopupControl);
        }
        internal void RestorePopups()
        {
            Reopen(LanguagePopupControl, _wasLanguagePopupOpen);
            Reopen(CountriesPopupControl, _wasCountriesPopupOpen);
            Reopen(LbPolicyPopupControl, _wasLbPolicyPopupOpen);
        }

        private bool AnyOpen(string[] popupNames)
        {
            foreach (var name in popupNames)
            {
                if (FindPopup(name) is { IsOpen: true }) return true;
            }

            return false;
        }

        private Popup? FindPopup(string name) => _find(name) as Popup;

        private void ClosePane(Border pane)
        {
            if (!pane.IsVisible) return;

            pane.Classes.Remove(OpenClass);
            DispatcherTimer.RunOnce(() => pane.IsVisible = false, TimeSpan.FromMilliseconds(CloseAnimationMs));
        }

        private void ShowPane(Border pane)
        {
            pane.IsVisible = true;
            pane.Classes.Add(OpenClass);

            if (_find(LightDismissControl) is Border lightDismiss) lightDismiss.IsVisible = true;
        }

        private bool TakeDown(string popupName)
        {
            if (FindPopup(popupName) is not Popup popup) return false;

            bool wasOpen = popup.IsOpen;
            if (wasOpen) popup.IsOpen = false;
            return wasOpen;
        }

        private void Reopen(string popupName, bool wasOpen)
        {
            if (wasOpen && FindPopup(popupName) is Popup popup) popup.IsOpen = true;
        }
    }
}
