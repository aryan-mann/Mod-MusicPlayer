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
using System.Windows.Threading;
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
        public MusicFile RandomSong => MusicFile.LoadedFiles.Count > 0 ? MusicFile.LoadedFiles[_r.Next(0, MusicFile.LoadedFiles.Count)] : null;
        public MusicFile LastPlayedSong { get; set; }

        public Dispatcher UiThread = Application.Current.Dispatcher;

        //Setup commands for Project Butler
        public override Dictionary<string, Regex> RegisteredCommands => new Dictionary<string, Regex>() {
            ["List"] = new Regex(@"^list$", RegexOptions.IgnoreCase),
            ["Choose"] = new Regex(@"^choose (?<choice>\d+)$", RegexOptions.IgnoreCase),

            ["Refresh"] = new Regex(@"^refresh$", RegexOptions.IgnoreCase),

            ["Search"] = new Regex(@"^\? (?<search>.+)$", RegexOptions.IgnoreCase),
            ["Play Song"] = new Regex(@"^play (?<name>.+)$", RegexOptions.IgnoreCase),
            ["List Artist"] = new Regex(@"^artist (?<artist>.+)$", RegexOptions.IgnoreCase),
            ["List Album"] = new Regex(@"^album (?<album>.+)$", RegexOptions.IgnoreCase),
            ["Random"] = new Regex(@"^random$", RegexOptions.IgnoreCase),

            ["Radio"] = new Regex(@"^(?<command>stop|start) radio", RegexOptions.IgnoreCase),
            ["Skip Radio Song"] = new Regex(@"^skip$", RegexOptions.IgnoreCase),
            ["Replay"] = new Regex(@"^replay$"),

            ["Sleep"] = new Regex(@"^sleep (?<seconds>\d+)$", RegexOptions.IgnoreCase),
            ["Stop Sleep"] = new Regex(@"^stop sleep(ing)?$", RegexOptions.IgnoreCase),
            ["Get Sleep"] = new Regex(@"^sleep time$", RegexOptions.IgnoreCase)
        };

        public override async Task OnInitialized() {

            UiThread.Invoke(() => {
                _instance = new SongList();
            });

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

                    UiThread.Invoke(() => {
                        _instance.UseSearchQuery(SearchQuery.Empty);
                        _instance.Show();
                    });

                } else {
                    string output = $"Songs:- \n\n\n";
                    for(var i = 0; i < MusicFile.LoadedFiles.Count; i++) {
                        output += $"[{i.ToString().PadLeft(3)}] {MusicFile.LoadedFiles[i]}\n";
                    }
                    cmd.Respond(output);
                }

                return;
            }

            if(cmd.LocalCommand == "Choose") {
                int numInt = int.Parse(RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["choice"].Value);

                if(numInt > MusicFile.LoadedFiles.Count - 1 || numInt < 0) {
                    cmd.Respond($"Song #{numInt} does not exist.");
                    return;
                }

                await MusicFile.LoadedFiles[numInt].PlayAsync();
            }

            if(cmd.LocalCommand == "Random") {
                await RandomSong.PlayAsync();
                return;
            }

            if(cmd.LocalCommand == "Refresh") {
                cmd.Respond("Refreshing..");
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

                cmd.Respond($"Replaying {LastPlayedSong}");
                return;
            }

            if(cmd.LocalCommand == "Search") {
                string query = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["search"].Value;
                SearchQuery sq = SearchQuery.Generate(query);

                if(cmd.IsLocalCommand) {
                    UiThread.Invoke(() => {
                        _instance.UseSearchQuery(sq);
                        _instance.Show();
                    });
                    return;
                } else {
                    List<MusicFile> results = await MusicFile.ExecuteSearchQuery(sq);
                    if(results.Count == 0) {
                        cmd.Respond($"No results found for {query}");
                    } else {
                        MusicFile mf = results[0];
                        cmd.Respond($"Playing {mf}");
                        await mf.PlayAsync();
                    }
                    return;
                }

            }

            if(cmd.LocalCommand == "Play Song") {
                string song = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["name"].Value.ToLower();

                MusicFile mf = MusicFile.LoadedFiles.FirstOrDefault(f => Regex.Match(f.Title.ToLower(), song).Success);
                if(mf == null) { cmd.Respond("Song not found."); return; }

                await mf.PlayAsync();
                return;
            }

            if(cmd.LocalCommand == "List Artist") {
                string artist = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["artist"].Value.ToLower();

                if(cmd.IsLocalCommand) {
                    UiThread.Invoke(() => {
                        _instance.UseSearchQuery(new SearchQuery() {
                            Artist = artist
                        });
                        _instance.Show();
                    });
                } else {
                    string output = $"Songs by {artist}:-\n";
                    List<MusicFile> files = await MusicFile.ExecuteSearchQuery(new SearchQuery() {
                        Artist = artist
                    });

                    for(int i = 0; i < files.Count; i++) {
                        output += $"[{i.ToString().PadLeft(3)}] {files[i]}\n";
                    }
                    cmd.Respond(output);
                }

                return;
            }

            if(cmd.LocalCommand == "List Album") {
                string album = RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["album"].Value.ToLower();

                if(cmd.IsLocalCommand) {
                    UiThread.Invoke(() => {
                        _instance.UseSearchQuery(new SearchQuery() {
                            Album = album
                        });
                        _instance.Show();
                    });
                } else {
                    string output = $"Songs on {album}:-\n";
                    List<MusicFile> files = await MusicFile.ExecuteSearchQuery(new SearchQuery() {
                        Artist = album
                    });

                    for(int i = 0; i < files.Count; i++) {
                        output += $"[{i.ToString().PadLeft(3)}] {files[i]}\n";
                    }
                    cmd.Respond(output);
                }

                return;
            }

            if(cmd.LocalCommand == "Radio") {
                switch(RegisteredCommands[cmd.LocalCommand].Match(cmd.UserInput).Groups["command"].Value.ToLower()) {
                    case "start":
                        await StartRadio();
                        cmd.Respond("Started radio");
                        break;
                    case "stop":
                        StopRadio();
                        cmd.Respond("Stopped radio");
                        break;
                }
                return;
            }

            if(cmd.LocalCommand == "Skip Radio Song") {

                if(_runRadio == false) { return; }

                _radioTimer.Stop();
                MusicFile file = RandomSong;
                _radioTimer.Interval = file.Duration * 1000f;
                await file?.PlayAsync();
                _radioTimer.Start();

                cmd.Respond("Skipping currently playing song");

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

            await mf?.PlayAsync();
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
            await file?.PlayAsync();
        }

        private void StopRadio() {
            _radioTimer.Stop();
        }

    }

}
