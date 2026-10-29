# Project notes for Claude

Slay the Spire 2 (**v0.111.0**) mod: every draw opens a "Choose N cards to Draw" screen on the draw pile. Part of
testing whether a seed is unwinnable with perfect information; companion to the seed search tool in
`C:\Users\chook\Desktop\Searching for an Unwinnable Seed` (Necrobinder, Act 1, forced early elite) and to the sibling
mod repo **RngPreview** (expected at `C:\Users\chook\Desktop\RngPreview`). See README.md for the player's view.

The user confirmed in game that the draw screen works. The Enabled tickbox (Settings → Mods and BaseLib's Mod
Configuration) has not been checked in game yet.

## Ground rules

- **Never guess game behaviour; read the decompiled code.** Decompile into the session scratchpad (never commit it):
  `ilspycmd -p -o <scratch>/sts2src "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll"`
  (~1 min). BaseLib: `...\steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll`.
- Install fails with "file locked by SlayTheSpire2.exe" while the game runs. Don't kill the game; ask the user to close
  it and run `! cd "<this repo>" && dotnet build -c Release`.
- The user tests in game; say plainly what hasn't been tried in game.
- Commit only when asked.

## Build / verify

- `dotnet build -c Release` builds and copies `CustomDrawOrder.dll`, `.json` and `CustomDrawOrder.BaseLib.dll` (only
  built if BaseLib.dll exists) into `<game>\mods\CustomDrawOrder\`. `-p:Install=false` skips installing;
  `-p:GameDir=...` / `-p:BaseLibDll=...` override paths. Mods load only at game start.
- `cd tools\PatchCheck && dotnet run -c Release -- ../../bin/Release/net9.0/CustomDrawOrder.dll`: applies the mod's
  Harmony patches to the real sts2.dll without the game, lists every patched method (11 expected) and runs all static
  constructors. Run after any patch change; a missing line means a patch target didn't match. Don't reference the mod
  from that project (.NET would load the stale copy instead of the path given).

## How it works

- Every draw goes through `CardPileCmd.DrawInternal`; `ChosenDraw` replaces it with a faithful copy where only the
  chosen card differs (hand limit, `ShouldDraw`, `ShuffleIfNecessary`, history, `AfterCardDrawn`, `InvokeDrawn` all
  kept). One screen per batch (`k = min(draws left, hand space, free cards)`); no screen when there's no choice.
  Uses `CardSelectCmd.FromCombatPile` with `RequireManualConfirmation` (otherwise it auto-confirms at k). When the
  mod is switched off the prefix returns true and the game's own draw runs.
- `Pins.cs` keeps cards the game put in a fixed place (tracked even while switched off): Innate (turn 1
  `MoveToTopInternal`), `CardPileCmd.Add(..., Top)` (Headbutt, Thinking Ahead, Photon Cut, Glimmer, Cosmic
  Indifference; Shining Strike / Feral / Nostalgia / Rebound via play-result location, which also goes through Add),
  Perfect Fit after reshuffle, Imbued (`ShouldStartAtBottomOfDrawPile`, bottom). Nothing else puts cards at the bottom
  for good (Reboot / Bottled Potential reshuffle immediately). Every other draw-pile placement is random.
- Drawing consumes no RNG. The draw pile order comes from the run-wide `Shuffle` generator (combat-start shuffle,
  reshuffles, random inserts); reshuffles happen at the same times as vanilla, so the mod doesn't shift any generator.
- The draw screen is a player choice inside the turn-start `HookPlayerChoiceContext`, so on turn 1 the game runs
  `AfterSideTurnStart` relics (Crossbow, Big Hat, Orange Dough) at that moment. RngPreview models this by checking
  whether this mod is loaded (mod id `CustomDrawOrder`).

## Mod loading (v0.111.0)

- A mod is `<game>\mods\<Id>\<Id>.json` (manifest) + `<Id>.dll`. `Mod.cs` is the `[ModInitializer]`: load settings,
  `Harmony.PatchAll`, register with BaseLib.
- `ModToggle/` (namespace `ModToggle`, with `ModInfo.cs`) is the Enabled tickbox: settings file, panel in Settings →
  Mods (`NModInfoContainer.Fill/Clear` patches), and `BaseLibBridge`. **The same code is duplicated in the RngPreview
  repo**; keep the two copies in sync when changing it.
- The game calls `GetTypes()` on every mod DLL, so nothing in the main DLL may reference BaseLib. The BaseLib part is a
  separate `CustomDrawOrder.BaseLib.dll` (`BaseLibCompat/`), loaded by `BaseLibBridge` only when BaseLib is loaded
  (pattern from the user's mod `C:\Users\chook\Desktop\STS2-adjust-unknown-chances`). BaseLib titles the entry from
  `<ROOT NAMESPACE OF THE ModConfig CLASS>.mod_title` in `settings_ui`, so the compat code must stay in namespace
  `CustomDrawOrder.*`.
- Settings: `%APPDATA%\SlayTheSpire2\CustomDrawOrder.settings.json` (never under mods/: every .json there is read as a
  manifest).
- Custom UI text without a .pck: merge keys into a loc table at use time (`LocManager.Instance.GetTable(...).MergeWith`).
- Mod UI is built from stock Godot nodes in code (mod assemblies can't register node scripts); the game's tickbox is
  an `NTickbox` with `res://scenes/screens/settings_tickbox.tscn`'s children moved in.
- MSBuild gotchas: XML comments can't contain `--`; `tools/` and `BaseLibCompat/` must stay excluded from the mod's
  compile; the game folder ships .NET runtime DLLs, so PatchCheck uses `UseAppHost=false`.
