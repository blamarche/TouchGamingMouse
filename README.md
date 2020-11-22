# TouchGamingMouse

## Overview
TouchGamingMouse is an overlay tool that allows many Windows games to be playable using a touchscreen device such as a Surface Pro or 2-in-1 laptop. Due to certain implementation decisions, most Windows games don't respond properly to touch events and have issues such as not registering taps as clicks, registering taps as double-clicks, or any other number of undesired behavior. Games that should work well with touch screens such as turn based strategy and grand strategy games are normally totally unplayable!

TouchGamingMouse addresses this in two ways. The first and perhaps most critical way is by intercepting mouse events and resending them in a way that most DirectX games will properly detect and handle as normal mouse events. In addition, it adds an overlay with commonly used keyboard keys and extra mouse buttons that are normally not possible with a Windows touch screen. These are by located along the bottom and right edge of the screen by default.

A great example of the type of games made playable with TouchGamingMouse are the Grand Strategy games created by Paradox Interactive such as Stellaris, Europa Universalis IV, Hearts of Iron IV, etc.

![Screenshot](https://i.imgur.com/woOrwfo.jpg)

## Usage

* Install AutoHotkey
* Download and install TouchGamingMouse from the releases section
* Run TGMLauncher and choose a configuration
* Run your game in borderless window mode, or windowed mode.
* When done, right click the TouchGamingMouse icon in the system tray to exit.

## Gestures

Some configs (the default, and Paradox Classic) now support gestures which start near the center of your screen.

* Zoom in / out: Start by touching near the center of the screen, then move your finger in a clockwise or counterclockwise circle. A red dot will show in the center of the screen, circle around this to emulate mouse-wheel up/down.
* Pan the camera: Start by touching near the center of the screen. Slightly swipe in the opposite direction you wish to pan, then pull back to your starting position in the direction you wish to move.

## Advanced Usage
```
Command line options for TouchGamingMouse:
--config=<file.json> | Specify a specific config file to load so different games can have their own profile.
--writeconfig | Creates the file config.json with the built-in default config, or the contents of the file specified with --config, then exits.
---noahk | Forces the application to skip launching the autohotkey script, even if the config specifies otherwise.
--skipahkcheck | By default the application checks if Autohotkey is installed and warns the user if its not, this options suppresses that message.
```

Example: `TouchGamingMouse.exe --config=dice-config.json`

## Thanks
Thank you to the developers of AutoHotkey!

# Development

## License
Code: GPLv3

## Contribution
Feel free to submit pull requests containing feature enhancements. Any submitted pull request code must be licensed GPLv3 to be included.  

## Roadmap

* Replace AutoHotkey dependency, or bundle it within the app itself.

## Build
Load the Visual Studio project in VS2019. Ensure all dependencies/references are present. Build & Run.
