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

    public partial class SongList : Window {

        //Locations of all songs
        List<string> Paths = new List<string>();
        //Where to search for songs
        string BaseDirectory { get; set; }

        #region Programmatically Defined Colors
        private static SolidColorBrush DarkGrey { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#232425");
        private static SolidColorBrush LightGrey { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#4D4E4F");
        private static SolidColorBrush CreamWhite { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#F1F1F1");
        private static SolidColorBrush DarkLime { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#24AB93");
        private static SolidColorBrush LightLime { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#03DC8D");
#endregion

        public SongList(List<string> paths, string baseDirectory, string search) {
            InitializeComponent();

            Paths = paths;
            SearchInput.Text = search;
            BaseDirectory = baseDirectory;

            Loaded += SongList_Loaded;
            SearchInput.KeyDown += SearchInput_KeyDown;
            SearchList.KeyDown += SearchList_KeyDown;
            KeyDown += SongList_KeyDown;

            Deactivated += (sender, e) => {
                Close();
            };
        }

        //Set the position and size of the SongList window
        private void SongList_Loaded(object sender, RoutedEventArgs e) {
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
            this.Height = SystemParameters.PrimaryScreenHeight;
            this.Top = 0;

            FillList();
        }

        //Close the window when escape is pressed while window is active
        private void SongList_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Escape) {
                Hide();
            }
        }
        
        //Play the song selected in the SongList window
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

        //Filter songs from the base directory when enter is pressed while searching
        private void SearchInput_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                SearchList.Items.Clear();
                FillPaths(SearchInput.Text);
                FillList();
            }
        }

        //Set the Paths variable directly from a list of paths
        public void FillPaths(List<string> _paths, string songName) {
            SearchInput.Text = songName;
            Paths = _paths;
        }
        
        //Search for songs in the base directory and then fill the Paths variable
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

        //Foreach item in the Paths list, create an entry in the songs list and format the name
        public void FillList() {
            SearchList.Items.Clear(); 
            
            //Design of the list items
            Paths.ForEach(pt => {
                var LBI = new ListBoxItem() {
                    Background = DarkGrey,
                    Foreground = CreamWhite,
                    DataContext = pt,
                    IsTabStop = true,
                };

                string temp = Path.GetFileNameWithoutExtension(pt);
                LBI.Content = temp.Substring(0, temp.Length - 15);

                LBI.Selected += (sender, e) => {
                    LBI.FontSize = 36;
                    LBI.FontWeight = FontWeights.Bold;
                };
                LBI.Unselected += (sender, e) => {
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
                    this.Close();
                };
                SearchList.Items.Add(LBI);
            });
        }

    }
}
