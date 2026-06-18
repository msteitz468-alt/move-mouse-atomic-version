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

Move Mouse Linux simulates hardware input through a pluggable backend and selects one automatically based on your session:

- **X11 sessions** use `xdotool` / `xprintidle` / `wmctrl`:
  ```bash
  sudo apt install xdotool xprintidle wmctrl
  ```
- **Wayland sessions** (e.g. KDE Plasma, GNOME) use `ydotool`, which injects input through the kernel's `/dev/uinput` device and therefore works where X11 automation is blocked. See the [Fedora Atomic & Wayland](#fedora-atomic-silverblue--kinoite--wayland) section below for setup.

The backend is chosen from `XDG_SESSION_TYPE`. You can override it with the `MOVEMOUSE_INPUT` environment variable (`xdotool` or `ydotool`).

## Installation

### From Debian Package (Recommended)

1. Check the [Releases](https://github.com/msteitz468-alt/move-mouse-atomic-version/releases) page for the latest `.deb` package.
2. Install it using `apt` (which will automatically resolve dependencies):
   ```bash
   sudo apt install ./move-mouse_4.0.0-1_amd64.deb
   ```

### Building from Source

You'll need the **.NET 8 SDK**, `debhelper`, and `dpkg-dev` installed. 

Clone the repository and run the packaging script:

```bash
git clone https://github.com/msteitz468-alt/move-mouse-atomic-version.git
cd move-mouse-atomic-version
./build-deb.sh
```

This will automatically publish a self-contained single-file binary and build a standard Debian package inside the project directory.

### Fedora Atomic (Silverblue / Kinoite) & Wayland

Fedora Atomic desktops are immutable (no `apt`, read-only `/usr`) and default to a Wayland session, so the `.deb` and the X11 tools don't apply. Move Mouse still works: it ships a **self-contained .NET binary** (no .NET runtime needed) and a Wayland input backend driven by `ydotool`.

The steps below install it entirely in your home directory, then add the one system package (`ydotool`) and the permissions it needs to inject input.

#### 1. Install the application (rootless, no reboot)

You can either build from source or extract the prebuilt `.deb` payload. Both drop a self-contained binary into `~/.local`.

**Option A — extract the prebuilt `.deb`** (the package is just an `ar` archive; `dpkg` is not required):

```bash
mkdir -p /tmp/mm && cd /tmp/mm
ar x /path/to/move-mouse_4.0.0-1_amd64.deb
mkdir -p out && tar xf data.tar.zst -C out

mkdir -p ~/.local/lib/move-mouse ~/.local/bin ~/.local/share/applications
cp -r out/usr/lib/move-mouse/. ~/.local/lib/move-mouse/
chmod 755 ~/.local/lib/move-mouse/move-mouse
ln -sf ~/.local/lib/move-mouse/move-mouse ~/.local/bin/move-mouse

# Desktop launcher with an absolute Exec path
sed 's|^Exec=move-mouse|Exec='"$HOME"'/.local/bin/move-mouse|' \
    out/usr/share/applications/move-mouse.desktop > ~/.local/share/applications/move-mouse.desktop
update-desktop-database ~/.local/share/applications 2>/dev/null || true
```

**Option B — build from source** in a [Toolbx](https://containertoolbx.org/) container (the host has no .NET SDK):

```bash
toolbox create -y dotnet-build
toolbox run -c dotnet-build sudo dnf install -y dotnet-sdk-8.0
toolbox run -c dotnet-build dotnet publish MoveMouseLinux.csproj \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=none -p:EnableCompressionInSingleFile=true \
    -o ~/.local/lib/move-mouse
ln -sf ~/.local/lib/move-mouse/move-mouse ~/.local/bin/move-mouse
```

Make sure `~/.local/bin` is on your `PATH` (it is by default on Fedora).

#### 2. Install `ydotool` and grant `/dev/uinput` access (root + one reboot)

```bash
# Layer ydotool onto the base image
rpm-ostree install ydotool

# Let your user open /dev/uinput via a udev rule + the 'input' group
echo 'KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"' \
  | sudo tee /etc/udev/rules.d/60-ydotool-uinput.rules
sudo usermod -aG input "$USER"

# Reboot to apply the layered package, group membership, and udev rule
systemctl reboot
```

#### 3. Run the `ydotoold` daemon as a user service

Move Mouse talks to the `ydotoold` daemon over a socket in `$XDG_RUNTIME_DIR`. Create a user service:

```bash
mkdir -p ~/.config/systemd/user
cat > ~/.config/systemd/user/ydotoold.service <<'EOF'
[Unit]
Description=ydotoold virtual input daemon (for Move Mouse on Wayland)
Documentation=man:ydotoold(8)

[Service]
Type=simple
Restart=always
RestartSec=2
ExecStart=/usr/bin/ydotoold --socket-path=%t/.ydotool_socket --socket-perm=0600

[Install]
WantedBy=default.target
EOF

systemctl --user daemon-reload
systemctl --user enable --now ydotoold.service
systemctl --user status ydotoold.service   # expect: active (running)
```

#### 4. Launch

Run `move-mouse`, or pick **Move Mouse** from your application launcher. On startup the log
(`$XDG_RUNTIME_DIR/move-mouse/Move Mouse.log`) should read `Selecting input backend: "ydotool"`.

**Troubleshooting:** if the cursor doesn't move, check that log. A `failed to connect socket .../.ydotool_socket` line means `ydotoold` isn't running — verify step 3 and that you rebooted after step 2.

## Auto-Pause on Wayland

**Auto-Pause**, **Auto-Resume**, and per-action **break-on-user-activity** work on Wayland even though the compositor exposes no idle-time API. The Wayland backend reads raw input events directly from `/dev/input/event*` to tell when you last used the keyboard or mouse, ignoring the cursor movement the app generates itself (ydotool's own virtual device is excluded).

This requires the app to be able to read those devices, which the [setup above](#fedora-atomic-silverblue--kinoite--wayland) provides by adding you to the `input` group. If the app can't read them, it logs `available=False` and these features stay disabled rather than misbehaving. Auto-Pause is enabled by default; configure it under **Settings → Auto-Pause**.

## Wayland feature coverage

The Wayland (`ydotool`) backend now covers the full action set:

- **Scroll-wheel actions** work via a dedicated virtual wheel device created through `/dev/uinput` (`ydotool` itself has no wheel command).
- **Activate-window-by-title** works on **KDE Plasma** via KWin's scripting D-Bus interface (matches window caption, class, or name). On other Wayland compositors it is a no-op.
- **Stop when locked** uses the freedesktop ScreenSaver D-Bus interface; **pause on battery** reads `/sys/class/power_supply`.

The only feature Wayland fundamentally can't provide is the **Position Cursor "Track"** helper (capturing the live pointer location), since clients cannot read the cursor position.

## Known Issues

- **`/dev/uinput` permissions**: The Wayland backend requires access to `/dev/uinput` and a running `ydotoold` daemon (see setup above). The same access powers the scroll device. Without it, input simulation silently fails — check the log.
