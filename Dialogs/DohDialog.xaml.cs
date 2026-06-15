using System.Windows;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class DohDialog : Window
    {
        private readonly AppConfig _config;
        public DohDialog(AppConfig config)
        {
            _config = config;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            
            txtUpDoh.Text = config.UpstreamDohUrl;
            btnOk.Click += OnSave;
            btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
            btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };

            Loaded += (s, e) =>
            {
                txtUpDoh.Focus();
                // CaretIndex is not directly available on ComboBox
            };
        }
        private void OnSave(object sender, RoutedEventArgs e)
        {
            var v = txtUpDoh.Text.Trim();
            if (!string.IsNullOrWhiteSpace(v)) _config.UpstreamDohUrl = v;
            DialogResult = true;
            Close();
        }
    }
}
