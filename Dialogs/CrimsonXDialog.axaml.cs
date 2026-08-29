using Avalonia.Controls;
using System.Windows.Input;
using System;

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
        }
    }
}
