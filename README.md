# Cascade Menu

A macOS Sequoia-inspired, world-space UI kit for **Gorilla Tag** on **BepInEx 5**. The look and feel are based on the Cascade Luau UI: dark glass, traffic lights, a collapsible 200px sidebar, rounded cards, native-feeling controls, search, dropdowns and toasts.

The module tabs now contain real gameplay features: a toggleable hand-link block, a head-relative joystick fly mode, overlay tools and a construction kit. The remaining pages show how to build and wire reusable UI rows.

## Features

- True world-space Canvas for both desktop and VR; no EventSystem required.
- Custom pointer hit-testing for desktop mouse and VR hand rays, with an authentic system arrow cursor in VR and fast-drag VR ray tracking plus scroll/stick forward-and-backward depth control while holding the titlebar.
- **Two switchable menu styles**: the resizable **Cascade Window (macOS)** UI (with automatic 2-column responsive layout when resized wide or with the sidebar collapsed) and the **Glass Handheld (Wrist & Dot)** vertical wrist/fingertip menu.
- **Shader-like Multi-Orb Window Aura**: animated dual-tone Gaussian radial orb halo behind the menu with customizable **Orb size**, **Orb intensity**, theme-accent matching, quick swatches, and a **Circular RGB Color Wheel**.
- **Custom Background Images & Clear / Frosted / Solid Glass**:
  - Upload any `.png`, `.jpg`, or `.jpeg` wallpaper directly from **Settings → Background**.
  - Clicking **Browse...** launches an asynchronous **Windows PowerShell (`powershell.exe -STA`)** `OpenFileDialog` (Windows File Explorer picker) on a background thread so Gorilla Tag / VR never freezes.
  - Clicking **Open Folder** opens `BepInEx\config\CascadeMenu\Backgrounds\` directly in **Windows File Explorer (`explorer.exe`)** so you can drop images in manually and hit **Refresh**.
  - Images are read straight into memory (`File.ReadAllBytes` → `Texture2D`, never locking files on disk), stretched smoothly to fit the full window with zero cutoff and baked 12px rounded corners, and automatically generate a frosted-glass blurred variant in memory.
  - Choose **Frosted Glass**, **Clear Transparent**, **Solid Opaque**, or slide continuously along **Clear ↔ Solid** (`0% – 100%`) — both the window panels and your uploaded background picture follow the same Clear / Frosted / Solid rule.
- **Sleek Sub-Tab Bar**: responsive **Icon + Name + Glowing Colored Underline Bar** (or **Classic Pills**), customizable under **Settings → Sub-Tabs** with swatches, hex input, and a circular color wheel.
- **Circular HSV/RGB Color Wheel & Live RGB Rainbow Mode**: interactive drag-to-pick circular color wheel + brightness ramp, right-side live target inspector explaining exactly what each of the 17 color roles changes, plus animated **RGB Rainbow Mode** with speed and target selectors.
- **Big Easy-Grab Sliders**: full-width chunky sliders enabled by default for effortless dragging in VR and PC (toggleable under **Settings → Appearance**).
- **Auto-Save & Fast Deferred Startup**: automatically saves menu, theme, and mod states on changes, close, and quit, with batched startup config binding and staggered post-world mod restoration so saved mods activate cleanly without startup lag.
- **Body Estimation**, **Chams (through-wall + opacity slider)**, **NameBadges (All-in-One Nametags)**, **PropForge (All-in-One Construction & Sandbox with solid locomotion colliders)**, **LyricWave**, **Personal Space**, **Safe Quit Box**, and organized Harmony patches under `src/Patches/`.
- Dark glass theme by default, plus a light theme and a live seasonal theme system (Default, Halloween, Thanksgiving, Christmas, New Year, Valentine's, St. Patrick's, Easter, 4th of July, Summer, Fall, Winter, Dark and Light).
- Mods are one switch: turning one on starts its effect immediately; turning it off stops it and runs cleanup.
- **Module tabs**: every mod category has its own sidebar page - **Overlays**, **Movement**, **Visual**, **Fun**, **Snake**, **Tools**, **Flight**, **Desktop**, **Performance**, **Super**, **Game** and **Player** - so mods are grouped by purpose instead of sharing one long list.
- **Over a hundred extra one-switch mods** fill the new pages. Overlays: coordinates, altitude, attitude, velocity, hand speed, distance walked, jump counter, room info, session clock, frame time, memory, active mods, keybinds, target info, room props, water state, ground state, input monitor, time scale and build stamp. Movement: gravity scale, weightless, heavy gravity, moon/super jump, jump speed, no slide, ice skates, long arms, tiny/giant body, slow motion, bullet time, bouncy feet, air dash, rocket jump, auto walk and speed cap. Visual: FOV boost/narrow, extended view, vignette, letterbox, scanlines, film grain, alignment grid, safe zone, night vision, fullbright, fog off/density, long shadows, plus chams, tracers, boxes, markers and cosmetic tags. Fun: motion/hand/rainbow trails, laser sight, orbiting orbs, party lights, fireflies, snowfall, rainfall, confetti, fireworks, ground ring, beacon light and breadcrumbs. Snake: auto play with a risky fallback, auto restart, instant win, open/exit, a live status card, speed control, invincibility, path visualizer, set best score, pellet control, a stats tracker and a best-score notifier. Tools: ruler, look distance, object info, beacon, clipboard helpers for room code/player list/local id/position/room props, mod search, free cursor and fullscreen. Flight: jump jet, feather fall, rocket boots and glide. Desktop: lock/show cursor, big/compact menu and menu near. Performance: shadow off, short shadows, 60/72/90 FPS caps, player culling, LOD bias, texture quality, AA off and pixel lights. Game: size changer max/min, force player giant, size changer info, teleport to size changer and list size changers. Player: change name, join room code, set custom property, quick chat, teleport gun, teleport to nearest/random player, cycle players and follow nearest. Every one of them is a single row with an immediate start/stop and a gear only when it has options.
- **Crosshair**, **Clock**, **Session Stats** and **Screenshot**: camera-anchored overlays for a centre dot/cross/ring, a corner clock, live FPS/room/players/uptime, and a one-shot PNG capture saved to `CascadeScreenshots`.
- **Compass**, **Speedometer**, **FPS Graph** and **Screen Tint**: more visual overlays - a sliding heading strip, local movement speed with a fill bar, sixty scrolling frame-rate bars, and a full-view colour filter.
- **Joystick Fly**, **Noclip** and **Acrobatics** are one-switch mod rows backed by the existing movement controllers; **Unlimited FPS** removes VSync and raises the frame cap, restoring the previous values when switched off.
- **Name Tags**: lightweight, display-only name labels above every remote gorilla rig, with an optional platform line, a fixed custom size, five appearance presets and off-screen fade. Separate from the full Player HUD so either can run alone.
- **Player HUD**: a display-only, themed information card above every other player. Name, rig color swatch, RGB/hex, player ID, Photon custom properties, tagged state, platform, ping, FPS, distance and cosmetic count. Cards are pooled per player and updated a few times a second, with no raycast targets, so nothing on them can be clicked. Name Tags and Player HUD both appear as simple toggle rows with gear buttons on the **Visual** page; each gear page has Classic, VRChat, Fortnite, Minimal and Glass appearance presets plus a name-color mode.
- **Talking Players**: a small, camera-anchored speaker list on the right of the view that shows only the players who are speaking, each with a speaker glyph and a name chip tinted with that player's gorilla color. There is no backing panel behind the chips and the rows are packed tight, so the list stays a thin strip at the screen edge. Sensitivity, linger time, row count, position, size, opacity, distance and text color are settings; the mod is a single toggle on the **Visual** page and costs nothing while off.
- **GunLib tab**: grip-gated targeting with Left/Right/Either grip hands, a selectable beam hand, a desktop mouse/key stand-in, configurable beam/ring colors and sizes, max distance, and a raycast that stops on everything solid - world geometry, props and construction pieces - while ignoring triggers and overlay beams.
- **Construction**: a local-only building kit. The **Block** piece is a full cube that snaps into its own world-aligned cell, so floors, walls and towers tile with no gaps and no half-block overlaps - the rest of the kit (bricks, planks, beams, logs and balls) snaps to a 0.5 m grid, rotates in 45-degree steps, undoes and clears builds, and is demolished with a projectile-firing demolition gun or a direct explosion. Every piece carries its own procedural block texture - staggered brickwork, nailed boards, ribbed steel, bark and a stitched ball - tinted by the chosen color style, so builds look like real materials instead of flat plastic. Pieces are solid blocks that stay exactly where they were placed - walls, floors and stairs behave like a real build instead of a pile of loose props - and an explosion releases them before throwing them. Dynamic sliding is still available by turning **Static pieces** off, and **Textures** switches back to flat colors.
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
- Game and BepInEx assemblies, supplied either by `GORILLA_TAG_DIR` or by a local `libs/` folder. No Unity editor is required.

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

### Build references

Game DLLs are not committed. The project finds them in one of two ways:

1. Set `GORILLA_TAG_DIR` to the Gorilla Tag install folder before building:

   ```powershell
   $env:GORILLA_TAG_DIR = 'C:\Path\To\Gorilla Tag'
   dotnet build -c Release
   ```

   The project then references `$(GORILLA_TAG_DIR)\Gorilla Tag_Data\Managed` for the game assemblies and `$(GORILLA_TAG_DIR)\BepInEx\core` for `BepInEx.dll` and `0Harmony.dll`.

2. Leave `GORILLA_TAG_DIR` unset and copy the same DLLs into `libs/`:

   - From `Gorilla Tag_Data\Managed`: `Assembly-CSharp.dll`, `Unity.TextMeshPro.dll`, `Unity.InputSystem.dll` and the `UnityEngine*.dll` files the project references.
   - From `BepInEx\core`: `BepInEx.dll` and `0Harmony.dll`.

   `libs/` is ignored by git, so this copy is local to your machine.

After a game update, refresh the references from the current install (or point `GORILLA_TAG_DIR` at it) and rebuild. Do not copy game DLLs into `bin\Release`; they are marked `Private=false` and must be supplied by the game.

The current references were refreshed from the 13 November 2026 game build. The older references predated the computer Snake game, so the Snake page needs these refreshed files.

## Install

1. Build the plugin.
2. Copy `bin\Release\CascadeMenu.dll` into:

   ```text
   <Gorilla Tag>\BepInEx\plugins
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
| Open / close | F1, Insert, Tab | B / Y, Joystick Press (Stick Click), or A / X (configurable in Controls) |
| Point | Mouse | Right-hand ray + VR system arrow cursor |
| Select | Left click | Right trigger or A |
| Move menu & depth | Drag titlebar + mouse wheel (scale / forward-back) | Hold titlebar + move hand freely; right stick up/down pushes forward/back |
| Resize window | Drag bottom-right corner grip (auto 2-column layout when wide) | Drag bottom-right corner grip |
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
- **Construction** is local-only. Enable it on the **Fun** page (or the Mods list), hold the chosen grip, and pull the trigger. Build mode places the selected piece on the first visible surface. **Block** is the Minecraft-style piece: it fills the whole cube cell the aim ray enters, so blocks placed side by side, on top of each other or against a wall meet edge to edge, and it always stays upright, rotating only in quarter turns instead of tilting to the surface. The other pieces snap to the 0.5 m grid by default. **A/X** or **R** rotates 45 degrees, **Q** cycles the piece, **Z** undoes the newest piece and **Delete** clears the build. With **Static pieces** on, each placed piece is kinematic: it stays where it was put, but it is still a solid collider, so players can stand on it, climb it and bump into it. Explosions unfreeze the affected pieces before applying force, so demolition still throws the build apart. Auto-weld only runs in dynamic mode; the Weld tool selects one piece with the trigger, then welds it to the next aimed piece. The demolition gun fires a physics round that explodes on contact, while Explosion mode detonates directly at the aim point. Pieces, previews, projectiles, welds and blast visuals all live under one local root and are destroyed when the mod stops.
- **Name Tags** and **Player HUD** both live on the **Visual** page as simple toggle rows, with a gear for their options. Toggling one on starts the overlay immediately. Name Tags draws only a themed name plate, optionally with the player's platform; Player HUD draws the full card. Their gear pages include appearance presets (**Classic**, **VRChat**, **Fortnite**, **Minimal**, **Glass**) and a name-color mode (style default, theme, white or rig color). Off-screen and distant objects fade out, while labels and cards keep a fixed size set by their Size slider.
- **Talking Players** also sits on the **Visual** page as one toggle. Its gear page sets which screen edge the compact list hangs from (right center is the default), size, opacity, sensitivity, linger time, maximum rows, showing your own mic and the speaker icon. The row color is always the speaker's gorilla color; the text can be automatic (dark on light, white on dark), plain white, dark, or the rig color itself on a dark chip.
- **Disconnect** is in the titlebar. It asks for confirmation unless either VR grip is held while pressing the button.

The menu itself, the Harmony patches and the visual overlays run only on your client: ESP, chams, name tags, Player HUD, Talking Players, overlay cards, Construction, trails, lasers, particles, the movement/flight mods, performance switches, desktop helpers and the Snake tools never send anything to other players. Normal game replication still shows other players your gorilla moving, flying or teleporting while those mods are active.

A few rows call the game's own networking APIs and therefore do affect the room or other players:

- **Report** on a player page submits through Gorilla Tag's own report API, so it reaches the server exactly like a scoreboard report.
- **Spawn Resource (Gun)** only works when the local client has Super Infection authority, and the resources it creates are networked entities other players receive.
- Player navigation tools (select, teleport gun/nearest/random/cycle, follow nearest, mute) act on your own rig or local player list. Mutes and selections stay on your machine, but your movement is replicated because it is your normal gorilla position.

The BepInEx log prints `Cascade gameplay patches installed` when the Harmony patches load.

## Pages

- **Home** - hero, primary/secondary actions, appearance dropdowns and a toggle.
- **Mods** - every discovered mod as a compact Enabled/Active row, with a settings gear only on mods that declare options.
- **Overlays** - twenty read-only information cards: position, altitude, attitude, velocity, hand speed, distance, jumps, room, session clock, frame time, memory, active mods, keybinds, target, room props, water/ground state, input, time scale and build stamp.
- **Movement** - gravity, jump, friction, body-scale and time-scale mods, plus dash/rocket impulses and an auto walk.
- **Visual** - ESP, HUD, target markers, camera/FOV changes, full-view filters (vignette, letterbox, scanlines, grain, grid, night vision), lighting and fog controls.
- **Fun** - cosmetic trails, laser sight, orbiting/party lights, fireflies, snow, rain, confetti, fireworks and ground markers, alongside the existing lobby tools and the Construction toggle, undo, clear and its settings gear.
- **Snake** - plays the computer's Snake minigame: BFS auto play with a risky fallback, auto restart, instant win, open/exit, a live state/score/best card, speed control, invincibility, a path visualizer, set best score, pellet-to-snake, a stats tracker and a best-score notifier.
- **Tools** - ruler, look distance, object inspector, world beacon, clipboard helpers, mod search, cursor and fullscreen controls.
- **Flight** - jump jet, feather fall, rocket boots and glide.
- **Desktop** - cursor lock/visibility and menu size/distance presets.
- **Performance** - shadow, frame cap, culling, LOD, texture, anti-aliasing and pixel-light switches.
- **Game** - size changer max/min, force player giant, size changer info, teleport to size changer and list size changers.
- **Player** - change name, join room code, set a custom property, quick chat, teleport gun, teleport to nearest/random player, cycle players and follow nearest.
- **GunLib** - state, grip gating, beam/ring appearance and the gun's settings links.
- **Profiler** - live Name Tags, Player HUD, Talking Players, GunLib and Construction frame cost.
- **Controls** - input, pointer and notification demonstrations.
- **Settings** - organized into clean sub-tabs (**General**, **Appearance**, **Background**, **Orb Glow**, **Sub-Tabs**, **Modules**, and **HUD Mods**) covering Auto-Save, placement, UI scale, Big Sliders, RGB Rainbow Mode, PowerShell/Explorer PNG/JPG background uploads with Clear/Frosted/Solid blending, multi-orb glow size/intensity/color, and sub-tab bar customization.
- **About** - credits, links and a restore-defaults action.

### Custom Menu Backgrounds (PowerShell & File Explorer)

Cascade Menu supports custom `.png`, `.jpg`, and `.jpeg` menu backgrounds stored under:

```text
<Gorilla Tag>\BepInEx\config\CascadeMenu\Backgrounds\
```

- **Browse... (PowerShell Picker)**: Opens a native Windows `OpenFileDialog` via `powershell.exe -NoProfile -NonInteractive -STA -WindowStyle Hidden` on a background thread so Gorilla Tag and VR never stall while choosing an image. Picked files are copied into `BepInEx\config\CascadeMenu\Backgrounds\` and loaded straight into memory (`File.ReadAllBytes` → `Texture2D`) without locking the file on disk.
- **Open Folder (Windows Explorer)**: Launches `explorer.exe` directly to `BepInEx\config\CascadeMenu\Backgrounds\` so you can drag-and-drop images manually and click **Refresh**.
- **Stretch-to-Fit & Surface Matching**: Images smoothly stretch to fit the full window with zero cutoff and baked 12px rounded corners, and automatically follow your **Surface preset** (`Frosted Glass`, `Clear Transparent`, `Solid Opaque`) and **Clear ↔ Solid** slider (including an in-memory frosted-glass blurred texture).

Use the sidebar search field to filter rows by their title, subtitle or search text. Search filtering is applied live and content scrolls back to the top when the results change.

## Project layout

```text
CascadeMenu/
  CascadeMenu.csproj
  libs/                       Optional local game and BepInEx references (ignored by git)
  src/
    Plugin.cs                 BepInEx entry point
    PluginInfo.cs
    Menu/
      CascadeMenuBehaviour.cs Canvas, pointer, fast VR drag, input, lifecycle, RGB & auto-save
      CascadeBackgroundManager.cs PowerShell OpenFileDialog picker, Explorer folder & in-memory PNG/JPG loader
      MenuApp.cs              Declarative pages and organized Settings sub-tabs
      MenuState.cs            Session-only menu value store
      MenuConfig.cs           BepInEx config entries (batched startup bind)
      ThemesPage.cs           Preset list, Auto toggle, Circular Color Wheel, RGB & target preview
      GunLibPage.cs           State, grip, beam/ring and settings links for the gun
      ProfilerPage.cs         Live per-frame cost view for the heavy features
      ProfilerStats.cs        Shared allocation-free frame-cost registry
      ModuleSettings.cs       Settings pages for the built-in modules
    Patches/
      PatchManager.cs         Central Harmony patch installer/uninstaller
      GameplayPatches.cs      Hand-link and fly-gravity patches
      LocalRigGuardPatches.cs Local rig safety guards
      PlatformDeployerPatch.cs Platform deployer hook
      QuitBoxSafetyPatch.cs   Safe Quit Box protection
      GTPlayerStatsSpoofPatch.cs Optional client-side stat overrides
      SetupVoicePatch.cs      Voice setup hook
    Mods/
      Core/
        Mod.cs                One-switch lifecycle and settings declarations
        ModRegistry.cs        Discovery, persistence, generated rows and gear pages
        ModSetting.cs         Toggle/slider/stepper/dropdown/text model for mod options
        ModKit.cs             Shared player/camera access, materials, sprites, billboard clouds
        ModHud.cs             Corner information card and the read-only overlay base class
      Game/
        GameplayMods.cs       Harmony installation and lifecycle
        GameplayPatches.cs    Hand-link and fly-gravity patches
      Movement/
        JoystickFlyController.cs Menu-switch flight, stick mapping and gravity cache
        AcrobaticsController.cs Body-rotation mode
        NoclipController.cs   Collision bypass
        MovementMods.cs       Gravity, jump, friction, scale, time and impulse mods
      Flight/
        WASDFly.cs            Desktop WASD free-flight
        JoystickFly.cs        One-switch wrapper for the stick flight controller
        FlightMods.cs         Jump jet, feather fall, rocket boots and glide
      Visual/
        VisualMods.cs         FOV, full-view filters, lighting and ESP wrapper mods
      Fun/
        FunMods.cs            Trails, particles, weather and ground markers
      Snake/
        SnakeGame.cs          Cached reflection bridge and the BFS/survival AI
        SnakeMods.cs          Auto play/restart, instant win, controls and status card
      Desktop/
        DesktopMods.cs        Cursor and menu size/distance mods
      Tools/
        GunLibController.cs   Grip-gated aim, solid-surface raycast, beam and ring
        GunLibMod.cs          Thin Mod wrapper exposing the gun's Enabled/Active state
        Construction.cs       Physics pieces, welds, demolition gun and explosions
        Screenshot.cs         One-shot PNG capture into CascadeScreenshots
        UtilityMods.cs        Ruler, inspector, beacon, clipboard and window mods
      Overlays/
        OverlayStyles.cs      Classic, VRChat, Fortnite, Minimal and Glass appearance presets
        NameTags.cs           Pooled display-only name plates above remote rigs
        PlayerHud.cs          Pooled display-only player cards above every player
        VoiceIndicator.cs     Talking-players strip with player-colored name chips
        Crosshair.cs          Centre-of-view dot, cross or ring
        Clock.cs              Corner clock with optional date
        SessionStats.cs       FPS, room, players and uptime card
        Compass.cs            Sliding heading strip
        Speedometer.cs        Local movement speed and fill bar
        FpsGraph.cs           Scrolling frame-rate bars
        ScreenTint.cs         Full-view colour filter
        HudOverlay.cs         Local status overlay
        InfoOverlays.cs       Twenty read-only telemetry, room and player cards
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
        UnlimitedFps.cs       VSync/frame-cap override
      Super/
        SuperGuns.cs          Super-power guns
        Noclip.cs             One-switch wrapper for the noclip controller
        Acrobatics.cs         One-switch wrapper for body rotation
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

Pages are declared in `src/Menu/MenuApp.cs`. The fluent API is intentionally small:

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

The module pages are already wired to real config entries. The UI layer itself still does not require Harmony; gameplay patches live behind `GameplayMods` and `JoystickFlyController`.

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

A theme change never rebuilds the menu or resets the page. Static graphics are repainted from a cached binding list (`CascadeTheme.RecolorAll`), and the small number of widgets that cache their own state track `CascadeTheme.Version`. Color swatches keep their border on a child image instead of stacking a second `Graphic` on the chip, so changing a theme cannot break the Themes page or stop the menu from opening.

## Mod lifecycle

Each module tab (Overlays, Tools, Flight, Desktop, Performance and Super) uses two independent states:

- **Enabled** arms the mod. It does not start the effect.
- **Active** means the effect is running. It follows the mod's one switch.

Turning Enabled off clears Active and runs the mod's `OnDisable` cleanup. There is no separate activation bind; the row switch is the only control.

Every mod row is compact: an Active dot, the mod name, a live state line, an Enabled switch and, for mods that declare options, a settings gear. Mod options persist in the config file.

### Settings gear

Only mods that declare settings show a gear. A mod opts in by calling `AddSetting(...)` in its constructor or `EnableSettings()`. The gear opens a focused page containing:

- the live Active state,
- any mod-defined toggles, sliders, steppers or dropdowns.

Changing a value updates the panel in place and keeps its scroll position; only one settings page is open at a time.

The following mods currently expose extra options: **WASD Fly** (speed multiplier), **Teleport (Gun)** (cooldown), **Spawn Resource (Gun)** (cooldown), **Orbit Ghost (Gun)** (radius, speed and height), **Name Tags** (style preset, name color, size, range, height, opacity, platform line, hide-self and off-screen fade), **Player HUD** (style preset, name color, every card line, size, range, height and opacity), **Talking Players** (position, name color, size, opacity, sensitivity, linger, range, rows, include-self and speaker icon), **Crosshair** (style, color and size), **Clock** (position, 24-hour time, seconds, date and size), **Session Stats** (FPS, players, room, uptime, size and opacity), **Screenshot** (supersize), **Compass** (degrees, size and opacity), **Speedometer** (units, maximum, size and opacity), **FPS Graph** (position, scale, size and opacity), **Screen Tint** (color and opacity), **Joystick Fly** (speed, vertical speed, acceleration and aim), **Acrobatics** (smoothing), **Unlimited FPS** (target cap), **Construction** (tool, piece, size, color style, textures, snapping, rotation, static pieces, auto-weld radius, weld range/strength, explosion radius/force, destroy radius, max pieces, grip hand, desktop grip, aim range, projectile speed and cooldown) and **GunLib** (keybind and Active state). ESP, HUD, Gun, Cosmetics, Flight and Acrobatics expose their settings through the existing Settings modules; the GunLib tab links to both.

## Troubleshooting

- **The plugin is not listed in the BepInEx log.** Confirm the game is 64-bit, BepInEx 5 is installed at the game root, and `CascadeMenu.dll` is under `BepInEx\plugins`.
- **The menu loads but text is missing or blurry.** The UI builds its own high-resolution SDF font and keeps the game's localized TMP font as a fallback. The BepInEx log prints the active font and atlas at startup (`[Cascade] UI font: ...`). If a custom font fails to load, Cascade falls back automatically.
- **The menu appears in the wrong place or is hard to read.** Press the green traffic light or **Re-center window** on the Home page. Use **Settings → UI scale** to enlarge the whole window. By default the menu spawns level in front of you and stays anchored in world space; **Settings → Smooth follow** enables optional head following. The default distance is 0.7 m, fixed height is 1.5 m, and the height raise threshold is configurable.
- **The pointer is hard to see.** In VR, aim with the right controller: a colored line runs from your hand to the hit point. On desktop, the normal system mouse pointer is used. Input does not work in VR until the player has spawned and `GorillaTagger.Instance.rightHandTransform` exists.
- **Fly does nothing.** Check that `Joystick fly` is on in the Movement page and that the game is running in VR with tracked controllers. The log prints `Cascade fly is waiting for the local GTPlayer` if the player has not spawned. Use the direction source option and deadzone setting to tune the feel.
- **Hand links still move me.** Confirm `Block hand link` is on and the log contains `Cascade gameplay patches installed`. Toggle the setting once to force the patch state to refresh.
- **A game update broke loading.** Refresh the game references (set `GORILLA_TAG_DIR`, or re-copy them into `libs`), rebuild, and reinstall.

## Notes

- Gameplay patches are local-only and can be turned off from their module tabs or the BepInEx config.
- `libs/` is optional and ignored by git, so the project can build without locating the game installation from MSBuild. Set `GORILLA_TAG_DIR` instead if you prefer.
- Build outputs live under `bin/Release` and are not committed.

## Contributors

- **foggyie** - maintainer of Cascade Menu.
- **OpenCode** - AI assistance with the theme system, mod lifecycle, settings panels and documentation.
