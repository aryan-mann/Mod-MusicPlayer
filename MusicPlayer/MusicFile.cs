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
using CSCore.Codecs;
using CSCore.Codecs.MP3;
using CSCore.SoundOut;

namespace MusicPlayer {

    public class MusicFile {

        private static DirectSoundOut soundOut;
        private static CodecFactory codecFactory;
        static MusicFile() {
            soundOut = new DirectSoundOut();
            codecFactory = CodecFactory.Instance;

            soundOut.Stopped += (sender, args) => {
                FileFinishedPlaying?.Invoke(LastFilePlayed);
            };
        }

        public string Filepath { get; private set; }
        public string Extension => Path.GetExtension(Filepath);

        private TagLib.File MetadataFile { get; set; }
        public int Duration => (int) MetadataFile.Properties.Duration.TotalSeconds;

        public string Title => MetadataFile.Tag.Title ?? Path.GetFileNameWithoutExtension(Filepath);
        public string Artist => MetadataFile.Tag.Performers?.FirstOrDefault() ?? "";
        public string Album => MetadataFile.Tag.Album ?? "";
        public string[] Genres => MetadataFile.Tag.Genres ?? new string[] { };

        /// <summary>
        ///  File extensions (codecs) that we support
        /// </summary>
        public static string[] ValidExtensions { get; } = {
            ".mp3", ".m4a", ".ogg", ".wav", ".flv", ".wmv", ".ink", ".Ink", ".flac"
        };

        public delegate void OnFilePlayed(MusicFile playedFile);
        public static event OnFilePlayed FilePlayed;
        public static event OnFilePlayed FileFinishedPlaying;

        public static List<MusicFile> LoadedFiles { get; set; } = new List<MusicFile>();

        private MusicFile() {}

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

            try {
                mf.MetadataFile = await Task.Run(() => TagLib.File.Create(mf.Filepath));
                return mf;
            } catch {
                // Fails if file from shortcut does not exist
                return null;
            }
        }

        public static MusicFile LastFilePlayed;

        public async Task PlayAsync() {

            if(string.IsNullOrWhiteSpace(Filepath)) { return; }
            
            await Task.Run(() => {
                var codec = codecFactory.GetCodec(Filepath);

                soundOut.Stop();
                soundOut.Initialize(codec);
                soundOut.Play();
            });

            LastFilePlayed = this;
            FilePlayed?.Invoke(this);
        }

        public static async Task ResetAnd(Action act = null) {
            await Task.Run(() => {
                soundOut.Stop();

                act?.Invoke();
            });
        }

        public async Task StopAsync() {
            await Task.Run(() => {
                if (soundOut.PlaybackState == PlaybackState.Playing ||
                    soundOut.PlaybackState == PlaybackState.Paused) {
                    soundOut.Stop();
                }
            });
        }

        public async Task PauseAsync() {
            await Task.Run(() => {
                if (soundOut.PlaybackState == PlaybackState.Playing) {
                    soundOut.Pause();
                }
            });
        }

        public async Task ResumeAsync() {
            await Task.Run(() => {
                if (soundOut.PlaybackState == PlaybackState.Paused && soundOut.PlaybackState != PlaybackState.Stopped) {
                    soundOut.Resume();
                }
            });
        }

        public async Task ToggleAsync() {
            switch (soundOut.PlaybackState) {
                case PlaybackState.Paused:
                    await ResumeAsync();
                    break;
                case PlaybackState.Playing:
                    await PauseAsync();
                    break;
            }
        }
        
        /// <summary>
        /// Checks to see if path is a shortcut file (*.ink)
        /// </summary>
        /// <param name="path">The path to the file</param>
        /// <returns>True if it's a shortcut</returns>
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

        /// <summary>
        /// Gets the actual filepath of a shortcut file (*.ink)
        /// </summary>
        /// <param name="path">The filepath of the shortcut file</param>
        /// <returns>Actual filepath</returns>
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

        /// <summary>
        /// Gets a list of music files that match the SearchQuery
        /// </summary>
        /// <param name="sq">The SearchQuery</param>
        /// <returns></returns>
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
