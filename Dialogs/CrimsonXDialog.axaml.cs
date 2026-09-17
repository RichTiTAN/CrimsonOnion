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
    public partial class CrimsonXDialog : Window
    {
        public string DialogTitle { get; set; } = "";
        public string DialogHeader { get; set; } = "";
        public string DialogMessage { get; set; } = "";
        public string BtnGithub { get; set; } = "";
        public string BtnClose { get; set; } = "";

        public ICommand ButtonCommand { get; }

        public CrimsonXDialog()
        {
            DialogTitle = CrimsonOnion.Localization.AppStrings.PromoCrimsonXTitle;
            DialogHeader = CrimsonOnion.Localization.AppStrings.PromoCrimsonXHeader;
            DialogMessage = CrimsonOnion.Localization.AppStrings.PromoCrimsonXMsg;
            BtnGithub = "GitHub";
            BtnClose = CrimsonOnion.Localization.AppStrings.BtnClose;

            if (CrimsonOnion.Localization.AppStrings.IsPersian)
            {
                this.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
            }

            ButtonCommand = new RelayCommand(param => 
            {
                Close(param?.ToString());
            });
            DataContext = this;
            InitializeComponent();
            WindowChrome.RemoveMinimizeButton(this);
        }
    }
}

