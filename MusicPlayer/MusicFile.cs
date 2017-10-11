using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MusicPlayer {

    public class MusicFile {

        public string Filepath { get; private set; }
        public string Extension => Path.GetExtension(Filepath);

        private TagLib.File MetadataFile { get; set; }
        public int Duration => (int) MetadataFile.Properties.Duration.TotalSeconds;

        public string Title => MetadataFile.Tag.Title ?? Path.GetFileNameWithoutExtension(Filepath);
        public string Artist => MetadataFile.Tag.Performers?.FirstOrDefault() ?? "";
        public string Album => MetadataFile.Tag.Album ?? "";
        public string[] Genres => MetadataFile.Tag.Genres ?? new string[] { };

        public delegate void OnFilePlayed(MusicFile playedFile);
        public static event OnFilePlayed FilePlayed;

        public static List<MusicFile> LoadedFiles { get; set; } = new List<MusicFile>();

        //These extensions will be recognized as valid music files
        public static string[] ValidExtensions { get; } = {
            ".mp3", ".m4a", ".ogg", ".wav", ".flv", ".wmv", ".ink", ".Ink", ".flac"
        };

        private MusicFile() {

        }

        public static MusicFile FromFile(string path) {
            if(IsShortcut(path)) {
                path = ResolveShortcut(path);
            }

            if(!File.Exists(path) || !ValidExtensions.Contains(Path.GetExtension(path)?.ToLower())) { return null; }
            var mf = new MusicFile() {
                Filepath = path
            };

            mf.MetadataFile = TagLib.File.Create(mf.Filepath);
            return mf;
        }
        public static async Task<MusicFile> FromFileAsync(string path) {
            if(!File.Exists(path)) { return null; }
            var isShortcut = IsShortcut(path);

            if(isShortcut) {
                path = ResolveShortcut(path);
            }

            var mf = new MusicFile() {
                Filepath = path
            };

            mf.MetadataFile = await Task.Run(() => TagLib.File.Create(mf.Filepath));
            return mf;
        }

        public async Task PlayAsync() {

            if(string.IsNullOrWhiteSpace(Filepath)) { return; }

            await Task.Run(() => {
                var p = new Process() {
                    StartInfo = new ProcessStartInfo() {
                        WindowStyle = ProcessWindowStyle.Minimized,
                        FileName = Filepath
                    }
                };
                p.Start();
            });

            FilePlayed?.Invoke(this);
        }

        //Checks to see if a file is a shortcut (.Ink extension)
        private static bool IsShortcut(string path) {
            var directory = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);

            var returnValue = false;

            var thr = new Thread(new ThreadStart(() => {
                var shell = new Shell32.Shell();
                var folder = shell.NameSpace(directory);
                var folderItem = folder.ParseName(file);

                returnValue = folderItem?.IsLink ?? false;
            }));
            thr.SetApartmentState(ApartmentState.STA);
            thr.Start();
            thr.Join();

            return returnValue;
        }
        //Get the real path of a shortcut file
        private static string ResolveShortcut(string path) {
            var directory = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);

            var linkPath = file;

            var thr = new Thread(new ThreadStart(() => {
                var shell = new Shell32.Shell();
                var folder = shell.NameSpace(directory);
                var folderItem = folder.ParseName(file);

                linkPath = ((Shell32.ShellLinkObject) folderItem.GetLink).Path;
            }));
            thr.SetApartmentState(ApartmentState.STA);
            thr.Start();
            thr.Join();

            return linkPath;
        }

        public static async Task<List<MusicFile>> ExecuteSearchQuery(SearchQuery sq) {
            return await Task.Run(() => {
                var results = new List<MusicFile>();

                if(sq.Equals(SearchQuery.Empty)) {
                    MusicFile.LoadedFiles.ForEach(m => results.Add(m));
                } else {
                    foreach(var f in MusicFile.LoadedFiles) {
                        if((string.IsNullOrWhiteSpace(sq.Title) || f.Title.StartsWith(sq.Title, StringComparison.CurrentCultureIgnoreCase)) &&
                            (string.IsNullOrWhiteSpace(sq.Artist) || f.Artist.StartsWith(sq.Artist, StringComparison.CurrentCultureIgnoreCase)) &&
                            (string.IsNullOrWhiteSpace(sq.Album) || f.Album.StartsWith(sq.Album, StringComparison.CurrentCultureIgnoreCase)) &&
                            (string.IsNullOrWhiteSpace(sq.Extension) || f.Extension.StartsWith(sq.Extension, StringComparison.CurrentCultureIgnoreCase))) {
                            results.Add(f);
                        }
                    }
                }
                return results;
            });
        }

        public override string ToString() => $"{Title}{(!string.IsNullOrWhiteSpace(Artist) ? $" | {Artist}" : "")}";
    }

}
