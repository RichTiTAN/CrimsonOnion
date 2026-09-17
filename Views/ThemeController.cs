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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using CrimsonOnion.Localization;
using CrimsonOnion.Models;
using CrimsonOnion.Services;

namespace CrimsonOnion.Views
{
    internal sealed class ThemePalette
    {
        internal ThemePalette(string name, string accent, string accentHover, string accentPressed, string glow)
        {
            Name          = name;
            Accent        = Color.Parse(accent);
            AccentHover   = Color.Parse(accentHover);
            AccentPressed = Color.Parse(accentPressed);
            Glow          = Color.Parse(glow);
        }
        internal string Name { get; }

        internal Color Accent { get; }
        internal Color AccentHover { get; }
        internal Color AccentPressed { get; }
        internal Color Glow { get; }
    }
    internal sealed class ThemeController
    {
        internal const string DefaultTheme = "Crimson";
        internal const string CurrentThemeBrush = "ThemeCurrentBrush";
        internal const string VpnModeButton = "btnVpnMode";
        internal static readonly ThemePalette[] Palettes =
        {
            new ThemePalette("Crimson", "#B82E42", "#D13A51", "#932535", "#FFE64A62"),
            new ThemePalette("Blue",    "#2B6CB0", "#3182CE", "#2C5282", "#63B3ED"),
            new ThemePalette("Purple",  "#6B46C1", "#805AD5", "#553C9A", "#B794F4"),
            new ThemePalette("Green",   "#2F855A", "#38A169", "#276749", "#68D391"),
            new ThemePalette("Pink",    "#B83280", "#D53F8C", "#97266D", "#F687B3"),
            new ThemePalette("Yellow",  "#B7791F", "#D69E2E", "#975A16", "#F6E05E"),
        };
        internal enum Tone
        {
            Accent,
            AccentHover,
            AccentPressed,
            GlowColor,
            GlowBrush,
        }
        internal static readonly (string Key, Tone Tone)[] AppResources =
        {
            ("ThemeAccent",                      Tone.Accent),
            ("ThemeAccentPointerOver",           Tone.AccentHover),
            ("ThemeAccentPressed",               Tone.AccentPressed),
            ("ThemeGlow",                        Tone.GlowColor),
            ("ThemeGlowBrush",                   Tone.GlowBrush),
            ("ToggleSwitchFillOn",               Tone.Accent),
            ("ToggleSwitchFillOnPointerOver",    Tone.AccentHover),
            ("ToggleSwitchFillOnPressed",        Tone.AccentPressed),
            ("SliderThumbBackground",            Tone.Accent),
            ("SliderThumbBackgroundPointerOver", Tone.AccentHover),
            ("SliderThumbBackgroundPressed",     Tone.AccentPressed),
            ("SliderTrackValueFill",             Tone.Accent),
            ("SliderTrackValueFillPointerOver",  Tone.AccentHover),
            ("SliderTrackValueFillPressed",      Tone.AccentPressed),
        };
        internal static readonly (string Mode, string Button)[] ModeButtons =
        {
            (XraySupervisor.ProxyMode,  "btnProxyMode"),
            (XraySupervisor.VpnMode,    VpnModeButton),
            (XraySupervisor.ClearProxy, "btnClearProxy"),
        };

        private const string ProxyModeButton    = "btnProxyMode";
        private const string VpnModePanel       = "panVpnMode";
        private const string ConnectLabel       = "txtConnectBtn";
        private const string ConnectRing        = "panConnectGlow";
        private const string OuterGlowPanel     = "panOuterGlow";
        private const string OuterGlowRectangle = "rectOuterGlow";
        private const string BackgroundGlow     = "bgGlowEllipse";
        private const string ActiveModeClass    = "activeMode";
        private const string ConnectingState    = "Connecting";
        private static readonly TimeSpan GlowInterval = TimeSpan.FromMilliseconds(33);
        private const double FullTurn     = 360;
        private const double RotationStep = 4.0;
        private const double ClockStep    = 0.033;
        private const double DriftSpanX   = 350;
        private const double DriftBaseY   = 150;
        private const double DriftSpanY   = 100;
        private const double DriftRateX   = 0.14;
        private const double DriftRateY   = 0.21;

        private readonly Func<string, Control?> _find;
        private readonly Func<string> _bridge;
        private readonly Func<bool> _windowActive;
        private readonly Action _save;
        private readonly Action _splitTunnelChanged;
        private readonly IResourceDictionary _windowResources;
        private readonly AppConfig _cfg;
        private readonly AppState _state;

        private DispatcherTimer? _timer;
        private double _angle;
        private double _time;

        internal ThemeController(
            Func<string, Control?> find,
            Func<string> bridge,
            Func<bool> windowActive,
            Action save,
            Action splitTunnelChanged,
            IResourceDictionary windowResources,
            AppConfig cfg,
            AppState state)
        {
            _find               = find;
            _bridge             = bridge;
            _windowActive       = windowActive;
            _save               = save;
            _splitTunnelChanged = splitTunnelChanged;
            _windowResources    = windowResources;
            _cfg                = cfg;
            _state              = state;
        }
        internal void ApplyTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName)) themeName = DefaultTheme;

            var palette       = PaletteFor(themeName);
            var accent        = new SolidColorBrush(palette.Accent);
            var accentHover   = new SolidColorBrush(palette.AccentHover);
            var accentPressed = new SolidColorBrush(palette.AccentPressed);
            var glowBrush     = new SolidColorBrush(palette.Glow);

            var app = Application.Current;
            if (app != null)
            {
                foreach (var (key, tone) in AppResources)
                {
                    app.Resources[key] = tone switch
                    {
                        Tone.Accent        => (object)accent,
                        Tone.AccentHover   => accentHover,
                        Tone.AccentPressed => accentPressed,
                        Tone.GlowColor     => palette.Glow,
                        _                  => glowBrush,
                    };
                }
            }

            if (_state.IsEngineRunning && _find(ConnectLabel) is TextBlock txtConnectBtn
                && txtConnectBtn.Text == AppStrings.ConnectedBtn)
            {
                txtConnectBtn.Foreground = new SolidColorBrush(palette.Glow);
            }

            string gradientKey = "Theme" + themeName + "Brush";
            if (_windowResources.ContainsKey(gradientKey))
            {
                _windowResources[CurrentThemeBrush] = _windowResources[gradientKey];
            }
        }
        internal void SelectTheme(Button? button)
        {
            if (button == null) return;

            string themeName = button.CommandParameter?.ToString() ?? DefaultTheme;

            _cfg.ThemeColor = themeName;
            _save();
            ApplyTheme(themeName);
        }

        private static ThemePalette PaletteFor(string themeName)
        {
            foreach (var palette in Palettes)
            {
                if (palette.Name == themeName) return palette;
            }

            return Palettes[0];
        }
        internal void ApplyGlowVisibility()
        {
            if (_find(OuterGlowPanel) is Panel pan) pan.IsVisible = !_cfg.DisableGlow;
            if (_find(BackgroundGlow) is Ellipse bg) bg.IsVisible = !_cfg.DisableGlow;
        }
        internal void StartGlow()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = GlowInterval };
                _timer.Tick += OnGlowTick;
                _timer.Start();
            }
        }

        internal void StopGlow()
        {
            _timer?.Stop();
            _timer = null;
        }
        private void OnGlowTick(object? sender, EventArgs e)
        {
            if (_cfg.PauseGlow || _cfg.DisableGlow || !_windowActive()) return;

            _angle += RotationStep;
            if (_angle >= FullTurn) _angle -= FullTurn;

            _time += ClockStep;

            if (_find(OuterGlowRectangle) is Rectangle rect && rect.RenderTransform is RotateTransform rotate)
            {
                rotate.Angle = _angle;
            }

            if (_find(BackgroundGlow) is Ellipse ellipse && ellipse.RenderTransform is TranslateTransform drift)
            {
                drift.X = Math.Sin(_time * DriftRateX) * DriftSpanX;
                drift.Y = DriftBaseY + Math.Cos(_time * DriftRateY) * DriftSpanY;
            }
        }
        internal void UpdateRingAnimation(string state)
        {
            if (_find(ConnectRing) is Border ring)
            {
                ring.Opacity = state == ConnectingState ? 1.0 : 0.0;
            }
        }
        internal void ApplyModeUI(string mode)
        {
            foreach (var (_, button) in ModeButtons) _find(button)?.Classes.Remove(ActiveModeClass);

            var panVpnMode = _find(VpnModePanel) as Panel;
            if (_find(VpnModeButton) is Button btnVpnMode)
            {
                if (VpnModeUnavailable())
                {
                    btnVpnMode.IsEnabled = false;
                    btnVpnMode.Opacity   = 0.3;
                    AppStrings.ApplyToolTip(panVpnMode, AppStrings.TtDisabledVpnSnowflake);
                }
                else
                {
                    btnVpnMode.IsEnabled = true;
                    btnVpnMode.Opacity   = 1.0;
                    if (panVpnMode != null) ToolTip.SetTip(panVpnMode, null);
                }
            }

            string activeButton = ProxyModeButton;
            foreach (var (name, button) in ModeButtons)
            {
                if (name == mode) activeButton = button;
            }

            _find(activeButton)?.Classes.Add(ActiveModeClass);

            _splitTunnelChanged();
        }
        internal bool VpnModeUnavailable() => _bridge() == BridgeNames.Snowflake && !_cfg.EnableDirectUDP;
        internal static string ModeOf(string? buttonName)
        {
            foreach (var (mode, button) in ModeButtons)
            {
                if (button == buttonName) return mode;
            }

            return XraySupervisor.ProxyMode;
        }
    }
}
