using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace MusicPlayer {

    public class MusicFile {

        public string Filepath { get; private set; }
        public string Extension => Path.GetExtension(Filepath);

        private TagLib.File MetadataFile { get; set; }
        public int Duration => (int) MetadataFile.Properties.Duration.TotalSeconds;

        public string Title => MetadataFile.Tag.Title ?? Path.GetFileNameWithoutExtension(Filepath);
        public string Artist => MetadataFile.Tag.Performers?.FirstOrDefault() ?? "";
        public string Album => MetadataFile.Tag.Album ?? "";
        public string[] Genres => MetadataFile.Tag.Genres ?? new string[]{};

        //These extensions will be recognized as valid music files
        public static string[] ValidExtensions { get; } = {
            ".mp3", ".m4a", ".ogg", ".wav", ".flv", ".wmv", ".ink", ".Ink", ".flac"
        };

        public static MusicFile FromFile(string path) {

            if (IsShortcut(path)) {
                path = ResolveShortcut(path);
            }
            
            if(!File.Exists(path) || !ValidExtensions.Contains(Path.GetExtension(path)?.ToLower())) { return null; }
            MusicFile mf = new MusicFile();
            mf.Filepath = path;
            mf.MetadataFile = TagLib.File.Create(mf.Filepath);
            return mf;
        }

        private MusicFile() {
            
        }

        public void Play() {
            Process p = new Process() {
                StartInfo = new ProcessStartInfo() {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    FileName = Filepath
                }
            };
            p.Start();
        }

        //Checks to see if a file is a shortcut (.Ink extension)
        private static bool IsShortcut(string path) {
            string directory = Path.GetDirectoryName(path);
            string file = Path.GetFileName(path);

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(directory);
            Shell32.FolderItem folderItem = folder.ParseName(file);

            if(folderItem != null) { return folderItem.IsLink; }
            return false;
        }
        //Get the real path of a shortcut file
        private static string ResolveShortcut(string path) {
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
