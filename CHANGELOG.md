# Changelog

## 0.1.0

- Boot (splash, update), Menu (landing, onboarding, lobby, profile) and in-game chrome (top bar, place tabs), after the Lords app's structure.
- `TouchButton`, `SafeAreaFitter`, `ShellSceneKit` (scenes, build order, Android and iOS settings, builds).

## 0.2.2

- A player signed in with Google skips the "Who is playing?" screen: the name comes from the account.

## 0.3.0

- A welcome screen at first start: "Sign in with Google" or "Continue as a guest", the same in every game, before the landing screen. No Back button; after the choice the player lands on the landing screen. Not asked again once answered or while signed in.
- The separate "Play offline" button is gone: with no connection the guest choice falls back to playing offline this run and asks again next start.
- The sign-in buttons ignore clicks while a sign-in is already running.
- `ShellConfig.Profile` (default true): a game can hide the landing screen's Profile button. The in-game Profile place stays.
- `ShellConfig.SkipLanding` (default false): the first start goes from sign-in straight into the game, for games with their own start scene. Coming back to the menu later still shows the landing screen.
