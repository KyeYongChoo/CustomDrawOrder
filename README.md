# Custom Draw Order (Slay the Spire 2 mod)

Lets you choose every card you draw, to play a fight out with a perfect draw order. Made for testing whether a seed
is unwinnable: if a fight can't be won even when you pick every draw, no shuffle would have saved it.

Works well with its sibling mod **RNG Preview**, which shows what every random card and potion generator will give.

> Not affiliated with Mega Crit. Built against Slay the Spire 2 **v0.111.0**.

## What it does

Whenever the game would draw cards (the 5-card hand at the start of the turn, or any "draw X cards" effect), a
**"Choose N cards to Draw"** screen opens on your draw pile. Pick the cards and press confirm. They're then drawn
one by one exactly as the game would draw them.

- **It is a real draw, not a played card.** The mod replaces only *which* card the game's draw takes; everything
  else is the game's own draw code. "When drawn" effects, "after you draw" relics and powers, draw prevention, the
  10-card hand limit and the combat history all behave as normal. Nothing is played, generated or exhausted, so
  "whenever you play a Skill" effects never see it.
- **Reshuffles work as in game.** If the draw pile has fewer cards than you need to draw, the rest are taken
  automatically (no screen, since there's no choice). The discard pile is then reshuffled the normal way and a new
  screen asks for the remaining draws.
- **No screen when there's no choice**, e.g. drawing 2 with exactly 2 cards left.
- **Cards the game itself puts in a fixed place keep it:**
  - Innate cards are drawn first on turn 1.
  - Cards put on top of the draw pile by an effect (Headbutt, Thinking Ahead, ...) are drawn next.
  - Perfect Fit's card is drawn first after a reshuffle.
  - Cards enchanted to start at the bottom are only drawn once nothing else is left.

  Everywhere else, where the game's order comes from the shuffle, you choose.
- **No effect on any random generator.** Drawing rolls nothing, and reshuffles happen at the same moments as in the
  normal game, so later rolls (card rewards, random cards, enemy moves) are unchanged.

## Turning it on and off

An **Enabled** tickbox takes effect immediately, even mid-combat (no restart). It's in two places, which change the
same setting:

- **Main Menu → Settings → General → Mod Settings → Custom Draw Order** (always available)
- **Main Menu → Mod Configuration → Custom Draw Order** (only when
  [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127) is installed; BaseLib is optional)

The setting is saved to `%APPDATA%\SlayTheSpire2\CustomDrawOrder.settings.json`.

## Install

Needs the [.NET 9 SDK](https://dotnet.microsoft.com/download). From this folder:

```
dotnet build -c Release
```

The build copies `CustomDrawOrder.dll`, `CustomDrawOrder.BaseLib.dll` and `CustomDrawOrder.json` into the game's
`mods\CustomDrawOrder\` folder. Restart the game afterwards (mods only load at startup); close it first, or the copy
fails because the game has the files open.

- The BaseLib DLL is only built when BaseLib is found at its Workshop path
  (`steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll`; override with `-p:BaseLibDll="..."`). It's a
  separate file because the game loads every type in a mod's DLL at startup, and a BaseLib reference would stop the
  mod loading for players without BaseLib.
- Game installed elsewhere: `-p:GameDir="D:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64"`
- Build without installing: `-p:Install=false`

## Limits

- Only *draws* are chosen. Effects that read the top of the draw pile without drawing (play the top card, look at
  the top cards) still see the order the shuffle produced.
- In v0.111.0 nothing else puts a card at the bottom of the draw pile for good. Reboot and Bottled Potential put
  your hand at the bottom, but reshuffle straight away. If a later version adds such an effect, its card will count as
  freely choosable.
- Singleplayer. In multiplayer every player would need the mod, and that hasn't been tested.
- The game's selection screen doesn't track click order, so the cards in one pick are drawn in an unspecified order.
  This only matters if one of them has a "when drawn" effect that interacts with another.

## Files

- `ChosenDraw.cs`: the replacement for `CardPileCmd.DrawInternal` and the choose screen.
- `Pins.cs`: tracks the cards the game put at a fixed place in the draw pile.
- `ModToggle/`, `ModInfo.cs`, `BaseLibCompat/`: the Enabled tickbox and its two settings pages.
- `tools/PatchCheck/`: checks the mod's patches against the game without starting it.
