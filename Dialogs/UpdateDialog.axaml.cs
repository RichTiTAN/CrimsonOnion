using Avalonia.Controls;
using System.Windows.Input;
using System;

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
