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
using System.Windows.Input;

namespace CrimsonOnion.Dialogs
{
    public partial class UpdateDialog : Window
    {
        public string DialogTitle { get; set; } = "";
        public string DialogMessage { get; set; } = "";
        public string PrimaryButtonText { get; set; } = "";
        public string SecondaryButtonText { get; set; } = "";
        public string CancelButtonText { get; set; } = "";

        public ICommand ButtonCommand { get; }

        public UpdateDialog()
        {
            ButtonCommand = new RelayCommand(param => 
            {
                Close(param?.ToString());
            });
            DataContext = this;
            InitializeComponent();
            WindowChrome.RemoveMinimizeButton(this);
        }

        public UpdateDialog(bool isManual, string remoteVer)
        {
            ButtonCommand = new RelayCommand(param => 
            {
                Close(param?.ToString());
            });
            DataContext = this;

            if (isManual)
            {
                DialogTitle = CrimsonOnion.Localization.AppStrings.UpdateManualTitle;
                DialogMessage = string.Format(CrimsonOnion.Localization.AppStrings.UpdateManualMsg, remoteVer);
                PrimaryButtonText = CrimsonOnion.Localization.AppStrings.BtnDownloadGithub;
            }
            else
            {
                DialogTitle = CrimsonOnion.Localization.AppStrings.UpdateAutoTitle;
                DialogMessage = string.Format(CrimsonOnion.Localization.AppStrings.UpdateAutoMsg, remoteVer);
                PrimaryButtonText = CrimsonOnion.Localization.AppStrings.BtnUpdateNow;
            }

            SecondaryButtonText = CrimsonOnion.Localization.AppStrings.BtnChangeLog;
            CancelButtonText = CrimsonOnion.Localization.AppStrings.BtnCancel;

            if (CrimsonOnion.Localization.AppStrings.IsPersian)
            {
                this.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
            }

            InitializeComponent();
            WindowChrome.RemoveMinimizeButton(this);
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}

