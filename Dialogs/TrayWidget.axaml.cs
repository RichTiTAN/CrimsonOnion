using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia;
using CrimsonOnion.Localization;

namespace CrimsonOnion.Dialogs
{
    public partial class TrayWidget : Window
    {
        private MainWindow _main = null!;
        private DispatcherTimer _timer = null!;
        private bool _isClosing = false;

        public TrayWidget()
        {
            InitializeComponent();
        }

        public TrayWidget(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateUI();
            _timer.Start();
            UpdateUI();
        }

        private void UpdateUI()
        {
            bool fa = AppStrings.IsPersian;
            if (_main.GetState().IsConnected)
            {
                lblStatus.Text = fa ? "متصل"           : "CONNECTED";
                lblStatus.Foreground = new SolidColorBrush(Color.Parse("#68D391"));
                btnToggle.Content    = fa ? "قطع اتصال" : "DISCONNECT";
            }
            else if (_main.GetState().IsEngineRunning)
            {
                lblStatus.Text = fa ? "در حال اتصال..." : "CONNECTING...";
                lblStatus.Foreground = new SolidColorBrush(Color.Parse("#E2E8F0"));
                btnToggle.Content    = fa ? "توقف موتور"  : "STOP ENGINE";
            }
            else
            {
                lblStatus.Text = fa ? "متصل نیست"  : "NOT CONNECTED";
                lblStatus.Foreground = new SolidColorBrush(Color.Parse("#E2E8F0"));
                btnToggle.Content    = fa ? "اتصال"   : "CONNECT";
            }
            lblSpeed.Text = _main.GetSpeedText();
        }

        public void ApplyLanguage(bool isPersian)
        {
            UpdateUI();

            bool fa = isPersian;
            btnClose.Content      = fa ? "بستن برنامه"  : "CLOSE THE APP";
            btnShowWindow.Content = fa ? "نمایش پنجره" : "SHOW WINDOW";

            lblStatus.FlowDirection = fa
                ? global::Avalonia.Media.FlowDirection.RightToLeft
                : global::Avalonia.Media.FlowDirection.LeftToRight;
        }

        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            _main.ConnectDisconnect();
            UpdateUI();
        }

        private void BtnShowWindow_Click(object sender, RoutedEventArgs e)
        {
            _main.Show();
            _main.WindowState = WindowState.Normal;
            _main.Activate();
            SafeClose();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            _main.Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            SafeClose();
        }

        private void SafeClose()
        {
            if (_isClosing) return;
            _isClosing = true;
            _timer.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            base.OnClosed(e);
        }
    }
}
