# 3d-unity-shell

The structure every Temnorid game follows, taken from the architecture of the Lords app (`lords.temnorid.com`): not its models or look, but how the screens follow each other.

| Lords (Angular) | Unity (this package) |
|---|---|
| `app-splash`: a turning ring while the app finds out who is here | `ShellBoot` (scene `Boot`): splash, profile, update check (a newer Windows release is installed here, before anything else) |
| public pages: landing, sign in | `ShellMenu` (scene `Menu`): landing (Play, Play with others, Profile, Quit), profile |
| `onboarding` (once) | onboarding in `ShellMenu`: a name and a colour, saved in `ShellProfile` |
| the lord's places, with a top bar and a bottom bar of places | `ShellChrome` in the game scene: top bar (game, place, who you are), place tabs (top bar on desktop, bottom bar on phones), a Profile place always last |
| detail pages with a back route | the game's own panels, shown when a place is selected |
| a session shared between devices | `3d-unity-net`: lobby in `ShellMenu`, then everybody loads the game scene |

Scene order in the build: `Boot`, `Menu`, then the game's own scenes. A game scene can still be opened directly (editor, tests): the shell is only ever a screen in front of it.

### What a game provides

1. A `ShellConfig` set before any scene loads (see `Assets/Scripts/HippodromeShell.cs` in `hippodrome.temnorid.com`): title, tagline, `GameId`, GitHub `Repo`, `MaxPlayers`, `GameScene`, accent colour, and its `Places` (two or three, for example Race and Records).
2. A scene builder that calls `ShellSceneKit.BuildBoot()`, `BuildMenu()`, `SetBuildOrder(gameScenePath...)` and `ApplyPlatformSettings(company, product, applicationId, version)`.
3. `ShellChrome.Create(...)` in the game's HUD, a handler for `PlaceSelected`, the HUD kept inside `TopFraction`/`BottomFraction`, and the game's "Main menu" buttons calling `ShellFlow.QuitToMenu()`.
4. On phones, `TouchButton`s for what the keyboard does (see `RaceHud.BuildTouch`), read by the game's driver or controller.
5. Multiplayer through `NetSession` (see `3d-unity-net`): the host is the authority, `NetSession.IsAuthority` is true offline and on the host.

`hippodrome.temnorid.com` is the reference implementation: `RaceNet.cs` (roster to grid slots, remote horses, poses, host-published computer riders, start sync), `OnlineRaceTests.cs` (a host on the real scene and a guest session in the same process).

### Status

v0.1: `ShellBoot`, `ShellMenu`, `ShellChrome`, `ShellProfile`, `ShellProfilePanel`, `ShellFlow`, `ShellConfig`, `ShellUpdates`, `ShellUi`, `SafeAreaFitter`, `TouchButton`, and the editor kit `ShellSceneKit` (shell scenes, build order, Android and iOS settings, Android build, iOS export).

### Roadmap

- Localisation hooks for the Ukrainian games.
- A shared pause menu.
