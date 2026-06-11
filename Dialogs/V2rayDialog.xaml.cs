using System.IO;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class V2rayDialog : Window
    {
        private readonly AppConfig _config;
        private readonly Action<string> _showToast;

        public V2rayDialog(AppConfig config, Action<string> showToast)
        {
            _config = config;
            _showToast = showToast;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            
            txtInput.Text = config.V2rayChainJson;
            btnImport.Click += OnImport;
            btnOk.Click += OnSave;
            btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
            btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };

            Loaded += (s, e) =>
            {
                txtInput.Focus();
                txtInput.CaretIndex = txtInput.Text.Length;
            };
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                txtInput.Text = File.ReadAllText(dlg.FileName);
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var txt = txtInput.Text;
            if (string.IsNullOrWhiteSpace(txt))
            {
                _config.V2rayChainJson = "";
                DialogResult = true;
                Close();
                return;
            }
            try
            {
                var parsed = JObject.Parse(txt);
                JToken? testNode = parsed["outbounds"] is JArray arr ? arr.FirstOrDefault() : parsed;
                if (testNode?["protocol"] == null)
                    throw new Exception("Missing 'protocol' field.");
                _config.V2rayChainJson = txt.Trim();
                DialogResult = true;
                Close();
            }
            catch
            {
                _showToast("Invalid Xray JSON syntax!");
            }
        }
    }
}
