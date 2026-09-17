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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CrimsonOnion.Localization;
using CrimsonOnion.Models;
using CrimsonOnion.Services;

namespace CrimsonOnion.Views
{
    internal sealed class UpdateController
    {
        internal const string CheckUpdateButton = "btnCheckUpdate";
        internal const string TitleUpdateButton = "btnTitleUpdate";
        internal const string LatestVersion = "0.0.0";
        internal const string ReleasePageUrl = "https://github.com/RichTiTAN/CrimsonOnion/releases";
        internal const string PromoUrl = "https://github.com/RichTiTAN/CrimsonX";
        internal const string DialogPrimary = "Primary";
        internal const string DialogSecondary = "Secondary";
        internal const int LatestAnswerMs = 3000;
        internal const int CancelledAnswerMs = 2000;

        private readonly Func<string, Control?> _find;
        private readonly AppConfig _cfg;
        private readonly VpnRuntimeState _vpn;
        private readonly Action<string, bool> _toast;
        private readonly Action _quitForInstall;
        private readonly Action<string> _openUrl;
        private readonly Func<bool, string, Task<string?>> _askUpdate;
        private readonly Func<Task<string?>> _askPromo;
        private readonly Func<CancellationToken, Task<(string? remoteVer, string? remoteMin)>> _checkForUpdates;
        private readonly Func<string, string, Action<string>, CancellationToken, Task> _downloadAndInstall;
        internal UpdateController(
            Func<string, Control?> find,
            AppConfig cfg,
            VpnRuntimeState vpn,
            Action<string, bool> toast,
            Action quitForInstall,
            Action<string> openUrl,
            Func<bool, string, Task<string?>> askUpdate,
            Func<Task<string?>> askPromo,
            Func<CancellationToken, Task<(string? remoteVer, string? remoteMin)>>? checkForUpdates = null,
            Func<string, string, Action<string>, CancellationToken, Task>? downloadAndInstall = null)
        {
            _find               = find;
            _cfg                = cfg;
            _vpn                = vpn;
            _toast              = toast;
            _quitForInstall     = quitForInstall;
            _openUrl            = openUrl;
            _askUpdate          = askUpdate;
            _askPromo           = askPromo;
            _checkForUpdates    = checkForUpdates ?? (token => UpdateService.CheckForUpdatesAsync(token));
            _downloadAndInstall = downloadAndInstall ?? ((version, baseDir, progress, token) =>
                UpdateService.DownloadAndInstallUpdateAsync(version, baseDir, progress, token));
        }
        internal string RemoteVersion { get; private set; } = LatestVersion;
        internal string RemoteMinVersion { get; private set; } = LatestVersion;
        internal bool HasKnownUpdate => !string.IsNullOrEmpty(RemoteVersion) && RemoteVersion != LatestVersion;
        internal void SetStatus(string status)
        {
            if (_find(TitleUpdateButton) is Button btnTitleUpdate) btnTitleUpdate.Content = status;
            if (_find(CheckUpdateButton) is Button btnCheckUpdate) btnCheckUpdate.Content = status;
        }
        internal async Task CheckSilentlyAsync()
        {
            try
            {
                var (remoteVer, remoteMin) = await _checkForUpdates(CancellationToken.None);

                if (remoteVer != null)
                {
                    RemoteVersion    = remoteVer;
                    RemoteMinVersion = remoteMin ?? LatestVersion;

                    if (_find(TitleUpdateButton) is Button btnTitleUpdate) btnTitleUpdate.IsVisible = true;

                    SetStatus(AppStrings.UpdateAutoTitle);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log(ex);
            }
        }
        internal async Task StartDownloadAsync()
        {
            if (CancellationTokens.CancelAndDispose(ref _vpn.UpdateCts)) return;

            if (!HasKnownUpdate) return;

            _vpn.UpdateCts = new CancellationTokenSource();
            var token = _vpn.UpdateCts.Token;

            try
            {
                await _downloadAndInstall(RemoteVersion, _cfg.BaseDir, SetStatus, token);

                _quitForInstall();
            }
            catch (OperationCanceledException)
            {
                _toast(token.IsCancellationRequested ? AppStrings.UpdateCancelled
                                                     : AppStrings.ToastUpdateDownloadFailed, false);
                SetStatus(AppStrings.UpdateAutoTitle);
            }
            catch (Exception ex)
            {
                _toast(string.Format(AppStrings.ToastUpdateErrorFormat, ex.Message), false);
                SetStatus(AppStrings.UpdateAutoTitle);
            }
            finally
            {
                CancellationTokens.Forget(ref _vpn.UpdateCts);
            }
        }
        internal async Task TitleUpdateClicked()
        {
            if (CancellationTokens.CancelAndDispose(ref _vpn.UpdateCts)) return;

            await AskToUpdateAsync(IsManualUpdate(), RemoteVersion);
        }
        internal async Task CheckUpdateClicked()
        {
            if (_find(CheckUpdateButton) is not Button btnCheckUpdate) return;

            if (CancellationTokens.CancelAndDispose(ref _vpn.UpdateCts)) return;

            if (HasKnownUpdate)
            {
                await AskToUpdateAsync(IsManualUpdate(), RemoteVersion);
                return;
            }

            btnCheckUpdate.Content = AppStrings.UpdateChecking;
            _vpn.UpdateCts = new CancellationTokenSource();
            var token = _vpn.UpdateCts.Token;

            try
            {
                var (remoteVer, remoteMin) = await _checkForUpdates(token);

                if (remoteVer == null)
                {
                    _toast(AppStrings.ToastLatestVersion, true);
                    btnCheckUpdate.Content = AppStrings.UpdateLatest;
                    try { await Task.Delay(LatestAnswerMs, token); } catch { }
                    btnCheckUpdate.Content = AppStrings.CheckForUpdates;

                    CancellationTokens.Forget(ref _vpn.UpdateCts);
                    return;
                }

                if (IsTooOldToAutoUpdate(remoteMin))
                {
                    btnCheckUpdate.Content = AppStrings.UpdateManual;

                    var manualAnswer = await _askUpdate(true, remoteVer);
                    if (manualAnswer == DialogPrimary || manualAnswer == DialogSecondary) _openUrl(ReleasePageUrl);

                    CancellationTokens.Forget(ref _vpn.UpdateCts);
                    return;
                }

                RemoteVersion = remoteVer;
                if (_find(TitleUpdateButton) is Button btnTitleUpdate) btnTitleUpdate.IsVisible = true;

                SetStatus(AppStrings.UpdateAutoTitle);

                CancellationTokens.Forget(ref _vpn.UpdateCts);

                var answer = await _askUpdate(false, remoteVer);
                if (answer == DialogPrimary) _ = StartDownloadAsync();
                else if (answer == DialogSecondary) _openUrl(ReleasePageUrl);
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                {
                    _toast(AppStrings.UpdateCancelled, false);
                    btnCheckUpdate.Content = AppStrings.UpdateCancelled;
                    try { await Task.Delay(CancelledAnswerMs); } catch { }
                }
                else
                {
                    _toast(AppStrings.ToastUpdateCheckFailed, false);
                }

                btnCheckUpdate.Content = AppStrings.CheckForUpdates;
            }
            catch (Exception ex)
            {
                _toast(string.Format(AppStrings.ToastUpdateErrorFormat, ex.Message), false);
                btnCheckUpdate.Content = AppStrings.CheckForUpdates;
            }
            finally
            {
                CancellationTokens.Forget(ref _vpn.UpdateCts);
            }
        }
        internal async Task PromoClicked()
        {
            var answer = await _askPromo();

            if (answer == DialogPrimary) _openUrl(PromoUrl);
        }
        private bool IsManualUpdate() => IsTooOldToAutoUpdate(RemoteMinVersion);
        private static bool IsTooOldToAutoUpdate(string? remoteMin)
            => Version.Parse(UpdateService.AppVersion) < Version.Parse(remoteMin ?? LatestVersion);
        private async Task AskToUpdateAsync(bool isManual, string remoteVer)
        {
            var answer = await _askUpdate(isManual, remoteVer);

            if (answer == DialogPrimary)
            {
                if (isManual) _openUrl(ReleasePageUrl);
                else _ = StartDownloadAsync();
            }
            else if (answer == DialogSecondary)
            {
                _openUrl(ReleasePageUrl);
            }
        }
    }
}
