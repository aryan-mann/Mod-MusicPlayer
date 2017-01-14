using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ModuleAPI;

namespace MusicPlayer {

    [ApplicationHook]
    public class Hooker : ModuleAPI.Module {

        #region Useless Stuff
        public override string Author {
            get { return "Aryan Mann"; }
        }
        public override string SemVer {
            get { return "0.1.0"; }
        }
        public override string Name {
            get { return "Music Player"; }
        }
        public override Uri Website {
            get { return new Uri("http://www.aryanmann.com/"); }
        }
        public override void ConfigureSettings() {
            return;
        }
        public override void OnInitialized() {
            return;
        }
        public override void OnShutdown() {
            return;
        }
        #endregion

        //Instance of the SongList window
        SongList SL_Instance;

        //Setup commands for Project Butler
        private Dictionary<string, Regex> _RegisteredCommands = new Dictionary<string, Regex>() {
            ["specific"] = new Regex("(play|play song|song) (?<song>.+)"),
            ["random"] = new Regex("play ?(anything|something|random|any|whatever|music)"),
            ["list all"] = new Regex("(all|list) ?songs?")
        };
        public override Dictionary<string, Regex> RegisteredCommands {
            get {
                return _RegisteredCommands;
            }
        }

        //These extensions will be recognized as valid music files
        public static string[] validExtensions = new string[] {
            ".mp3", ".m4a", ".ogg", ".wav", ".flv", ".wmv", ".ink", ".Ink", ".flac"
        };

        //Project Butler command hook
        public override void OnCommandRecieved(string CommandName, string UserInput) {
            if(CommandName == "random") {
                PlayRandom();
            } else if(CommandName == "specific") {
                string song = RegisteredCommands[CommandName].Match(UserInput).Groups["song"].Value.ToString();
                if(string.IsNullOrWhiteSpace(song)) { return; }

                PlayThis(song);
            } else if(CommandName == "list all") {
                DisplaySongList();
            }
        }

        //Creates a popup that shows all songs
        public void DisplaySongList() {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            List<string> files = Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => validExtensions.Contains(Path.GetExtension(path).ToLower()) || (IsShortcut(path) && validExtensions.Contains(Path.GetExtension(ResolveShortcut(path))))).ToList();
                        
            if(SL_Instance == null || !SL_Instance.IsLoaded) {
                SL_Instance = new SongList(files, BaseDirectory, "all");
            } else {
                SL_Instance.FillPaths(files, "all");
                SL_Instance.FillList();
            }
            SL_Instance.Show();
        }

        //Play the first song that matches the user input
        public void PlayThis(string songName) {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            List<string> files = Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => validExtensions.Contains(Path.GetExtension(path).ToLower()) || (IsShortcut(path) && validExtensions.Contains(Path.GetExtension(ResolveShortcut(path))))).ToList();

            List<string> matchingFiles = new List<string>();
            files.ForEach(fl => {
                if(Regex.Match(Path.GetFileNameWithoutExtension(fl), songName, RegexOptions.IgnoreCase).Success) {
                    matchingFiles.Add(fl);
                }
            });

            if(matchingFiles.Count == 0) { return; } 
            else if(matchingFiles.Count == 1) {
                Process pa = new Process() {
                    StartInfo = new ProcessStartInfo() {
                        FileName = matchingFiles[0],
                        WindowStyle = ProcessWindowStyle.Minimized
                    }
                };
                pa.Start();
            } else {
                if(SL_Instance == null || !SL_Instance.IsLoaded) {
                    SL_Instance = new SongList(matchingFiles, BaseDirectory, songName);
                } else {
                    SL_Instance.FillPaths(matchingFiles, songName);
                    SL_Instance.FillList();
                }
                SL_Instance.Show();
            }
        }

        Random r = new Random();
        //Play any random song from the base directory
        public void PlayRandom() {
            string songPath = Path.Combine(BaseDirectory, "Songs");
            if(!Directory.Exists(songPath)) { Directory.CreateDirectory(songPath); return; }

            List<string> files = Directory.GetFiles(songPath, "*", SearchOption.AllDirectories).Where(path => validExtensions.Contains(Path.GetExtension(path).ToLower()) || (IsShortcut(path) && validExtensions.Contains(Path.GetExtension(ResolveShortcut(path))))).ToList();

            if(files.Count == 0) { return; }

            PlayThis(Path.GetFileNameWithoutExtension(files[r.Next(0, files.Count)]));
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

            Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;

            return link.Path;
        }

    }

}
