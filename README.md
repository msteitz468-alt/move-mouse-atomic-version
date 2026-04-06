# Move Mouse Linux

Move Mouse is a utility that automates mouse movement and keyboard actions on Linux to prevent screensavers from locking your computer and active status signals (like Microsoft Teams or Slack) from timing out. 

Built with **.NET 8** and **Avalonia UI**, this is an open-source, native Linux implementation designed to be familiar to users of the immensely popular Windows version.

## Features

- 🖱️ **Keep Alive**: Automatically moves the mouse cursor at configurable intervals.
- ⌨️ **Simulate Keystrokes**: Send key presses to keep sessions active without moving the cursor.
- 🕒 **Schedules**: Define blackout periods (e.g. nights) or specific schedules where Move Mouse will automatically start and stop.
- ⚙️ **Custom Actions**: Configure exactly what direction, speed, and patterns the mouse should follow (e.g., stealthy micro movements vs random wandering).
- ✋ **Auto-Pause**: Automatically break and pause movements when user user activity is detected.

## Requirements

Move Mouse Linux relies on native X11 tools to poll system idle times and simulate hardware input. It requires the following packages to be installed:

```bash
sudo apt install xdotool xprintidle wmctrl
```
*(Note for Wayland users: The utility uses Xwayland compatibility features to function. Ensure apps you want to keep awake are running in compatible sessions).*

## Installation

### From Debian Package (Recommended)

1. Check the [Releases](https://github.com/msteitz468-alt/move-mouse-linux/releases) page for the latest `.deb` package.
2. Install it using `apt` (which will automatically resolve dependencies):
   ```bash
   sudo apt install ./move-mouse_4.0.0-1_amd64.deb
   ```

### Building from Source

You'll need the **.NET 8 SDK**, `debhelper`, and `dpkg-dev` installed. 

Clone the repository and run the packaging script:

```bash
git clone https://github.com/msteitz468-alt/move-mouse-linux.git
cd move-mouse-linux
./build-deb.sh
```

This will automatically publish a self-contained single-file binary and build a standard Debian package inside the project directory.

## Known Issues

- **Wayland Native Apps**: True Wayland-native compositors fiercely restrict global programmatic cursor movement. Move Mouse relies on `xdotool` logic and works optimally under X11 or when moving the pointer across Xwayland windows.
