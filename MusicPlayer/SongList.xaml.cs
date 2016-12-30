using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicPlayer {

    /// <summary>
    /// Interaction logic for SongList.xaml
    /// </summary>
    public partial class SongList : Window {

        List<string> Paths = new List<string>();
        string BaseDirectory { get; set; }

        public SongList(List<string> paths, string baseDirectory, string search) {
            InitializeComponent();

            Paths = paths;
            SearchInput.Text = search;
            BaseDirectory = baseDirectory;

            Loaded += SongList_Loaded;
            SearchInput.KeyDown += SearchInput_KeyDown;
            SearchList.KeyDown += SearchList_KeyDown;
            KeyDown += SongList_KeyDown;
        }

        private void SongList_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Escape) {
                Hide();
            }
        }
         
        private void SearchList_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                ListBoxItem lbi = (ListBoxItem) SearchList.SelectedItem;
                if(lbi == null) { return; }

                Process p = new Process() {
                    StartInfo = new ProcessStartInfo() {
                        FileName = lbi.DataContext.ToString() ?? "",
                        WindowStyle = ProcessWindowStyle.Minimized
                    }
                };
                p.Start();
                Hide();
            }
        }

        private void SearchInput_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                SearchList.Items.Clear();
                FillPaths(SearchInput.Text);
                FillList();
            }
        }

        private void SongList_Loaded(object sender, RoutedEventArgs e) {
            FillList();
        }

        public void FillPaths(List<string> _paths, string songName) {
            SearchInput.Text = songName;
            Paths = _paths;
        }
        
        public void FillPaths(string input) {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            SearchInput.Text = input;

            List<string> files = Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => Hooker.validExtensions.Contains(Path.GetExtension(path).ToLower()) || (Hooker.IsShortcut(path) && Hooker.validExtensions.Contains(Path.GetExtension(Hooker.ResolveShortcut(path))))).ToList();

            List<string> matchingFiles = new List<string>();
            foreach(string s in files) {
                if(Regex.Match(Path.GetFileNameWithoutExtension(s).ToLower(), input.ToLower()).Success) {
                    matchingFiles.Add(s);
                }
            }

            if(files.Count == 0) { SearchList.Items.Clear(); return; }
            Paths = matchingFiles;
        }

        public void FillList() {
            SearchList.Items.Clear(); 
            
            Paths.ForEach(pt => {
                var LBI = new ListBoxItem() {
                    Background = Brushes.White,
                    Foreground = Brushes.Black,
                    DataContext = pt,
                    IsTabStop = true,
                };

                string temp = Path.GetFileNameWithoutExtension(pt);
                LBI.Content = temp.Substring(0, temp.Length - 15);

                LBI.Selected += (sender, e) => {
                    LBI.Foreground = Brushes.Black;
                    LBI.FontSize = 64;
                    LBI.FontWeight = FontWeights.Bold;
                };
                LBI.Unselected += (sender, e) => {
                    LBI.Foreground = Brushes.Black;
                    LBI.FontSize = 18;
                    LBI.FontWeight = FontWeights.Normal;
                };
                LBI.MouseDoubleClick += (sender, e) => {
                    Process pa = new Process() {
                        StartInfo = new ProcessStartInfo() {
                            FileName = LBI.DataContext.ToString(),
                            WindowStyle = ProcessWindowStyle.Minimized
                        }
                    };
                    pa.Start();
                };
                SearchList.Items.Add(LBI);
            });
        }

    }
}
