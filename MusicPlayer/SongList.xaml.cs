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

            KeyDown += (sender, args) => {
                if (args.Key == Key.Escape) {
                    Hide();
                }
            };

            SearchList.MouseDoubleClick += async (sender, args) => { await PlaySelectedSong(); };
            SearchList.KeyDown += async (sender, args) => {
                if (args.Key == Key.Enter) {
                    await PlaySelectedSong();
                }
                if (args.Key == Key.Tab) {
                    SearchInput.Focus();
                    SearchInput.SelectAll();
                    Keyboard.PrimaryDevice.Focus(SearchInput);
                }
            };

            SearchInput.KeyDown += (sender, args) => {
                if (args.Key == Key.Tab) {
                    SearchList.Focus();
                    Keyboard.PrimaryDevice.Focus(SearchList);
                }

                if (args.Key == Key.Enter)
                    UseSearchQuery(SearchQuery.Generate(SearchInput.Text));
            };
        }

        public SearchQuery LastSearchQuery { get; private set; }

        public void SetSearchQueryString(SearchQuery sq) {
            SearchInput.Text = sq.ToString();
        }

        private async Task PlaySelectedSong() {
            var mf = SearchList.SelectedItem as MusicFile;

            if (mf != null) {
                await mf.PlayAsync();
            }
            else {
                await Task.CompletedTask;
            }

            Hide();
        }

        public bool Ready { get; private set; }
        private static string SongDirectory => Path.Combine(Hooker.InitialDirectory, "Songs");

        /// <summary>
        /// List of all requested songs
        /// </summary>
        public BindingList<MusicFile> SongSource { get; private set; } = new BindingList<MusicFile>();

        public async Task LoadAllSongs() {
            if (!Directory.Exists(SongDirectory)) {
                Directory.CreateDirectory(SongDirectory);
                //TODO: Check for permissions
            }

            MusicFile.LoadedFiles = MusicFile.LoadedFiles ?? new List<MusicFile>();
            MusicFile.LoadedFiles.Clear();

            IEnumerable<string> files = null;
            files = Directory.EnumerateFiles(SongDirectory, "*", SearchOption.AllDirectories);

            var fileTasks = files.Select(MusicFile.FromFileAsync).ToList();
            var musicFiles = await Task.WhenAll(fileTasks);

            MusicFile.LoadedFiles = musicFiles.ToList();
            Ready = true;
        }

        public async Task RefreshSongs() {
            await LoadAllSongs();
        }

        public async void UseSearchQuery(SearchQuery sq) {
            LastSearchQuery = sq;
            SearchInput.Text = sq.ToString();
            SongSource.Clear();

            foreach (var file in await MusicFile.ExecuteSearchQuery(sq)) {
                SongSource.Add(file);
            }
        }

        public void UseSongList(SearchQuery sq, List<MusicFile> files) {
            LastSearchQuery = sq;
            SongSource?.Clear();

            files.ForEach(f => SongSource.Add(f));
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

        public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Artist) &&
                               string.IsNullOrWhiteSpace(Album) && string.IsNullOrWhiteSpace(Extension);

        public SearchQuery() { }

        public static SearchQuery Generate(string text) {
            var sq = new SearchQuery();

            var argsMatch = Regex.Matches(text, "((?<letter>r|a|e){(?<content>.+?)})+");
            foreach (Match match in argsMatch) {
                var letter = match.Groups["letter"].Value;
                var content = match.Groups["content"].Value;

                switch (letter) {
                    case "r":
                        sq.Artist = content;
                        break;
                    case "a":
                        sq.Album = content;
                        break;
                    case "e":
                        sq.Extension = content;
                        break;
                }
            }

            if (argsMatch.Count > 0) {
                var textMatch = Regex.Match(text, "^(?<!{)(?<title>(\\w+|\\s+)+)(?= (r|a|e){)");
                sq.Title = textMatch.Groups["title"].Value.Trim();
            }
            else {
                sq.Title = text.Trim();
            }

            return sq;
        }

        public override string ToString() {
            return $"{(!string.IsNullOrEmpty(Title) ? $"{Title} " : "")}{(!string.IsNullOrEmpty(Artist) ? $"r{{{Artist}}} " : "")}{(!string.IsNullOrEmpty(Album) ? $"a{{{Album}}} " : "")}{(!string.IsNullOrEmpty(Extension) ? $"e{{{Extension}}}" : "")}";
        }

        public static SearchQuery Empty => new SearchQuery();
    }
}