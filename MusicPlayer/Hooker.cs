using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ModuleAPI;
using MusicPlayer;
using Timer = System.Timers.Timer;

namespace MusicPlayer {

    [ApplicationHook]
    public class Hooker: Module {

        #region Useless Stuff
        public override string Name { get; } = "Music Player";
        public override string SemVer { get; } = "0.2.0";
        public override string Author => "Aryan Mann";
        public override Uri Website { get; } = new Uri("http://www.aryanmann.com/");
        public override string Prefix => "music";

        public override Task ConfigureSettings() => Task.CompletedTask;
        public override Task OnShutdown() => Task.CompletedTask;
        #endregion

        public static string InitialDirectory => ModuleExtensions.GetBaseDirectoryOfType(typeof(Hooker));

        //Instance of the SongList window
        private SongList _instance = null;

        private readonly Random _r = new Random();
        public MusicFile RandomSong => SongList.LoadedSongs.Count > 0 ? SongList.LoadedSongs[_r.Next(0, SongList.LoadedSongs.Count)] : null;
        public MusicFile LastPlayedSong { get; set; }

        //Setup commands for Project Butler
        public override Dictionary<string, Regex> RegisteredCommands => new Dictionary<string, Regex>() {
            ["List"] = new Regex(@"^list$", RegexOptions.IgnoreCase),
            ["Refresh"] = new Regex(@"^refresh$", RegexOptions.IgnoreCase),
            ["Play Song"] = new Regex(@"^play (?<name>.+)$", RegexOptions.IgnoreCase),
            ["List Artist"] = new Regex(@"^artist (?<artist>.+)$", RegexOptions.IgnoreCase),
            ["List Album"] = new Regex(@"^album (?<album>.+)$", RegexOptions.IgnoreCase),
            ["Random"] = new Regex(@"^random$", RegexOptions.IgnoreCase),
            ["Radio"] = new Regex(@"^(?<command>stop|start) radio", RegexOptions.IgnoreCase),
            ["Skip Radio Song"] = new Regex(@"^skip$", RegexOptions.IgnoreCase),
            ["Replay"] = new Regex(@"^replay$"),
            ["Sleep"] = new Regex(@"^sleep (?<seconds>\d+)$", RegexOptions.IgnoreCase),
            ["Stop Sleep"] = new Regex(@"^stop sleep(ing)?$", RegexOptions.IgnoreCase),
            ["Get Sleep"] = new Regex(@"^$", RegexOptions.IgnoreCase)
        };
        
        public override async Task OnInitialized() {

            Thread staThread = new Thread(() => {
                _instance = new SongList();
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            await _instance.LoadAllSongs();

            MusicFile.FilePlayed += file => {
                LastPlayedSong = file;
            };

        }

        //Project Butler command hook
        public override async Task OnCommandRecieved(Command cmd) {

            if(!_instance.Ready) {
                cmd.Respond("Music player not ready.");
                return;
            }

            if(cmd.LocalCommand == "List") {

                if(cmd.IsLocalCommand) {

                } else {
                    string output = $"";
                    SongList.LoadedSongs.ForEach(mf => {
                        output += (string.IsNullOrWhiteSpace(mf.Artist) ? "" : $"{mf.Artist} - ") + $"{mf.Title}\n";
                    });
                    cmd.Respond(output);
                }

                return;
            }

            if(cmd.LocalCommand == "Random") {
                MusicFile mf = RandomSong;
                await mf.PlayAsync();
                return;
            }

            if(cmd.LocalCommand == "Refresh") {
                await _instance.RefreshSongs();
                cmd.Respond("Refreshed!");

                return;
            }

            if(cmd.LocalCommand == "Replay") {
                if(LastPlayedSong == null) { return; }

                if(_runRadio) {
                    _radioTimer.Stop();
                    _radioTimer.Interval = LastPlayedSong.Duration * 1000f;
                    await LastPlayedSong.PlayAsync();
                    _radioTimer.Start();
                    return;
                }

                await LastPlayedSong.PlayAsync();
                return;
            }

            if(cmd.LocalCommand == "Play Song") {
                string song = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["name"].Value.ToLower();
                MusicFile mf = SongList.LoadedSongs.FirstOrDefault(f => Regex.Match(f.Title.ToLower(), song).Success);

                if(mf == null) { cmd.Respond("Song not found."); return; }

                await mf.PlayAsync();
                return;
            }

            if(cmd.LocalCommand == "List Artist") {
                string artist = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["artist"].Value.ToLower();

                List<MusicFile> files = await Task.Run(() => SongList.LoadedSongs.Where(s => (Regex.Match(s.Artist.ToLower(), artist).Success
                                                                              && !string.IsNullOrWhiteSpace(s.Artist))).ToList());

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

                List<MusicFile> files = await Task.Run(() => SongList.LoadedSongs.Where(s => (Regex.Match(s.Album.ToLower(), album).Success && !string.IsNullOrWhiteSpace(s.Album))).ToList());

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
                        await StartRadio();
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
                await file.PlayAsync();
                _radioTimer.Start();

                return;
            }

            #region Sleep

            if(cmd.LocalCommand == "Sleep") {
                string seconds = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["seconds"].Value;
                int secs;
                if(string.IsNullOrWhiteSpace(seconds) || !int.TryParse(seconds, out secs)) { return; }

                _sleepTimer.Interval = secs * 1000;
                _sleepTimer.Elapsed += SleepTimerOnElapsed;
                _sleepTimer.Start();
                cmd.Respond($"Sleeping after {secs} seconds");

                return;
            }

            if(cmd.LocalCommand == "Stop Sleep") {
                _sleepTimer?.Stop();
                cmd.Respond($"Stopped scheduled sleep");
                return;
            }

            if(cmd.LocalCommand == "Get Sleep") {
                string response = (_sleepTimer.Enabled == false) ? @"Sleep timer is inactive." : $"Remaining time: {_sleepTimer.RemainingTime}";
                cmd.Respond(response);
                return;
            }

            #endregion
        }

        private void SleepTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs) {
            _radioTimer.Stop();
        }

        private async void RadioSongCompletedAsync(object sender, System.Timers.ElapsedEventArgs e) {
            if(_runRadio == false) { return; }

            MusicFile mf = RandomSong;
            _radioTimer.Interval = mf.Duration * 1000d;
            _radioTimer.Start();

            await mf.PlayAsync();
        }

        private TimerPlus _radioTimer = new TimerPlus();
        private TimerPlus _sleepTimer = new TimerPlus();
        private bool _runRadio = false;
        private async Task StartRadio() {
            _runRadio = true;

            MusicFile file = RandomSong;
            double duration = file.Duration * 1000d;
            _radioTimer.Interval = duration;

            _radioTimer.Elapsed += RadioSongCompletedAsync;
            _radioTimer.Start();
            await file.PlayAsync();
        }

        private void StopRadio() {
            _radioTimer.Stop();
        }

        //Creates a popup that shows all songs ["list all"]
        public void DisplaySongList(List<MusicFile> filesToShow = null) {
            _instance.Show();
        }

    }

}
