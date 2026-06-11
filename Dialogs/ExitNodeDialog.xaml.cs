using System.Windows;
using System.Windows.Controls;
using CrimsonOnion.Models;

namespace CrimsonOnion.Dialogs
{
    public partial class ExitNodeDialog : Window
    {
        private readonly AppConfig _config;

        private static readonly Dictionary<string, string> Countries = new()
        {
            { "Argentina", "ar" }, { "Australia", "au" }, { "Austria", "at" },
            { "Brazil", "br" }, { "Canada", "ca" }, { "Finland", "fi" },
            { "France", "fr" }, { "Germany", "de" }, { "Hong Kong", "hk" },
            { "Iceland", "is" }, { "India", "in" }, { "Iran", "ir" },
            { "Italy", "it" }, { "Japan", "jp" }, { "Mexico", "mx" },
            { "Netherlands", "nl" }, { "New Zealand", "nz" }, { "Romania", "ro" },
            { "Singapore", "sg" }, { "South Africa", "za" }, { "South Korea", "kr" },
            { "Spain", "es" }, { "Sweden", "se" }, { "Switzerland", "ch" },
            { "United Arab Emirates", "ae" }, { "United Kingdom", "gb" }, { "United States", "us" }
        };

        public ExitNodeDialog(AppConfig config)
        {
            _config = config;
            InitializeComponent();
            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            
            foreach (var kvp in Countries)
            {
                var item = new ComboBoxItem
                {
                    Content = $"{kvp.Key} ({kvp.Value.ToUpper()})",
                    Tag = kvp.Value
                };
                cmbCountries.Items.Add(item);
            }
            foreach (ComboBoxItem item in cmbCountries.Items)
            {
                if ((string)item.Tag == config.CustomExitCountry)
                {
                    cmbCountries.SelectedItem = item;
                    break;
                }
            }
            if (cmbCountries.SelectedItem == null && cmbCountries.Items.Count > 0)
                cmbCountries.SelectedIndex = 0;

            btnOk.Click += OnSave;
            btnCancel.Click += (s2, e2) => { DialogResult = false; Close(); };
            btnCloseDialog.Click += (s2, e2) => { DialogResult = false; Close(); };
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (cmbCountries.SelectedItem is ComboBoxItem item)
                _config.CustomExitCountry = ((string)item.Tag).ToLower();
            DialogResult = true;
            Close();
        }
    }
}
