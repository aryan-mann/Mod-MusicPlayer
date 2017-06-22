using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicPlayer {

    public partial class SongList : Window {

        public SongList() {
            InitializeComponent();

            Loaded += (sender, e) => CenterScreen();
            Deactivated += (sender, e) => Hide();
            Activated += (sender, args) => Show();
        }
        
        public bool Ready { get; private set; }
        private static string SongDirectory => Path.Combine(Hooker.InitialDirectory, "Songs");

        public event EventHandler SongsLoaded;

        /// <summary>
        /// List of all loaded songs
        /// </summary>
        public static List<MusicFile> LoadedSongs { get; set; } = new List<MusicFile>();

        /// <summary>
        /// List of all requested songs
        /// </summary>
        public BindingList<MusicFile> SongSource { get; private set; } = new BindingList<MusicFile>();
        
        public async Task LoadAllSongs() {
            if (!Directory.Exists(SongDirectory)) {
                Directory.CreateDirectory(SongDirectory);
                //TODO: Check for permissions
            }

            LoadedSongs = LoadedSongs ?? new List<MusicFile>();
            LoadedSongs.Clear();

            IEnumerable<string> files = null;
            files = Directory.EnumerateFiles(SongDirectory, "*", SearchOption.AllDirectories);

            List<Task<MusicFile>> fileTasks = files.Select(MusicFile.FromFileAsync).ToList();
            MusicFile[] musicFiles = await Task.WhenAll(fileTasks);

            LoadedSongs = musicFiles.ToList();
            Ready = true;

            SongsLoaded?.Invoke(this, EventArgs.Empty);
        }
        public async Task RefreshSongs() {
            await LoadAllSongs();
        }

        public void ExecuteSearchQuery(SearchQuery sq) {
            SongSource = new BindingList<MusicFile>(LoadedSongs.Where(f => 
                    (string.IsNullOrWhiteSpace(sq.Title) || f.Title.StartsWith(sq.Title, StringComparison.CurrentCultureIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(sq.Artist) || f.Artist.StartsWith(sq.Artist, StringComparison.CurrentCultureIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(sq.Album) || f.Album.StartsWith(sq.Album, StringComparison.CurrentCultureIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(sq.Extension) || f.Extension.StartsWith(sq.Extension, StringComparison.CurrentCultureIgnoreCase))
            ).ToList());
            
        }
        private void CenterScreen() {
            Left = SystemParameters.PrimaryScreenWidth - Width;
            Height = SystemParameters.PrimaryScreenHeight;
            Top = 0;
        }
        
    }

    public class SearchQuery {
        public string Title;
        public string Artist;
        public string Album;
        public string Extension;
    }
}
