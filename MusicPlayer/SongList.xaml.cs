using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicPlayer {

    public partial class SongList : Window {

        //Locations of all songs
        List<MusicFile> _paths;
        //ItemSource
        List<ListBoxItem> SongSource = new List<ListBoxItem>();
        //Where to search for songs
        string BaseDirectory { get; set; }

        //        #region Programmatically Defined Colors
//        private static SolidColorBrush DarkGrey { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#232425");
//        private static SolidColorBrush LightGrey { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#4D4E4F");
//        private static SolidColorBrush CreamWhite { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#F1F1F1");
//        private static SolidColorBrush DarkLime { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#24AB93");
//        private static SolidColorBrush LightLime { get; } = (SolidColorBrush) new BrushConverter().ConvertFrom("#03DC8D");
//#endregion

        public SongList(List<MusicFile> paths, string baseDirectory, string search) {
            InitializeComponent();

            _paths = paths;
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
            Left = SystemParameters.PrimaryScreenWidth - Width;
            Height = SystemParameters.PrimaryScreenHeight;
            Top = 0;

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
                        FileName = ((MusicFile)lbi.DataContext).Filepath,
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
                SongSource.Clear();
                SearchList.ItemsSource = null;
                SearchList.ItemsSource = SongSource;

                FillPaths(SearchInput.Text);
                FillList();
            }
        }

        //Set the Paths variable directly from a list of paths
        public void FillPaths(List<MusicFile> _paths, string songName) {
            SearchInput.Text = songName;
            this._paths = _paths;
        }
        
        //Search for songs in the base directory and then fill the Paths variable
        public void FillPaths(string input) {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            SearchInput.Text = input;

            List<MusicFile> files = new List<MusicFile>();
            List<MusicFile> matchingFiles = new List<MusicFile>();
            
            foreach(MusicFile mf in files) {
                if(Regex.Match(Path.GetFileNameWithoutExtension(mf.Filepath).ToLower(), input.ToLower()).Success) {
                    matchingFiles.Add(mf);
                }
            }

            if(files.Count == 0) {
                SongSource.Clear();
                SearchList.ItemsSource = null;
                SearchList.ItemsSource = SongSource;
                return; }

            _paths = matchingFiles;
        }

        //Foreach item in the Paths list, create an entry in the songs list and format the name
        public void FillList() {
            SongSource.Clear();
            
            //Design of the list items
            _paths.ForEach(pt => {

                var LBI = new ListBoxItem {
                    IsTabStop = true,
                    Content = Path.GetFileNameWithoutExtension(pt.Filepath),
                    DataContext = pt,
                };



                LBI.MouseDoubleClick += (sender, e) => {
                    Process pa = new Process() {
                        StartInfo = new ProcessStartInfo() {
                            FileName = pt.Filepath,
                            WindowStyle = ProcessWindowStyle.Minimized
                        }
                    };
                    pa.Start();
                    Close();
                };

                SongSource.Add(LBI);
            });

            SearchList.ItemsSource = null;
            SearchList.ItemsSource = SongSource;
        }

    }
}
