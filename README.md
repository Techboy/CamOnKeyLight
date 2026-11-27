# Cam On Key Light

Automatically turns on and off your Elgato lights when you turn on and off your webcam.

## What are Elgato lights?

[Elgato](https://www.elgato.com/) makes Wi-Fi LED panel lights, such as the Key Light, Key Light Air and Key Light Mini, for people who present, stream or work on camera. You can adjust their brightness and colour temperature from your computer or phone.

## Why use them on calls?

Most rooms light you badly for video. A window behind you leaves your face in shadow, and overhead lights cast harsh shadows. A Key Light set up in front of you gives even, soft light on your face, so you look clear and natural on Teams, Zoom, Google Meet and other calls.

## What Cam On Key Light does

You shouldn't have to remember to switch your lights on before every call and off again afterwards. Cam On Key Light runs in the Windows system tray and watches for any app using your webcam. When a call turns your camera on, it turns your lights on. When the camera goes off, it turns them off again after a short delay. You can still switch them on or off by hand from the tray at any time.

## Get cashback on an Elgato light

Buying an Elgato light? Sign up through my [TopCashback referral link](https://www.topcashback.co.uk/ref/techboy) to get cashback on your purchase.

## How it looks

The main window is a dark status dashboard. A hero line says whether the lights are on or off and why, for example **Lights are off** / **because your camera is off**, with a pill such as **Auto: lights follow the camera**. Each light is a card with its name, address, group and an on/off pill. **Add light**, **Find lights** and **Retry** sit under the cards. **Auto / On / Off** is a segmented control, and the delays are in seconds. **Settings** and **Help** are in the header. The footer links to documentation, issues, releases and About.

![Main window in dark mode: lights are off because the camera is off, with Auto selected and delays in seconds](docs/camonkeylight-main-window.png)

## Features

- In **Auto**, lights follow the webcam. **On** holds every configured light on, and **Off** holds them off.
- Each light shows its own status: On, Off, Unreachable, Unexpected reply or Checking.
- **Find** searches the local network. On startup the app also reads addresses saved by Elgato Control Center.
- Optional descriptions and groups. From the tray, a group can be switched on or off, or set to 25, 50, 75 or 100 percent brightness.
- Quiet hours, and a choice of which camera apps count.
- Optional keyboard shortcuts for Auto, On and Off.
- Dark (the default for a new install), Light, or System. A choice you already saved is kept. System is the saved value `FollowWindows`.
- A per-user installer and a portable zip. Updates come from the stable releases of this project.
- Starts when you sign in, if you tick that option.

## Requirements

- 64-bit Windows 11, or 64-bit Windows 10 Enterprise or LTSC (versions 1607, 1809, and 21H2). The .NET 11 runtime is included in the download, so you do not install it separately. .NET 11 needs a 64-bit processor with SSE4.2, which these Windows versions already require.
- This PC and the lights on the same local network, unless you set up the [home relay](#home-relay). The app talks to each light over HTTP. The port is 9123 unless you put a different port on the address.
- The setup program installs for the current user and does not ask for administrator approval.

The app leaves Windows networking, VPN, routes and company security software as they are. The relay is optional. With the relay address empty, the app behaves as it did before.

## Download

The latest stable release is [on GitHub](https://github.com/Techboy/CamOnKeyLight/releases/latest).

- **`CamOnKeyLight-Setup.exe`**: download this one. It is the normal installer.
- **`CamOnKeyLight-win-x64.zip`**: the portable copy. Unzip it and run it. The automatic updater also uses this file.
- **`.sha256` files**: checksums for checking a download. You can ignore them.
- **`AutoKeyLight-win-x64.zip`**: the package version 0.6.0 expects. For a new install, use the setup program or `CamOnKeyLight-win-x64.zip`. See [Updates](#updates).

The build is not code-signed. There is no paid certificate. Windows SmartScreen may say "Windows protected your PC" the first time you open the installer or the program. Choose **More info**, then **Run anyway**.

### Installer

1. Download `CamOnKeyLight-Setup.exe`. `CamOnKeyLight-Setup.exe.sha256` is the checksum for that file.
2. Run the setup. It installs to `%LOCALAPPDATA%\Programs\CamOnKeyLight`, adds a **Cam On Key Light** shortcut to your Start menu, and adds a per-user uninstall entry in Windows Settings.
3. If an older Auto Key Light install is there, setup closes it, removes that install, its Start menu shortcut and its uninstall entry, and keeps your settings.
4. If Cam On Key Light or Auto Key Light is already running, setup asks you to close it before replacing the program files.

Uninstall removes the program, the Start menu shortcut, and a sign-in startup entry that still points at this install. It leaves `%LOCALAPPDATA%\CamOnKeyLight` in place, which is where your settings and update backup live.

On a new install, **Start when I sign in** stays off until you tick it. If an older copy already started at sign-in, that choice is kept.

### Portable zip

1. Download `CamOnKeyLight-win-x64.zip` and, if you want to check it, `CamOnKeyLight-win-x64.zip.sha256`.
2. Extract it to a normal folder you can write to. Run it from that folder, not from inside the zip, and not from `C:\Windows`.
3. The .NET runtime is included.

The automatic updater always downloads that zip, whether you installed with setup or extracted the zip. It replaces the program files in the folder you are running, and it does not switch a portable folder over to the Start menu install. The setup program is not an automatic update.

## Usage

Open **Cam On Key Light** from the Start menu, or run `CamOnKeyLight.exe`. The app stays in the notification area. Its icon is a light bulb, lit while the camera is on.

Closing the window hides it and leaves the app running. Double-click the icon to open the window again. **Exit** on the tray menu quits. If you start the app while it is already running, the second start does nothing.

**Auto**, **On** and **Off** are in the window and on the tray menu. The choice is saved for this Windows user. The window title, the pill under the hero line, and the tick on the tray menu show which one is selected. The tray icon shows the camera, not the mode.

**Help** contains **Documentation**, **Report an issue**, **Release notes** and **GitHub project**, which open this project's pages in the browser, and a link to the Elgato website. **About Cam On Key Light** is last, after a separator. The same documentation, issue and release links, plus About, are along the bottom of the main window. **About** also links to the GitHub project and to the Elgato website. **Settings** in the header, and **Settings…** on the tray, opens the extra settings.

## Lights

Each card shows the description (or the address, if you have not named it), the address, the group and the status.

- **Add light** or **Edit** asks for an address, a description and an optional group. **Edit** is on the card's action button. You can also double-click a card, or press Enter or F2. Delete removes the selected light.
- An address is the light's IP, for example `192.168.1.5`. Add `:port` only when the light is not on port 9123.
- **Find lights** on the window, and **Find lights…** on the tray, searches the local network for Elgato lights (`_elg._tcp.local`). The dialog lists the name, address and port. Lights already in the list are skipped. If none answer, the dialog says so.
- A light added from Find remembers its name and serial. If that address later stops responding, then after about 20 seconds the app looks for the same light and updates the address when the match is unique.
- On startup, and again after Add or Edit, the app reads any addresses stored by Elgato Control Center. If a removed address comes back, see [Troubleshooting](#troubleshooting).
- **Retry** checks every light again. A light that does not answer within 5 seconds is shown as Unreachable. The check only asks the light for its status.
- The status text is **On**, **Off**, **Unreachable**, **Unexpected reply**, **Checking…** or **Not checked**. The summary says **Lights are ON** only when every listed light reported on.

The description is saved with the address, for example "Light on the left". A group name is shown in the list. Once any light has a group, the tray **Groups** menu can turn that group on or off, or set its brightness to 25, 50, 75 or 100 percent. Lights with no group are listed there as Ungrouped.

Automatic on and off follows the camera for every light, unless Settings limits that to chosen groups. Lights with no group follow the camera only when every group does.

## Timing

Both boxes are shown in seconds, in steps of 0.1, and both apply only in **Auto**. The settings file still stores milliseconds, so a value saved by an older version still loads. The window does not rewrite that value until you change the box. 1500 ms is shown as 1.5 seconds.

**Turn on after** is how long the camera has to stay on before the lights turn on. A new install uses 0. The maximum is 10 seconds.

**Turn off after** is the pause after the camera turns off, so switching camera apps does not flicker the lights. A new install uses 1.5 seconds. If you already saved a value, including 0, that saved value is kept. The maximum is 60 seconds. Set it to 0 for an immediate automatic off. If the camera comes back before the delay ends, the pending off is dropped and the lights stay as they were.

**On** and **Off** apply as soon as you choose them. Manual Off does not wait for the off delay.

## Light control

- **Auto** is the original behaviour and the default. Lights follow the webcam. An existing install stays on Auto.
- **On** turns every configured light on and keeps it on, even when the webcam turns off. The next time the app starts, it restores On.
- **Off** turns every configured light off and keeps it off, even when the webcam turns on. The next time the app starts, it restores Off.
- Choosing **Auto** after On or Off sets the lights from the camera's current state straight away. The delays apply to camera changes after that.
- The camera monitor keeps running while the mode is On or Off, so the window can still show the real camera state. In those modes the lights stay on or off as you set them.

## Keyboard shortcuts

Shortcuts are optional, and none are set by default, so the app leaves keys free for other programs.

Open **Shortcuts** from the window or the tray menu. Choose Set, then press Ctrl, Alt or Shift together with a letter, number or function key (F1 to F24). Esc cancels. Clear removes one. A combination that rarely clashes is Ctrl+Alt+Shift with a letter.

The shortcut works while the window is closed. The tray menu shows it next to Auto, On or Off. Common editing and Windows shortcuts, such as Ctrl+C and Alt+F4, are refused, and the Windows key is refused too. If Windows says another program already owns the shortcut, the app tells you and leaves the previous shortcut in place. Shortcuts are released when you exit from the tray menu.

## Settings

**Settings** in the header, and **Settings…** on the tray, holds the settings that are not on the main window. A list on the left opens Quiet hours, Camera apps, Groups, Notifications, Home relay and About. **Save changes** keeps them. Home relay is optional. Leave **Use a home relay** off unless you use the [home relay](#home-relay). Turning that switch off and saving clears the saved relay address. The secrets file is kept.

### Quiet hours

Quiet hours stay off until you tick **Enable quiet hours**. The times start at 22:00 and 07:00, which crosses midnight, and every day starts selected. If the start and end are the same, the selected days are quiet all day. Use 24-hour times.

While quiet hours are active, automatic mode does not turn lights on. On and Off from the tray still work, and so do On and Off in the window. The tray tooltip and menu show whether quiet hours are active.

![Quiet hours, off, from 22:00 to 07:00, every day selected](docs/camonkeylight-options-quiet-hours.png)

### Camera apps

**All apps** is selected by default, so any app using the camera counts. **Only these apps** counts only the ticked names. **All except these apps** ignores the ticked names. Names appear after an app uses the camera, or you can type an exe name and press **Add**.

![Camera apps, with All apps selected](docs/camonkeylight-options-camera-apps.png)

### Groups

**All groups follow the camera** is ticked by default. Untick it to choose which groups follow. Automatic switching stays per light. Lights with no group follow the camera only when every group does. The tray menu can still turn a group on or off, or set its brightness.

![Groups, with all groups set to follow the camera](docs/camonkeylight-options-groups.png)

### Notifications

**Notify when a light goes offline or comes back** is on by default. Offline and online notices wait about 20 seconds, so a light that flickers does not send a burst of messages. The first check after startup does not notify. **Notify when an update is installed** is off until you turn it on.

![Notifications, with offline notices on and update notices off](docs/camonkeylight-options-notifications.png)

## Appearance

Choose **Dark**, **Light** or **System**. A new install uses Dark. A choice you already saved is kept, including Follow Windows, which is what **System** stores. System matches the Windows app theme. The light theme uses the same warm gold on a paper background. While Windows high contrast is on, the app keeps the Windows system colours.

## Settings

Light addresses, descriptions, groups, delays, theme, light mode, shortcuts, quiet hours, camera-app rules, notification choices, the update tick box and the relay address are stored in `%LOCALAPPDATA%\CamOnKeyLight\user-settings.json`. That file sits outside the program folder, so installing, updating or moving the program leaves it in place. A settings file from an older version still loads. Anything that file does not mention yet uses the default. The Cloudflare Access client id, client secret and the relay shared secret are not in that file. They are stored beside it in `relay-secrets.bin`, protected with Windows data protection for the current user. Uninstall leaves this folder in place.

The first time this version starts, if that file is missing and `%LOCALAPPDATA%\AutoKeyLight\user-settings.json` exists, the app copies the old file and leaves the original in place. It also picks up an existing `user.config` from the older per-folder settings when that file still has your lights. A saved off delay of 0 stays 0. Descriptions start blank until you edit them.

### Start when I sign in

The tick box follows the current user's Startup apps list (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`). The app runs after you sign in. It does not run before Windows login, and it does not need administrator approval. The command is stored in quotes under the name `CamOnKeyLight`. An older `AutoKeyLight` or `MyApp` entry that pointed at this app is moved to that name and updated to the copy you are running, so a previous folder is not launched after you move or install the app. If sign-in startup was off, it stays off.

## Updates

This version checks only stable releases of [Techboy/CamOnKeyLight](https://github.com/Techboy/CamOnKeyLight/releases) and downloads `CamOnKeyLight-win-x64.zip`. Pre-releases and drafts are ignored. It does not update from the upstream [astutejoe/AutoKeyLight](https://github.com/astutejoe/AutoKeyLight) project, and it does not send a token or other credential.

**Install updates automatically** is ticked by default. Untick it to stop background checks and unattended installs. While it is ticked, the first check runs shortly after startup, then about every six hours. **Check for updates** still works with the tick box clear, and **Install update** appears only after a package has been downloaded and checked.

An update waits while the webcam is on. Automatic install also waits until the camera has been off for 15 seconds. If GitHub cannot be reached, the app keeps the version you already have and says so.

A download is accepted only from this repository's GitHub release. The zip is checked against GitHub's asset digest, when GitHub provides one, and against the published SHA-256 file. It is then extracted with path checks. The previous program files are copied to `%LOCALAPPDATA%\CamOnKeyLight\updates\backup` before they are replaced, and restored if the copy fails or the new program's version does not match the release. After a successful update the app restarts into the tray.

Version 0.6.0 is different. It asks GitHub for a repository named Techboy/AutoKeyLight, and it only accepts a download address under `/Techboy/AutoKeyLight/releases/download/`. This repository is now Techboy/CamOnKeyLight, and GitHub lists the files under that name, so a PC still on 0.6.0 will not take the update by itself. Install `CamOnKeyLight-Setup.exe`, or the current portable zip, on that PC. The release still includes `AutoKeyLight-win-x64.zip` because that is the package shape 0.6.0 knows how to open: it has to contain `AutoKeyLight.exe` and it must not contain `CamOnKeyLight.exe`.

## Home relay

Use this when the PC cannot reach the lights on the home LAN, for example while a work VPN is connected and that VPN captures private addresses (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`). Normal internet traffic still goes out directly, so the app can reach a relay on your NAS through a Cloudflare Tunnel. The app tries each light directly first. If that attempt does not connect, it sends the same command through the relay. It remembers the failure for about 45 seconds, so the next switch does not wait on the direct attempt again. Brightness commands are sent as the same JSON the light already accepts, including colour temperature when a body contains it.

With the relay address left empty, none of this runs. Timeouts, status text and the network calls stay as they are today.

```mermaid
flowchart LR
    subgraph Laptop["Work laptop (on VPN)"]
        Cam["Webcam turns on"] --> App["Cam on Key Light app"]
    end

    App -. "1. Direct try on port 9123 (blocked by VPN)" .-x Light

    App -- "2. HTTPS with Access service token" --> CF["Cloudflare Access<br/>lights.example.com"]
    CF -- "Cloudflare Tunnel" --> Tunnel["cloudflared on home NAS"]

    subgraph Home["Home network"]
        Tunnel --> Relay["Relay container<br/>(checks shared secret)"]
        Relay -- "Local HTTP on port 9123" --> Light["Elgato Key Light"]
    end
```

The app always tries the light directly first. If a VPN blocks that, it sends the on/off request to your relay hostname over HTTPS. Cloudflare Access only lets it through with the service token, and the Cloudflare Tunnel carries it to the relay container on your home network, which checks the shared secret and switches the light locally.

The relay is a small web service in `relay/`. It only forwards to the light addresses you list. It does not accept an arbitrary URL. Cloudflare Access authenticates the PC with a service token. An optional shared secret is checked again on the NAS.

The container listens on port 8080. These are the paths:

| Method | Path | What it does |
| --- | --- | --- |
| GET | `/health` | Answers without the shared secret, for the container health check. |
| GET | `/v1/ping` | Checks the secret and reports how many lights are configured. It does not change a light. |
| GET | `/v1/lights?address=` | Reads one light. |
| PUT | `/v1/lights?address=` | Forwards a JSON body to that light. |
| POST | `/v1/lights/on?address=` | Turns that light on. |
| POST | `/v1/lights/off?address=` | Turns that light off. |

When `RELAY_SHARED_SECRET` is set, every path except `/health` requires the header `X-Relay-Secret`. The app also sends `CF-Access-Client-Id` and `CF-Access-Client-Secret` when those boxes are filled in.

### NAS container

The image is published to GHCR as `ghcr.io/techboy/camonkeylight-relay`. The moving tag is `latest`. A build also tags `sha-<short sha>`, and a version tag such as `v1.2.3` publishes that version. GitHub marks a new package private. In the package settings, make it public, or sign Container Manager in to `ghcr.io` with a personal access token that can read packages. The image has no secrets in it.

LCARS is an x86-64 Synology. The image is built for `linux/amd64` and `linux/arm64`. In Container Manager, create a project from `relay/docker-compose.yml`, or create a container from the image with the host network and the environment variables below.

1. Put the lights in `LIGHTS`, separated by commas. `192.168.1.5` means port 9123. Add `:port` only when a light is not on 9123.
2. Set `RELAY_SHARED_SECRET` to a long random string. Leave it unset only if you are content with Cloudflare Access as the only check.
3. Leave `DISCOVER` at `false` unless you want the container to look for `_elg._tcp.local` on the LAN. Discovery needs the host network. Addresses you listed are kept either way.
4. Use the host network. The process then listens on port 8080 of the NAS, and it can open connections to the lights. Do not forward port 8080 from the router.
5. Set the restart policy to always, or `unless-stopped` as in the compose file.
6. On the NAS, `http://127.0.0.1:8080/health` should return `{"status":"ok"}`.

### Cloudflare Tunnel

1. In the Cloudflare Zero Trust dashboard, open **Networks** → **Tunnels** and create a tunnel. Install `cloudflared` on the NAS with the token the dashboard shows. A second Container Manager container running `cloudflare/cloudflared` with that token is enough. Give that container the host network as well, so it can reach `127.0.0.1:8080`.
2. Add a public hostname. The subdomain is yours, for example `lights`, on a domain Cloudflare already hosts. The service type is **HTTP**. The URL is `http://127.0.0.1:8080`. Leave the path empty. Save the hostname.

### Cloudflare Access service token

1. Open **Access** → **Service credentials** → **Service tokens**. Create a token named `Cam On Key Light`. Copy the Client ID and the Client Secret. The secret is shown once. Store it in a password manager.
2. Open **Access** → **Applications** → **Add an application** → **Self-hosted**. Name it `Key lights`. The application domain is the public hostname from the tunnel, for example `lights.example.com`.
3. Add one policy. Name it `Service token`. The action is **Service Auth**. The include rule is **Service Token**, and the value is the token you just created. Do not add a policy that allows everyone, and do not add an email login policy. Save the application.
4. A request without the service token is stopped by Cloudflare and never reaches the NAS. A manual check uses placeholders, not real secrets:

```bash
curl -H "CF-Access-Client-Id: YOUR_CLIENT_ID" \
  -H "CF-Access-Client-Secret: YOUR_CLIENT_SECRET" \
  -H "X-Relay-Secret: YOUR_SHARED_SECRET" \
  https://lights.example.com/v1/ping
```

### App settings

Open **Settings** → **Home relay** and turn on **Use a home relay**.

![Home relay, with Use a home relay off and the address and secret fields empty](docs/camonkeylight-options-relay.png)

1. Relay URL: `https://lights.example.com` (your public hostname, with `https://`, and no path).
2. Access client ID: the Client ID from the service token.
3. Access client secret: the Client Secret from the service token.
4. Shared secret: the same string as `RELAY_SHARED_SECRET`. Leave it empty only when the container has no shared secret.
5. Press **Test relay**. A working setup says the relay answered and how many lights are configured. The test does not switch the lights. **Save changes** writes the address in `user-settings.json` and the three credential fields in `relay-secrets.bin`. The two secrets are masked until you press **Show**.

The window shows **via relay** on a light, and in the lights summary, while that path is in use. The tray tip gains a relay note when it still fits. After you disconnect the VPN, the next successful direct check drops the note.

## Troubleshooting

**An address you removed comes back after a restart.** The app reads lights from `%APPDATA%\Elgato\ControlCenter\Settings.xml` when Elgato Control Center has saved them. Open that file and delete the old `<IpAddress>` entries inside `<Accessories>`, or delete the file. Deleting the file clears the settings Control Center stored there.

**A light is missing from the list.** Press **Find**, or type the IP yourself. Elgato Control Center, opened from the system tray, lists the lights on this PC. Open a light there to see its IP address.

![Elgato Control Center opened from the system tray, showing the left and right Key Lights](docs/camonkeylight-control-center.png)

If you can reserve an address for the light on your router, it keeps the same IP after the light restarts.

**The status says Unreachable.** The light did not answer within 5 seconds, or, when a relay is configured, the direct attempt and the relay both failed. Check that this PC and the light are on the same network, then press **Retry**. Retry only checks the connection. It leaves VPN settings, routes and security software alone. If the lights are unreachable only while a work VPN is connected, and that VPN sends private addresses down the tunnel, set up the [home relay](#home-relay). The camera line is not affected.

**The status says On via relay or Off via relay.** The app could not reach that light directly, so it used the home relay. On the local network, with the VPN disconnected, the status goes back to On or Off with no relay note.

**The lights flicker when one camera app takes over from another.** Raise **Turn off after**. A new install uses 1.5 seconds, which is there for that gap.

**The lights stay off during a call.** Confirm the mode is **Auto**, quiet hours are not active, and **Camera apps** is not set to ignore the program you are in. The camera line follows Windows: it is on when an app is streaming from the camera.

**The lights stay on after a call.** **On** keeps them on. In **Auto** they wait out the off delay, and they stay on if Windows still reports the camera as in use.

**Nothing happens when you open the app.** It is already running, or you closed the window earlier. Look for the light-bulb icon in the notification area. **Exit** on that menu is what quits.

## Releasing a new version

The repository is [Techboy/CamOnKeyLight](https://github.com/Techboy/CamOnKeyLight). Publishing a release uses the free `windows-latest` runner. That workflow does not use a code-signing certificate, an Actions cache, or a stored Actions artifact.

1. Pick the next version, for example `0.8.0`.
2. Create a GitHub release and tag it `v0.8.0`. Leave it as a normal release, not a pre-release and not a draft. The tag has to look like `v1.2.3`.
3. Publish the release. The workflow attaches `CamOnKeyLight-win-x64.zip`, `CamOnKeyLight-win-x64.zip.sha256`, `AutoKeyLight-win-x64.zip`, `AutoKeyLight-win-x64.zip.sha256`, `CamOnKeyLight-Setup.exe` and `CamOnKeyLight-Setup.exe.sha256`.
4. Wait until those files are listed. People installing for the first time run the setup program. Copies already on 0.7.0 or later download `CamOnKeyLight-win-x64.zip`.

The version in the program comes from that tag. The workflow builds the zip files and the setup program from the same folder. Attach those files by hand only if the workflow failed. The app expects the checksum and GitHub's digest to describe the same zip.

`AutoKeyLight-win-x64.zip` is still produced so the legacy package keeps the file name version 0.6.0 will accept. A PC on 0.6.0 will not download it on its own after the repository rename. See [Updates](#updates).

## Build

The logic tests run on .NET 11, including on a machine that is not running Windows:

```bash
dotnet test --project CamOnKeyLight.Core.Tests/CamOnKeyLight.Core.Tests.csproj -c Release
dotnet test --project relay/CamOnKeyLight.Relay.Tests/CamOnKeyLight.Relay.Tests.csproj -c Release
```

The Windows app itself builds on Windows:

```powershell
dotnet publish CamOnKeyLight/CamOnKeyLight.csproj -c Release -r win-x64 --self-contained true
```

To build the packages a release would publish, on Windows, pass the version to stamp into the build:

```powershell
pwsh -NoProfile -File build/package.ps1 -Version 0.8.0
```

The script publishes the app, runs the package checks, and writes `CamOnKeyLight-win-x64.zip`, `AutoKeyLight-win-x64.zip` and `CamOnKeyLight-Setup.exe` under `dist`, with a `.sha256` file beside each one. The setup program is compiled with Inno Setup 7.1.1. If that compiler is not already unpacked in the build's work folder, the script downloads it.

The relay image is built from the repository root:

```bash
docker build -f relay/Dockerfile -t ghcr.io/techboy/camonkeylight-relay:local .
```

## Licence

Cam On Key Light is based on [astutejoe/AutoKeyLight](https://github.com/astutejoe/AutoKeyLight). It is MIT licensed. Copyright (c) 2022 Gabriel Garcia. See [LICENSE](LICENSE).

Cam On Key Light is an independent app. It is not made, sold or endorsed by Elgato.
