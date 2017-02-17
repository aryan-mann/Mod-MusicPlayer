# Music Player
Are you tired of using Windows Explorer to search for and play music? 
Would you like to be able to search for and play music within 4 seconds?
The MusicPlayer module for Project Butler is here to the rescue.

![alt text](http://aryanmann.com/wp-content/uploads/2017/01/AllSongs.png "All Songs")

## Installation
Download the latest build from GitHub and unzip the file into the Project Butler Modules folder. Yup, it's that easy.

[***Latest Stable Release***](https://github.com/aryan-mann/Mod-MusicPlayer/releases/tag/0.1.0)

## Adding Songs
Create a folder called '***Songs***' under the Music Player module folder 
or just run any command supported by MusicPlayer once and MusicPlayer will automatically create the folder for you. 

You can either directly add songs to the folder or just add shortcuts to the songs and MusicPlayer will resolve those
shortcuts at runtime. 

***Valid Extensions:*** ```MP3 M4A OGG WAV FLV FLAC INK (shortcut)```
## Commands

The trigger/prefix is ``music``

### - Random
Don't know what to listen to? Play something at random.

Raw Regex | ``(anything|something|whatever|random|any)``

- ```anything```
- ```something```
- ```whatever```
- ```random```
- ```any```

### - Specific
Know what to listen to? Play what you want.

Raw Regex | ``(play|song| play song) (.+)`` 

- ```play *song name*```
- ```song *song name*```
- ```play song *song name*```

### - View All Songs
View all recognized songs and play one from them.

Raw Regex | ``list( all)``

- ```list```
- ```list all```

### - Quickset Volume
Quickly set the volume of the current playback device to preset values.

Raw Regex | ``vol(ume)? (?<action>up|down|mute|zero|full|min|max)``

- ```vol max```
- ```volume min```
- ```vol up```
- ```volume mute```

### - Finetune Volume
Set the exact value of the volume of the current playback device (0-100).

Raw Regex | ``set vol(ume)? (?<volume>\d{1,3})``

- ```set volume 28```
- ```set vol 94```

## Seamless UI Integration
Upon receiving a play command, music player automatically plays the song if only one file is returned from
the search otherwise it will open a UI that is overlayed on top of the current window which allows you to choose.

Example:- If I search for **Decks Da**, it will automatically play it. If I search for **How**, it will return a list of songs.

![alt text](http://aryanmann.com/wp-content/uploads/2017/01/FilterSongsHow.png "Filtered Songs")

## Now Supports Remote Commands
Listing songs or searching for songs through a remote device (such as a TCP client on a mobile phone) will return a message from the server containing all resulting songs.
