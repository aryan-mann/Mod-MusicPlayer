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
using Timer = System.Timers.Timer;

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
        public List<MusicFile> Songs = new List<MusicFile>();
        private SongList _slInstance;
        private bool _initialized = false;

        private Random _r = new Random();
        public MusicFile RandomSong => Songs[_r.Next(0, Songs.Count)];

        public void RefreshSongs() {
            Songs = new List<MusicFile>();
            foreach(string file in Directory.GetFiles(Path.Combine(BaseDirectory, "Songs"), "*", SearchOption.AllDirectories)) {
                MusicFile mf = MusicFile.FromFile(file);
                if(mf != null) {
                    Songs.Add(mf);
                }
            }
        }

        //Setup commands for Project Butler
        public override Dictionary<string, Regex> RegisteredCommands => new Dictionary<string, Regex>() {
            ["List"] = new Regex(@"^list$", RegexOptions.IgnoreCase),
            ["Refresh"] = new Regex(@"^refresh$", RegexOptions.IgnoreCase),
            ["Play Song"] = new Regex(@"^play (?<name>.+)$", RegexOptions.IgnoreCase),
            ["List Artist"] = new Regex(@"^artist (?<artist>.+)$", RegexOptions.IgnoreCase),
            ["List Album"] = new Regex(@"^album (?<album>.+)$", RegexOptions.IgnoreCase),
            ["Random"] = new Regex(@"^random$", RegexOptions.IgnoreCase),
            ["Radio"] = new Regex(@"^(?<command>stop|start) radio", RegexOptions.IgnoreCase),
            ["Skip Radio Song"] = new Regex(@"^skip$", RegexOptions.IgnoreCase)
        };



        //Project Butler command hook
        public override void OnCommandRecieved(Command cmd) {

            if(!_initialized) {
                RefreshSongs();
                _initialized = true;
            }

            if(cmd.LocalCommand == "List") {

                if(cmd.IsLocalCommand) {
                    DisplaySongList();
                } else {
                    string output = $"";
                    Songs.ForEach(mf => {
                        output += (string.IsNullOrWhiteSpace(mf.Artist) ? "" : $"{mf.Artist} - ") + $"{mf.Title}\n";
                    });
                    cmd.Respond(output);
                }

                return;
            }

            if(cmd.LocalCommand == "Random") {
                MusicFile mf = RandomSong;
                mf.Play();
                return;
            }

            if(cmd.LocalCommand == "Refresh") {
                RefreshSongs();

                if(!cmd.IsLocalCommand) {
                    cmd.Respond("Refreshed!");
                }

                return;
            }

            if(cmd.LocalCommand == "Play Song") {
                string song = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["name"].Value.ToLower();
                MusicFile mf = Songs.FirstOrDefault(f => Regex.Match(f.Title.ToLower(), song).Success);

                if(mf == null) { return; }

                mf.Play();
                if(!cmd.IsLocalCommand) {
                    cmd.Respond($"Playing {mf.Title}");
                }

                return;
            }

            if(cmd.LocalCommand == "Random") {
                RandomSong.Play();
                if(!cmd.IsLocalCommand) {
                    cmd.Respond("Choosing a random song.");
                }

                return;
            }

            if(cmd.LocalCommand == "List Artist") {
                string artist = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["artist"].Value.ToLower();
                List<MusicFile> files = Songs.Where(s => (Regex.Match(s.Artist.ToLower(), artist).Success && !string.IsNullOrWhiteSpace(s.Artist))).ToList();

                if(cmd.IsLocalCommand) {
                    DisplaySongList(files);
                } else {
                    string output = $"";
                    files.ForEach(mf => {
                        output += $"{mf.Title}\n";
                    });
                    cmd.Respond(output);
                }

                return;
            }

            if(cmd.LocalCommand == "List Album") {
                string album = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["album"].Value.ToLower();
                List<MusicFile> files = Songs.Where(s => (Regex.Match(s.Album.ToLower(), album).Success && !string.IsNullOrWhiteSpace(s.Album))).ToList();

                if(cmd.IsLocalCommand) {
                    DisplaySongList(files);
                } else {
                    string output = $"";
                    files.ForEach(mf => {
                        output += $"{mf.Title}\n";
                    });
                    cmd.Respond(output);
                }
                return;
            }

            if(cmd.LocalCommand == "Radio") {
                switch(RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["command"].Value.ToLower()) {
                    case "start":
                        StartRadio();
                        break;
                    case "stop":
                        StopRadio();
                        break;
                }
                return;
            }

            if(cmd.LocalCommand == "Skip Radio Song") {

                if(_runRadio == false) { return; }

                _radioTimer.Stop();
                MusicFile file = RandomSong;
                _radioTimer.Interval = file.Duration * 1000f;
                file.Play();
                _radioTimer.Start();

                return;
            }
        }

        private void RadioSongCompleted(object sender, System.Timers.ElapsedEventArgs e) {
            if(_runRadio == false) { return; }

            MusicFile mf = RandomSong;
            _radioTimer.Interval = mf.Duration * 1000d;
            _radioTimer.Start();

            mf.Play();
        }

        private Timer _radioTimer = new Timer();
        private bool _runRadio = false;
        private void StartRadio() {
            _runRadio = true;

            MusicFile file = RandomSong;
            double duration = file.Duration * 1000d;

            _radioTimer.Interval = duration;

            _radioTimer.Elapsed += RadioSongCompleted;
            _radioTimer.Start();
            file.Play();
        }

        private void StopRadio() {
            _radioTimer.Stop();
        }

        private void Play(MusicFile f) {
            Process.Start(f.Filepath);
        }

        //Creates a popup that shows all songs ["list all"]
        public void DisplaySongList(List<MusicFile> filesToShow = null) {

            if(_slInstance == null || !_slInstance.IsLoaded) {
                _slInstance = new SongList(filesToShow ?? Songs, BaseDirectory, "all");
            } else {
                _slInstance.FillPaths(filesToShow ?? Songs, "all");
                _slInstance.FillList();
            }
            _slInstance.Show();
        }

    }

}
