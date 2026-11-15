# Cascade Menu

A macOS Sequoia-inspired, world-space UI kit for **Gorilla Tag** on **BepInEx 5**. The look and feel are based on the Cascade Luau UI: dark glass, traffic lights, a collapsible 200px sidebar, rounded cards, native-feeling controls, search, dropdowns and toasts.

The **Mods** page now contains real gameplay features: a toggleable hand-link block and a head-relative joystick fly mode. The remaining pages show how to build and wire reusable UI rows.

## Features

- True world-space Canvas for both desktop and VR; no EventSystem required.
- Custom pointer hit-testing for desktop mouse and VR hand rays.
- High-contrast pointer: a laser line plus ring/dot cursor in VR, and a custom screen cursor on desktop.
- Dark glass theme by default, plus a light theme and a live seasonal theme system.
- Theme presets for Default, Halloween, Thanksgiving, Christmas, New Year, Valentine's, St. Patrick's, Easter, 4th of July, Summer, Fall, Winter, Dark and Light.
- Optional date-based Auto theme that picks a holiday or season at startup and whenever the menu opens.
- Custom color editor with RGB sliders, a hex field, quick-pick swatches and a live preview. Changes repaint every open window without rebuilding it.
- Mods are one switch: turning one on starts its effect immediately; turning it off stops it and runs cleanup.
- **Name Tags**: lightweight, display-only name labels above every remote gorilla rig, with an optional platform line, a fixed custom size, five appearance presets and off-screen fade. Separate from the full Player HUD so either can run alone.
- **Player HUD**: a display-only, themed information card above every other player. Name, rig color swatch, RGB/hex, player ID, Photon custom properties, tagged state, platform, ping, FPS, distance and cosmetic count. Cards are pooled per player and updated a few times a second, with no raycast targets, so nothing on them can be clicked. Name Tags and Player HUD both appear as simple toggle rows with gear buttons on the **Visual** page; each gear page has Classic, VRChat, Fortnite, Minimal and Glass appearance presets plus a name-color mode.
- **Talking Players**: a small, camera-anchored speaker list on the right of the view that shows only the players who are speaking, each with a speaker glyph and a name chip tinted with that player's gorilla color. There is no backing panel behind the chips and the rows are packed tight, so the list stays a thin strip at the screen edge. Sensitivity, linger time, row count, position, size, opacity, distance and text color are settings; the mod is a single toggle on the **Visual** page and costs nothing while off.
- **GunLib tab**: grip-gated targeting with Left/Right/Either grip hands, a selectable beam hand, a desktop mouse/key stand-in, configurable beam/ring colors and sizes, max distance, and a raycast that stops on everything solid - world geometry, props and construction pieces - while ignoring triggers and overlay beams.
- **Construction**: a local-only building kit. Place bricks, planks, beams, logs and balls, snap them to a 0.5 m grid, rotate in 45-degree steps, undo and clear builds, and demolish them with a projectile-firing demolition gun or a direct explosion. Pieces are solid blocks that stay exactly where they were placed - walls, floors and stairs behave like a real build instead of a pile of loose props - and an explosion releases them before throwing them. Dynamic sliding is still available by turning **Static pieces** off.
- **Profiler tab**: live per-frame cost for Name Tags, the Player HUD, the gun and Construction, with reset and log actions.
- macOS window chrome, traffic lights, titlebar, collapsible sidebar, back/forward history and sidebar search filtering.
- Reusable declarative pages, sections and rows.
- Toggles, animated sliders, buttons, steppers, radio groups and dropdown overlays.
- Real gameplay mods: block hand-link pickups and fly with the left stick.
- Toast notifications, tooltips via hover states and keyboard search input.
- Every sprite, rounded corner, ring, shadow and icon is generated at runtime. No asset bundle or external UI files are needed.
- High-resolution dynamic SDF text using a clean system font, with the game's localized TMP font kept as a glyph fallback. Text remains crisp instead of inheriting the game's low-resolution pixel font.

## Requirements

- Gorilla Tag with BepInEx 5 (x64). BepInEx's included `0Harmony.dll` provides the gameplay patches.
- .NET SDK 8.0 or newer to build. The project was built with .NET SDK 10.
- The referenced game/BepInEx assemblies are already in `libs`, so no Unity editor is required.

The project targets `netstandard2.1`.

## Build

From this folder:

```powershell
dotnet build -c Release
```

Output:

```text
bin\Release\CascadeMenu.dll
```

The build currently succeeds with zero warnings and zero errors.

### Refreshing the game references

The DLLs in `libs` are local copies of the assemblies from the current Gorilla Tag installation:

```text
D:\SteamLibrary\steamapps\common\Gorilla Tag
```

After a game update, replace `libs\Assembly-CSharp.dll` and the relevant `UnityEngine*.dll`, `Unity.TextMeshPro.dll`, and `Unity.InputSystem.dll` files with the current versions from `Gorilla Tag_Data\Managed`. Also copy the current `BepInEx.dll` and `0Harmony.dll` from `BepInEx\core`. Then rebuild. Do not copy game DLLs into `bin\Release`; they are marked `Private=false` and must be supplied by the game.

## Install

1. Build the plugin.
2. Copy `bin\Release\CascadeMenu.dll` into:

   ```text
   D:\SteamLibrary\steamapps\common\Gorilla Tag\BepInEx\plugins
   ```

   A subfolder such as `BepInEx\plugins\CascadeMenu\` also works.
3. Restart Gorilla Tag.
4. Press **F1**, **Insert**, **Tab**, or **B / Y** in VR to open the menu.

On first launch BepInEx creates:

```text
BepInEx\config\com.cascadeui.gorillatag.cfg
```

The menu starts hidden and its config is reloaded on the next launch.

## Controls

| Action | Desktop | VR |
|---|---|---|
| Open / close | F1, Insert, Tab | B or Y |
| Point | Mouse (custom cursor) | Right-hand ray + ring cursor |
| Select | Left click | Right trigger or A |
| Scroll | Mouse wheel | Right stick up/down |
| Toggle joystick fly | Movement toggle or Mods page | One switch; F5 also toggles it |
| Fly | - | Left stick, head-relative |
| Close | Escape | B / Y |
| Unfocus search | Escape | B / Y, or click elsewhere |
| Search | Click the field, then type | Click the field with the pointer, then use the keyboard |
| Delete search text | Backspace or the clear button | Clear button |

Traffic lights follow macOS conventions: green re-centers the window, yellow closes it, and red closes it.

## Gameplay mods

- **Block hand link** is enabled by default. It skips `GTPlayer.TakeMyHand_ProcessMovement`, so another player's hand link cannot carry the local player. Toggle it on the Mods page or with `Block Hand Link` in the config.
- **Joystick fly** is off by default. Turn on the Movement switch and flight starts immediately; turn it off and gravity is restored. The left stick controls forward/back and strafe; the right stick's vertical axis controls altitude. Movement aims with the head/camera by default, with an option to use the right controller. Speed, acceleration and deadzone are configurable. Flight input is smoothed on the physics step, and gravity plus old momentum are cleared when flight ends.
- **ESP** options work independently: boxes, names, tracers, through-wall body highlight and a through-wall diamond marker above each player. Highlight and marker color can be chosen in the Visual page.
- **Cosmetic ESP** recognizes the special cosmetic prefixes included in the pasted list and shows role labels above matching players. A separate notification toggle sends the same Cascade-style toast in a small standalone overlay, even while the menu is closed.
- **GunLib** is a thin client-side target gun. Enable it on the Fun tab or the GunLib tab, then hold the chosen grip (right hand by default) to show the beam and ring. Release grip and the beam, ring and pending hit are cleared immediately. The ray stops on anything solid - world geometry, props, invisible blockers and construction pieces - and only ignores triggers and the beam/trail overlays, so the beam never reaches through a wall. On desktop the default grip is the right mouse button; left, middle and a rebindable keyboard key are available. Grip is read separately from the trigger, so it does not click menu rows.
- **Construction** is local-only. Enable it on the **Fun** page (or the Mods list), hold the chosen grip, and pull the trigger. Build mode places the selected piece on the first visible surface, snapped to a 0.5 m grid by default; **A/X** or **R** rotates it 45 degrees, **Q** cycles the piece, **Z** undoes the newest piece and **Delete** clears the build. With **Static pieces** on, each placed block is kinematic: it stays where it was put, but it is still a solid collider, so players can stand on it, climb it and bump into it. Explosions unfreeze the affected pieces before applying force, so demolition still throws the build apart. Auto-weld only runs in dynamic mode; the Weld tool selects one piece with the trigger, then welds it to the next aimed piece. The demolition gun fires a physics round that explodes on contact, while Explosion mode detonates directly at the aim point. Pieces, previews, projectiles, welds and blast visuals all live under one local root and are destroyed when the mod stops.
- **Name Tags** and **Player HUD** both live on the **Visual** page as simple toggle rows, with a gear for their options. Toggling one on starts the overlay immediately. Name Tags draws only a themed name plate, optionally with the player's platform; Player HUD draws the full card. Their gear pages include appearance presets (**Classic**, **VRChat**, **Fortnite**, **Minimal**, **Glass**) and a name-color mode (style default, theme, white or rig color). Off-screen and distant objects fade out, while labels and cards keep a fixed size set by their Size slider.
- **Talking Players** also sits on the **Visual** page as one toggle. Its gear page sets which screen edge the compact list hangs from (right center is the default), size, opacity, sensitivity, linger time, maximum rows, showing your own mic and the speaker icon. The row color is always the speaker's gorilla color; the text can be automatic (dark on light, white on dark), plain white, dark, or the rig color itself on a dark chip.
- **Disconnect** is in the titlebar. It asks for confirmation unless either VR grip is held while pressing the button.

All of these features are local-only. The BepInEx log prints `Cascade gameplay patches installed` when the Harmony patches load.

## Pages

- **Home** - hero, primary/secondary actions, appearance dropdowns and a toggle.
- **Mods** - every discovered mod as a compact Enabled/Active row, with a settings gear only on mods that declare options.
- **Visual** - ESP, HUD and target markers, plus the Name Tags, Player HUD and Talking Players overlays as simple toggle rows with gear settings.
- **GunLib** - state, grip gating, beam/ring appearance and the gun's settings links.
- **Fun** - lobby tools plus the Construction toggle, undo, clear and its settings gear.
- **Profiler** - live Name Tags, Player HUD, Talking Players, GunLib and Construction frame cost.
- **Controls** - input, pointer and notification demonstrations.
- **Settings** - live UI scale, distance, theme, accent and behavior settings.
- **About** - credits, links and a restore-defaults action.

Use the sidebar search field to filter rows by their title, subtitle or search text. Search filtering is applied live and content scrolls back to the top when the results change.

## Project layout

```text
CascadeMenu/
  CascadeMenu.csproj
  libs/                       Local game and BepInEx references
  src/
    Plugin.cs                 BepInEx entry point
    PluginInfo.cs
    Menu/
      CascadeMenuBehaviour.cs Canvas, pointer, input, lifecycle, theme ownership, config rebuilds
      DemoApp.cs              Declarative pages and callbacks
      DemoState.cs            Session-only demo value store
      MenuConfig.cs           BepInEx config entries
      ThemesPage.cs           Preset list, Auto toggle and the custom color editor
      GunLibPage.cs           State, grip, beam/ring and settings links for the gun
      ProfilerPage.cs         Live per-frame cost view for the heavy features
      ProfilerStats.cs        Shared allocation-free frame-cost registry
      ModuleSettings.cs       Settings pages for the built-in modules
    Mods/
      Core/
        Mod.cs                One-switch lifecycle and settings declarations
        ModRegistry.cs        Discovery, persistence, generated rows and gear pages
        ModSetting.cs         Toggle/slider/stepper/dropdown/text model for mod options
      Game/
        GameplayMods.cs       Harmony installation and lifecycle
        GameplayPatches.cs    Hand-link and fly-gravity patches
      Movement/
        JoystickFlyController.cs Menu-switch flight, stick mapping and gravity cache
        AcrobaticsController.cs Body-rotation mode
        NoclipController.cs   Collision bypass
      Flight/
        WASDFly.cs            Desktop WASD free-flight
      Tools/
        GunLibController.cs   Grip-gated aim, solid-surface raycast, beam and ring
        GunLibMod.cs          Thin Mod wrapper exposing the gun's Enabled/Active state
        Construction.cs       Physics pieces, welds, demolition gun and explosions
        TagTools.cs           Tag-related tools
      Overlays/
        OverlayStyles.cs      Classic, VRChat, Fortnite, Minimal and Glass appearance presets
        NameTags.cs           Pooled display-only name plates above remote rigs
        PlayerHud.cs          Pooled display-only player cards above every player
        VoiceIndicator.cs     Talking-players strip with player-colored name chips
        HudOverlay.cs         Local status overlay
      Player/
        PlayerActions.cs      Player list actions
        SpectateController.cs Spectate mode
        FunActions.cs         Lobby actions
      Cosmetics/
        CosmeticBrowser.cs    Cosmetic catalog pages
        CosmeticChecker.cs    Cosmetic detection
        CosmeticShop.cs       Cosmetic shop page
      PC/
        PCButtonClicker.cs    Desktop button input
      Performance/
        NoMoreCosmetics.cs    Cosmetic suppression
      Super/
        SuperGuns.cs          Super-power guns
      Util/
        Util.cs               Shared helpers
    UI/
      CascadeWindow.cs        Window chrome, sidebar, titlebar, viewport, toasts, hit testing
      CascadeWidgets.cs       Toggle, slider, stepper, radio, dropdown, button, search widgets
      CascadeRows.cs          Page / section / row specifications
      CascadeTheme.cs         Role table, binding cache, contrast adjustment and live recolor
      CascadeThemePreset.cs   17-color palette, 14 presets and seasonal date matching
      CascadeSprites.cs       Procedural rounded, pill, ring, outline and shadow sprites
      CascadeIcons.cs         Procedural SDF icons
      CascadeUI.cs            Layout, text, font, gradient and interaction helpers
```

## Adding a page

Pages are declared in `src/Menu/DemoApp.cs`. The fluent API is intentionally small:

```csharp
var example = new CascadePage("Example", "An example page", CascadeIcon.Home);
example.Section("General", "A short section description.")
    .Toggle("Enable feature", "A short description", () => state.GetBool("feature"), value => state.SetBool("feature", value))
    .Slider("Amount", "How much to apply", 0f, 10f,
        () => state.GetFloat("amount", 5f),
        value => state.SetFloat("amount", value),
        value => value.ToString("0.0"))
    .Button("Do something", "Runs a callback", "Run", () => { /* ... */ }, true);

pages.Add(example);
```

Available row builders live in `CascadeRows.cs`. A new row type needs a matching branch in `CascadeWindow.BuildRow`.

The `Mods` page is already wired to real config entries. The UI layer itself still does not require Harmony; gameplay patches live behind `GameplayMods` and `JoystickFlyController`.

## Theming and tokens

`CascadeTheme.cs` is the single source of truth for colors. A theme is built from a 17-color palette (background, panel, header, button, button hover, button pressed, toggle on, toggle off, text, secondary text, accent, border, Active, field, danger, warning and success). Everything else - hover tints, borders, shadows, selection text and the Active indicator - is derived from that palette, so adding a preset never needs a second code path.

The default window is `850x530`, with a `200px` sidebar, `52px` titlebar and `10px` corner radius. `CascadeSprites.cs` generates all shapes, so changing radii or colors does not require new art.

### Presets and the Themes page

Open **Themes** from the sidebar to see every preset as a row with a color swatch. Picking a row applies it immediately and saves it to the config file. Presets are declared once in `CascadeThemePreset.All`; adding a season only requires one entry there.

- **Auto seasonal theme** uses the real date. Holiday ranges are checked before broad seasons, so Valentine's Day wins over Winter and 4th of July wins over Summer. Auto can be turned off at any time.
- **Custom colors** edits the live palette with red/green/blue sliders, a `#RRGGBB` field, quick-pick chips and a preview card. Every change repaints open windows, player cards, cosmetics, popups, tooltips and Active dots in place.
- **Save as Custom** stores the edited palette under the `Custom` theme.
- **Reset to preset** restores the editor to the selected preset; **Reset all** restores the Default theme and palette.
- Low-contrast text is switched automatically and a toast explains the change. This works in both the PC screen-space layout and the VR world-space layout.

A theme change never rebuilds the menu or resets the page. Static graphics are repainted from a cached binding list (`CascadeTheme.RecolorAll`), and the small number of widgets that cache their own state track `CascadeTheme.Version`.

## Mod lifecycle

The Mods page uses two independent states:

- **Enabled** arms the mod. It does not start the effect.
- **Active** means the effect is running. It follows the mod's one switch.

Turning Enabled off clears Active and runs the mod's `OnDisable` cleanup. There is no separate activation bind; the row switch is the only control.

Every mod row is compact: an Active dot, the mod name, a live state line, an Enabled switch and, for mods that declare options, a settings gear. Mod options persist in the config file.

### Settings gear

Only mods that declare settings show a gear. A mod opts in by calling `AddSetting(...)` in its constructor or `EnableSettings()`. The gear opens a focused page containing:

- the live Active state,
- any mod-defined toggles, sliders, steppers or dropdowns.

Changing a value updates the panel in place and keeps its scroll position; only one settings page is open at a time.

The following mods currently expose extra options: **WASD Fly** (speed multiplier), **Tag gun** (cooldown), **Teleport (Gun)** (cooldown), **Spawn Resource (Gun)** (cooldown), **Orbit Ghost (Gun)** (radius, speed and height), **Name Tags** (style preset, name color, size, range, height, opacity, platform line, hide-self and off-screen fade), **Player HUD** (style preset, name color, every card line, size, range, height and opacity), **Talking Players** (position, name color, size, opacity, sensitivity, linger, range, rows, include-self and speaker icon), **Construction** (tool, piece, size, color style, snapping, rotation, static pieces, auto-weld radius, weld range/strength, explosion radius/force, destroy radius, max pieces, grip hand, desktop grip, aim range, projectile speed and cooldown) and **GunLib** (keybind and Active state). ESP, HUD, Gun, Cosmetics, Flight and Acrobatics expose their settings through the existing Settings modules; the GunLib tab links to both.

## Troubleshooting

- **The plugin is not listed in the BepInEx log.** Confirm the game is 64-bit, BepInEx 5 is installed at the game root, and `CascadeMenu.dll` is under `BepInEx\plugins`.
- **The menu loads but text is missing or blurry.** The UI builds its own high-resolution SDF font and keeps the game's localized TMP font as a fallback. The BepInEx log prints the active font and atlas at startup (`[Cascade] UI font: ...`). If a custom font fails to load, Cascade falls back automatically.
- **The menu appears in the wrong place or is hard to read.** Press the green traffic light or **Re-center window** on the Home page. Use **Settings → UI scale** to enlarge the whole window. By default the menu spawns level in front of you and stays anchored in world space; **Settings → Smooth follow** enables optional head following. The default distance is 0.7 m, fixed height is 1.5 m, and the height raise threshold is configurable.
- **The pointer is hard to see.** In VR, aim with the right controller: a colored laser and a white ring with an accent center follow the hit point. On desktop, a custom high-contrast cursor is drawn at the mouse position. Input does not work in VR until the player has spawned and `GorillaTagger.Instance.rightHandTransform` exists.
- **Fly does nothing.** Check that `Joystick fly` is on in the Movement page and that the game is running in VR with tracked controllers. The log prints `Cascade fly is waiting for the local GTPlayer` if the player has not spawned. Use the direction source option and deadzone setting to tune the feel.
- **Hand links still move me.** Confirm `Block hand link` is on and the log contains `Cascade gameplay patches installed`. Toggle the setting once to force the patch state to refresh.
- **A game update broke loading.** Re-copy the current game assemblies into `libs`, rebuild, and reinstall.

## Notes

- Gameplay patches are local-only and can be turned off from the Mods page or the BepInEx config.
- `libs` is included so the project builds without locating the game installation from MSBuild.
- Build outputs live under `bin/Release` and are not committed.

## Contributors

- **foggyie** - maintainer of Cascade Menu.
- **OpenCode** - AI assistance with the theme system, mod lifecycle, settings panels and documentation.
