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

using CrimsonOnion.Localization;
using CrimsonOnion.Models;

namespace CrimsonOnion.Services;
internal static class GeoDisplay
{
    private static readonly System.Collections.Generic.Dictionary<string, string> ContinentNames =
        new System.Collections.Generic.Dictionary<string, string>
        {
            ["NA"] = "NORTH AMERICA", ["EU"] = "EUROPE",  ["AS"] = "ASIA",
            ["SA"] = "SOUTH AMERICA", ["AF"] = "AFRICA",  ["OC"] = "OCEANIA", ["AN"] = "ANTARCTICA"
        };
    public static string ContinentName(string? continentCode)
    {
        var code = continentCode ?? string.Empty;
        return ContinentNames.TryGetValue(code, out var name) ? name : code;
    }
    public static string DescribeExit(GeoResult geo, AppConfig cfg)
    {
        var continentDisplay = ContinentName(geo.ContinentCode);
        var countryDisplay = geo.Country;

        if (AppStrings.IsPersian)
        {
            continentDisplay = GeoTranslation.GetContinentFa(geo.ContinentCode, continentDisplay);
            countryDisplay = GeoTranslation.GetCountryFa(geo.CountryCode, countryDisplay);
        }

        var label = UsesCountryLabel(cfg) ? countryDisplay : continentDisplay;
        return string.IsNullOrWhiteSpace(label) ? UnknownLabel : TitleCase(label);
    }
    public static string Describe(bool tracing, bool timedOut, GeoResult? last, AppConfig cfg)
    {
        if (tracing) return AppStrings.GeoTracing;
        if (last != null) return DescribeExit(last, cfg);

        return timedOut ? AppStrings.GeoTimeout : AppStrings.Disconnected;
    }
    private const string UnknownLabel = "Unknown";
    private static string TitleCase(string label)
        => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(label.ToLowerInvariant());
    private static bool UsesCountryLabel(AppConfig cfg)
    {
        if (cfg.EnableV2rayChain || cfg.LastConfig == "Custom") return true;

        if (cfg.LastConfig == "Expert"
            && !string.IsNullOrWhiteSpace(cfg.ExpertExitNodes)
            && !cfg.ExpertExitNodes.Contains(",")) return true;

        return cfg.LastConfig != "Optimized" && cfg.LastConfig != "Expert";
    }
}
