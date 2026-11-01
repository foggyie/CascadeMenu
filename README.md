# Cascade Menu

A macOS Sequoia-inspired, world-space UI kit for **Gorilla Tag** on **BepInEx 5**. The look and feel are based on the Cascade Luau UI: dark glass, traffic lights, a collapsible 200px sidebar, rounded cards, native-feeling controls, search, dropdowns and toasts.

The **Mods** page now contains real gameplay features: a toggleable hand-link block and a head-relative joystick fly mode. The remaining pages show how to build and wire reusable UI rows.

## Features

- True world-space Canvas for both desktop and VR; no EventSystem required.
- Custom pointer hit-testing for desktop mouse and VR hand rays.
- High-contrast pointer: a laser line plus ring/dot cursor in VR, and a custom screen cursor on desktop.
- Dark glass theme by default, plus a light theme and eight accent colors.
- macOS window chrome, traffic lights, titlebar, collapsible sidebar, back/forward history and sidebar search filtering.
- Reusable declarative pages, sections and rows.
- Toggles, animated sliders, buttons, steppers, radio groups and dropdown overlays.
- Real gameplay mods: block hand-link pickups and double-tap **A** to fly with the left stick.
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
| Toggle joystick fly | Mods page toggle | Double-tap **A** while the menu is closed |
| Fly | - | Left stick, head-relative |
| Close | Escape | B / Y |
| Unfocus search | Escape | B / Y, or click elsewhere |
| Search | Click the field, then type | Click the field with the pointer, then use the keyboard |
| Delete search text | Backspace or the clear button | Clear button |

Traffic lights follow macOS conventions: green re-centers the window, yellow closes it, and red closes it.

## Gameplay mods

- **Block hand link** is enabled by default. It skips `GTPlayer.TakeMyHand_ProcessMovement`, so another player's hand link cannot carry the local player. Toggle it on the Mods page or with `Block Hand Link` in the config.
- **Joystick fly** is off by default. Double-tap **A** with the menu closed, or use the Movement toggle. The left stick controls forward/back and strafe; the right stick's vertical axis controls altitude. Movement aims with the head/camera by default, with an option to use the right controller. Speed, acceleration and deadzone are configurable. Flight input is smoothed on the physics step, and gravity plus old momentum are cleared when flight ends.
- **ESP** options work independently: boxes, names, tracers, through-wall body highlight and a through-wall diamond marker above each player. Highlight and marker color can be chosen in the Visual page.
- **Cosmetic ESP** recognizes the special cosmetic prefixes included in the pasted list and shows role labels above matching players. A separate notification toggle sends the same Cascade-style toast in a small standalone overlay, even while the menu is closed.
- **Disconnect** is in the titlebar. It asks for confirmation unless either VR grip is held while pressing the button.

Both features are local-only. The BepInEx log prints `Cascade gameplay patches installed` when the Harmony patches load.

## Pages

- **Home** - hero, primary/secondary actions, appearance dropdowns and a toggle.
- **Mods** - block hand-link pickups and configure head-relative joystick flight.
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
      CascadeMenuBehaviour.cs Canvas, pointer, input, lifecycle, config rebuilds
      DemoApp.cs              Declarative pages and callbacks
      DemoState.cs            Session-only demo value store
      MenuConfig.cs           BepInEx config entries
    Mods/
      GameplayMods.cs         Harmony installation and lifecycle
      GameplayPatches.cs      Hand-link and fly-gravity patches
      JoystickFlyController.cs Double-tap A, head-relative flight and gravity cache
    UI/
      CascadeWindow.cs        Window chrome, sidebar, titlebar, viewport, toasts, hit testing
      CascadeWidgets.cs       Toggle, slider, stepper, radio, dropdown, button, search widgets
      CascadeRows.cs          Page / section / row specifications
      CascadeSprites.cs       Procedural rounded, pill, ring, outline and shadow sprites
      CascadeIcons.cs         Procedural SDF icons
      CascadeTheme.cs         Dark/light tokens and accents
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

`CascadeTheme.cs` contains all color tokens and accent definitions. The default window is `850x530`, with a `200px` sidebar, `52px` titlebar and `10px` corner radius. `CascadeSprites.cs` generates all shapes, so changing radii or colors does not require new art.

Use the Settings page to change the theme or accent at runtime. Theme changes rebuild the window while keeping the current page.

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
- The project is not a git repository; build outputs are local only.
