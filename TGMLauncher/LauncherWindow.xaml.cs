using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TGMLauncher
{
    public partial class MainWindow : Window
    {
        Dictionary<string, string> configs = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();

            configs.Add("Default Config", "");
            lstConfig.Items.Add("Default Config");

            var d = new DirectoryInfo("./");
            foreach (var f in d.GetFiles("*.json"))
            {
                configs.Add(cleanName(f.Name),f.Name);
                lstConfig.Items.Add(cleanName(f.Name));
            }
        }

        private string cleanName(string n)
        {
            return titleCase(n.Replace("-", " ").Replace(".json", "").Replace("config", ""));
        }

        private void lstConfig_SelectionChanged(object sender, RoutedEventArgs e)
        {
            //e.Handled = true;
            string i = lstConfig.SelectedItem.ToString();
            var cfg = configs[i];
            Process.Start("TouchGamingMouse.exe", (cfg=="") ? "" : "--config=" + cfg);
            Application.Current.Shutdown();
        }
        private string titleCase(string title)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title.ToLower());
        }
    }
}
