using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ModuleAPI;

namespace MusicPlayer {

    [ApplicationHook]
    public class Hooker: Module {

        #region Useless Stuff
        public override string Name { get; } = "Music Player";
        public override string SemVer { get; } = "0.1.0";
        public override string Author => "Aryan Mann";
        public override Uri Website { get; } = new Uri("http://www.aryanmann.com/");
        public override string Prefix => "music";

        public override void ConfigureSettings() { }
        public override void OnInitialized() {
            return;
        }
        public override void OnShutdown() {
            return;
        }
        #endregion

        //Instance of the SongList window
        SongList _slInstance;
        private bool _isRadioRunning = false;
        private List<string> _songs = null;
        private string _lastPlayedSong = null;
        private string _randomSong => _songs[_r.Next(0, _songs.Count)];


        //Setup commands for Project Butler
        public override Dictionary<string, Regex> RegisteredCommands => new Dictionary<string, Regex>() {
            ["specific"] = new Regex("^(play|play song|song) (?<song>.+)$"),
            ["random"] = new Regex("^(anything|something|random|any|whatever|music)$"),
            ["list all"] = new Regex("^list( all)?$"),
            ["start radio"] = new Regex("radio on"),
            ["end radio"] = new Regex("radio off")
        };

        //These extensions will be recognized as valid music files
        public static string[] ValidExtensions = {
            ".mp3", ".m4a", ".ogg", ".wav", ".flv", ".wmv", ".ink", ".Ink", ".flac"
        };

        //Project Butler command hook
        public override void OnCommandRecieved(Command cmd) {

            if (_songs == null) {
                _songs = GetSongs();
            }

            if(cmd.LocalCommand == "random") {
                PlayRandom();
                return;
            }

            if(cmd.LocalCommand == "specific") {
                string song = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["song"].Value.ToString();
                if(string.IsNullOrWhiteSpace(song)) { return; }

                PlayThis(song, cmd.IsLocalCommand);
                return;
            }

            if(cmd.LocalCommand == "list all") {

                if(cmd.IsLocalCommand) {
                    DisplaySongList();
                } else {
                    string text = "";
                    var songs = GetSongs();

                    for(int i = 0; i < songs.Count; i++) {
                        text += $"{i}. {Path.GetFileNameWithoutExtension(Regex.Match(songs[i], @"(.+)( - Shortcut\.lnk)").Success ? songs[i].Substring(0, songs[i].Length - 15) : songs[i])}\n";
                    }
                    cmd.Respond(text);
                }

                return;
            }

            if(cmd.LocalCommand == "start radio") {
                StartRadio();
                return;
            }

            if(cmd.LocalCommand == "end radio") {
                StopRadio();
                return;
            }

        }

        #region Radio

        #region Radio

        private void StartRadio() {
            _isRadioRunning = true;
            RadioLoop();
        }

        private void RadioLoop() {

            if(!_isRadioRunning) { return; }

            string chosenSongPath = _randomSong;
            string resolvedPath = ResolveShortcut(chosenSongPath);
            TagLib.File f = TagLib.File.Create(resolvedPath);

            double timer = f.Properties.Duration.TotalSeconds;
            Process pa = new Process() {
                StartInfo =  new ProcessStartInfo() {
                    FileName = resolvedPath,
                    WindowStyle =  ProcessWindowStyle.Minimized
                }
            };
            pa.Start();
            _lastPlayedSong = resolvedPath;

            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(() => {  }).ContinueWith(task => {
                Task.Delay((int) timer * 1000).Wait();
                RadioLoop();
            }, scheduler);
        }

        private void StopRadio() {
            _isRadioRunning = false;
        }

        #endregion

        #endregion


        private List<string> GetSongs() {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return new List<string>(); }
            return Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => ValidExtensions.Contains(Path.GetExtension(path).ToLower()) || (IsShortcut(path) && ValidExtensions.Contains(Path.GetExtension(ResolveShortcut(path))))).ToList();
        }

        //Creates a popup that shows all songs ["list all"]
        public void DisplaySongList() {
            List<string> files = GetSongs();

            if(_slInstance == null || !_slInstance.IsLoaded) {
                _slInstance = new SongList(files, BaseDirectory, "all");
            } else {
                _slInstance.FillPaths(files, "all");
                _slInstance.FillList();
            }
            _slInstance.Show();
        }

        //Play the first song that matches the user input ["specific"]
        public void PlayThis(string songName, bool showList = true) {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            List<string> files = Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => ValidExtensions.Contains(Path.GetExtension(path).ToLower()) || (IsShortcut(path) && ValidExtensions.Contains(Path.GetExtension(ResolveShortcut(path))))).ToList();

            List<string> matchingFiles = new List<string>();
            files.ForEach(fl => {
                if(Regex.Match(Path.GetFileNameWithoutExtension(fl), songName, RegexOptions.IgnoreCase).Success) {
                    matchingFiles.Add(fl);
                }
            });

            if(matchingFiles.Count == 0) { return; } else if(matchingFiles.Count == 1) {
                Process pa = new Process() {
                    StartInfo = new ProcessStartInfo() {
                        FileName = matchingFiles[0],
                        WindowStyle = ProcessWindowStyle.Minimized
                    }
                };
                pa.Start();
            } else {
                if(showList) {
                    if(_slInstance == null || !_slInstance.IsLoaded) {
                        _slInstance = new SongList(matchingFiles, BaseDirectory, songName);
                    } else {
                        _slInstance.FillPaths(matchingFiles, songName);
                        _slInstance.FillList();
                    }
                    _slInstance.Show();
                } else {
                    Process pa = new Process() {
                        StartInfo = new ProcessStartInfo() {
                            FileName = matchingFiles[0],
                            WindowStyle = ProcessWindowStyle.Minimized
                        }
                    };
                    pa.Start();
                }
                _lastPlayedSong = matchingFiles[0];
            }
        }

        private Random _r = new Random();
        //Play any random song from the base directory ["random"]
        public void PlayRandom() {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            List<string> files = Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => ValidExtensions.Contains(Path.GetExtension(path).ToLower()) || (IsShortcut(path) && ValidExtensions.Contains(Path.GetExtension(ResolveShortcut(path))))).ToList();

            if(files.Count == 0) { return; }

            PlayThis(Path.GetFileNameWithoutExtension(files[_r.Next(0, files.Count)]), false);
        }

        //Checks to see if a file is a shortcut (.Ink extension)
        public static bool IsShortcut(string path) {
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            if(folderItem != null) { return folderItem.IsLink; }
            return false;
        }
        //Get the real path of a shortcut file
        public static string ResolveShortcut(string path) {
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            Shell32.ShellLinkObject link = (Shell32.ShellLinkObject) folderItem.GetLink;

            return link.Path;
        }

    }

}
