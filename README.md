# Wingmate

A free companion app for **Aion 2** by BeeSpecial3D.

![Wingmate in Aion 2: Abyss alerts at the top, launcher and Settings on the right, checklist bottom left](docs/images/overview.jpg)

### Abyss and event alerts
A small countdown overlay for the Abyss and server events (Artifact Siege, Nahma, Rift portals, Arena and more), with spoken warnings from Bella before each one starts. NA and EU times.

![Abyss alerts overlay](docs/images/alerts.png)

### Checklists for each character
Daily and weekly lists that reset on their own, plus your own custom lists.

<img src="docs/images/checklist.png" alt="Checklist with dailies and weeklies" width="360">

### Launcher
Pick your region and character, and open or close the alerts and checklist windows. The **Menu** button on the alerts and checklist windows opens or closes the launcher; Settings and Custom colors close with it and come back when it reopens. Drag the launcher's edges or its corner grip to resize it. Wingmate remembers where you put the launcher and Settings (and the launcher's size), and if you close Wingmate with the launcher's **×**, the windows you had open come back where you left them next time.

**First run:** the launcher asks for your character name first. Type it and press **Save** (or Enter); the Checklist unlocks once a name is saved. The alerts work right away.

**Coming soon:** Character stats.

<img src="docs/images/launcher.png" alt="Wingmate launcher" width="360">

### Lock windows
Click the **padlock** on the alerts or checklist window to lock it: clicks then pass straight through to the game. The padlock itself stays clickable, so click it again to unlock. You can also use **Alert window lock** and **Checklist window lock** in Settings or the tray menu, or the optional window lock hotkey (Ctrl+Alt+K by default).

### Settings
Text size, theme, voice volume, which alerts to hear, opacity and window locks, all in one place. Text size applies right away to every window, including Settings, Custom colors, the color picker and the update pop-up.

<img src="docs/images/settings.png" alt="Settings window in the Default theme" width="360">

### Themes
Default, Elyos, Asmodian, Abyss, Honey, Midnight and Light, each with its own opacity and warning color. Or pick **Custom** and choose every color yourself (background, text, borders, accents, checkmarks, the warning color and more).

<table>
  <tr>
    <td><img src="docs/images/theme-default.jpg" alt="Default theme in game"></td>
    <td><img src="docs/images/theme-asmodian.jpg" alt="Asmodian theme in game"></td>
    <td><img src="docs/images/theme-abyss.jpg" alt="Abyss theme in game"></td>
  </tr>
  <tr>
    <td align="center">Default</td>
    <td align="center">Asmodian</td>
    <td align="center">Abyss</td>
  </tr>
  <tr>
    <td><img src="docs/images/theme-honey.jpg" alt="Honey theme in game"></td>
    <td><img src="docs/images/theme-midnight.jpg" alt="Midnight theme in game"></td>
    <td></td>
  </tr>
  <tr>
    <td align="center">Honey</td>
    <td align="center">Midnight</td>
    <td></td>
  </tr>
</table>

<table>
  <tr>
    <td><img src="docs/images/checklist.png" alt="Checklist, Default theme"></td>
    <td><img src="docs/images/checklist-asmodian.png" alt="Checklist, Asmodian theme"></td>
    <td><img src="docs/images/checklist-abyss.png" alt="Checklist, Abyss theme"></td>
    <td><img src="docs/images/checklist-honey.png" alt="Checklist, Honey theme"></td>
    <td><img src="docs/images/checklist-midnight.png" alt="Checklist, Midnight theme"></td>
  </tr>
  <tr>
    <td align="center">Default</td>
    <td align="center">Asmodian</td>
    <td align="center">Abyss</td>
    <td align="center">Honey</td>
    <td align="center">Midnight</td>
  </tr>
</table>

<table>
  <tr>
    <td><img src="docs/images/alerts.png" alt="Abyss alerts, Default theme"></td>
    <td><img src="docs/images/alerts-asmodian.png" alt="Abyss alerts, Asmodian theme"></td>
    <td><img src="docs/images/alerts-abyss.png" alt="Abyss alerts, Abyss theme"></td>
  </tr>
  <tr>
    <td align="center">Default</td>
    <td align="center">Asmodian</td>
    <td align="center">Abyss</td>
  </tr>
  <tr>
    <td><img src="docs/images/alerts-honey.png" alt="Abyss alerts, Honey theme"></td>
    <td><img src="docs/images/alerts-midnight.png" alt="Abyss alerts, Midnight theme"></td>
    <td></td>
  </tr>
  <tr>
    <td align="center">Honey</td>
    <td align="center">Midnight</td>
    <td></td>
  </tr>
</table>

### Custom colors
In Settings, **Start from current theme** copies the theme you're using into Custom and opens the **Custom colors** window. **Edit custom colors** opens or closes it. Each color has a **Pick...** button and a **Default** button, and the warning color is one of them.

The color picker has a shade square, a hue slider and a hex box with a copy button. Changes show live in every Wingmate window while you pick; **OK** keeps the color, **Cancel** puts the old one back.

<table>
  <tr>
    <td><img src="docs/images/custom-colors.png" alt="Custom colors window" width="360"></td>
    <td valign="top"><img src="docs/images/color-picker.png" alt="Color picker" width="380"></td>
  </tr>
  <tr>
    <td align="center">Custom colors</td>
    <td align="center">Color picker</td>
  </tr>
</table>

### Updates itself
When a new version is out, Wingmate shows a small pop-up: **Update now** downloads it and restarts into the new version, **Later** closes the pop-up. If you pick **Later**, the **Update available** link on the launcher or the tray menu brings the pop-up back. Updating never touches your lists or settings.

**Wingmate never touches the game.** It doesn't automate anything, read game memory, or hook your keyboard or mouse. It is a separate window that sits on top of the game. The optional window lock hotkey uses the standard Windows hotkey API.

## Install

1. Go to [Releases](https://github.com/BeeSpecial3D/Wingmate/releases/latest) and download **`BeeSpecial3D.Wingmate-win-Setup.exe`**.
2. Run it. Wingmate installs for your user only (no admin needed) and adds Desktop and Start menu shortcuts.

> **"Windows protected your PC"?** Wingmate isn't code-signed yet, so Windows SmartScreen may warn you the first time you run the installer. Click **More info**, then **Run anyway**. You only need to do this once. Wingmate's source code is public, so you can read every line of it in this repo. It doesn't read game memory, inject anything, or automate input.

To uninstall, use Windows **Settings → Apps → Installed apps → Wingmate**.

## Your data

Your settings, characters and checklists live in `%LOCALAPPDATA%\Wingmate`:

| File | What it holds |
|---|---|
| `settings.json` | window positions, theme, alerts, voice |
| `checklist.json` | your characters and their lists |
| `schedule-cache.json` | the latest event schedule |
| `wingmate.log` | a small log, handy for bug reports |

The program itself installs to `%LOCALAPPDATA%\BeeSpecial3D.Wingmate`. Updates and uninstalling only touch that folder, so your data is never affected. Back up `%LOCALAPPDATA%\Wingmate` if you'd like a copy.

## Feedback

Found a bug or have an idea? Use **Send feedback** in Settings or the tray menu, or [open an issue](https://github.com/BeeSpecial3D/Wingmate/issues). Including your `wingmate.log` helps.

Wingmate is free. If you'd like to support it: https://ko-fi.com/beespecial3d

## For developers

- Build: `dotnet publish src/Wingmate -c Release -o publish` creates a single self-contained `Wingmate.exe` (win-x64; cross-building from Linux works).
- Tests: `dotnet test tests/Wingmate.Tests`
- Event schedule: [`schedule/schedule.json`](schedule/schedule.json). The app bundles a copy and fetches this file from `main` every 6 hours. Bump `version` when you edit it; the newer version wins.
- Voice clips: `python tools/render_voice.py <kokoro models dir> src/Wingmate/Assets/voice` (Kokoro, voice `af_bella`).
- Release: `vpk pack -u BeeSpecial3D.Wingmate -v <version> -p publish -e Wingmate.exe`, then upload the output to a GitHub release tagged `v<version>`.
