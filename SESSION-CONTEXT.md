# Session context export — Capture The Flag

Exported 2026-08-05. Companion to `HANDOFF.md`, which holds the durable architecture notes.
This file is the record of *this* session: what changed, why, and what is still open.

---

## 1. The magenta screen (solved)

The app built and launched but rendered **solid magenta**. Cause: `UI/Default` was missing from
the build. The engine resolves that shader **by name at runtime**, so nothing in the project marks
it as used and the build strips it — while `Sprites/Default` survives because real sprite assets
reference it. The entire menu is Canvas UI, so everything fell back to the error shader.

Diagnosed by grepping the built `Data/` folder for shader names rather than guessing:

```
UI/Default        MISSING
Sprites/Default   PRESENT
```

Fix: `Project Settings > Graphics > Always Included Shaders` (empty since the initial commit) now
lists `UI/Default`, `UI/Default Font`, `UI/DefaultETC1`, `Sprites/Default`, `Sprites/Mask`,
`Legacy Shaders/Diffuse`.

**The fix only lands in a fresh Unity export.** Rebuilding in Xcode alone changes nothing.

## 2. Disk exhaustion (the session's biggest time sink)

The boot volume repeatedly hit ~0 bytes free. Every failure it caused looked like something else:

| Symptom | Real cause |
|---|---|
| "The file could not be unlocked" | no space for atomic saves |
| `IOException: Directory not empty` in `iOSBuildPostprocessor` | Finder recreating `.DS_Store` mid-delete |
| App Store: "Invalid package. The uploaded package is corrupt" | `ditto` failing to copy `UnityFramework`, `global-metadata.dat`, `resources.assets` into the .ipa |

Xcode logs actual free space at the top of `ContentDelivery.log` — check it first. Archive + upload
stages the .ipa in `/var/folders` on the **boot** disk, so relocating DerivedData does not help.
Keep 20 GB free.

`rm -rf` failing with "Directory not empty" means Finder is recreating `.DS_Store` faster than the
delete walks the tree. Close the Finder window, or delete the `.DS_Store` files and the folder in a
single command.

## 3. Gameplay and flow

- **Bots spawn instantly.** `botFillDelay` 30 → 0 in the code default *and* the `fsfs.unity`
  serialized value. `BotFillRoutine` always yields one frame first, or bots claim spawn points
  before the host's own player exists.
  Trade-off: a human who matchmakes a moment later joins an already-full game.
- **Matchmaking falls back to bots after 15s.** Every online failure — service init, matchmaking
  fault, lobby failure, or a network that hangs without faulting — starts a local host match
  instead of showing a dead end. `MatchmakingService` is added at runtime by `NetworkBootstrap`,
  so the code default of 15 genuinely applies; there is no serialized override.
- **Root cause of "can't connect to lobby":** a previous attempt left `NetworkManager` still
  listening, so `StartHost()` refused with *"Can't start while listening"* and that surfaced as a
  lobby error. It now shuts down cleanly before hosting.

## 4. UI

- **Palette moved to Supercell blue/gold.** `GameUI.cs` holds the tokens; because `GameUIBuilder`
  is an *Editor* script, colours were baked into the scenes, so a remap pass was needed —
  58 graphics in MainMenu, 39 in the arena, alpha preserved. Lobby and LoadingScreen took **zero**
  hits, confirming they were never built through the design system.
- **`MenuResponsiveLayout.cs`** swaps between landscape and portrait presets rather than stretching
  one. Two things made portrait impossible to fix by scaling: `TopBar` is a fixed 1920 wide, and
  `PlayWrap` (560) + `IconRail` (400) + margins need ~1072 of the 1080 available.
  Deliberately **not** `[ExecuteAlways]` — in edit mode it would capture whatever the Game view
  aspect happened to be and serialize portrait overrides as the authored layout.
- **`MenuFigma.unity` is now the live menu** (Build Settings scene 0). Built from the Raid Rush
  Figma kit; components exported as 4× PNGs into `Assets/Generated/UI/` because the source assets
  are SVG and Unity cannot import those without the Vector Graphics package.
  **Those button exports have their labels baked into the artwork** — PLAY / PLAYER / SHOP cannot
  be re-labelled or localised without re-exporting.
- All "back to menu" paths route through one constant, `SceneLoader.MainMenu`, so swapping the menu
  scene is a one-line change. The old `MainMenu.unity` is kept in the project but excluded from the
  build.
- **Shop card reflowed to 1000×1500 portrait.** At the authored 1500 wide, the close button sat
  past the right edge of a 1080-wide screen — the panel could be opened but never dismissed.

## 5. Monetisation

- **Remove Ads** is a real non-consumable (`remove_ads`). Only interstitials are suppressed;
  rewarded stays live because it is opt-in and a payer still wants the coins.
- Every path that grants the entitlement requires a real store receipt — no debug flag, no
  inspector default, no free grant. The old stub that handed out gems free is gone.
- The entitlement is **never revoked** when the store reports no receipt: an offline launch must not
  strip a purchase. Cost: a refund won't restore interstitials until PlayerPrefs are cleared.
- Price comes from `metadata.localizedPriceString`, not a hardcoded `$1.99`.
- `RestorePurchases` is wired and visible. **Apple rejects any app selling a non-consumable without
  a visible restore control.**

## 6. Onboarding

`TutorialController.cs` on `fsfs`. Four steps, each gated on the player actually performing the
action: move → dash → grab the enemy flag → score. Builds its overlay in code so it cannot drift
from the HUD it points at. It **never blocks raycasts** — every step needs the real controls, so an
input-eating overlay would soft-lock first launch. Gate key `tutorial_completed_v1`;
`TutorialController.ResetProgress()` re-arms it.

## 7. Traps hit this session

1. **A serialized scene value silently beats the code default.** `botFillDelay` was 30 in
   `fsfs.unity`; editing the field initialiser did nothing.
2. **A Canvas without an EventSystem receives no input at all.** Worse: the check for one passed
   because another scene was open *additively* at the time and supplied it.
3. **Panel controllers never call `onClick.AddListener`** — every button is scene-wired, so
   assigning a serialized field is not enough; add a persistent listener via `UnityEventTools`.
4. **Cross-scene object copies break internal references** unless the objects are parented under a
   single temp root before instantiating, so Unity remaps them inside the copy.
5. **`com.unity.services.multiplayer` cannot coexist** with the Lobbies/Relay packages — it
   re-declares `Lobby`, `Allocation` and `JoinAllocation` in the *same namespaces*, so every
   reference becomes ambiguous (CS0433) and no `using` alias can resolve it. The Multiplayer Center
   wizard offers to install it. Decline.
6. **Multiplayer Center ≠ Multiplayer Play Mode.** The former is a setup wizard; the latter
   (`com.unity.multiplayer.playmode`) is the virtual-players feature.
7. **Reflection is blocked in Unity RunCommand.** Use `SerializedObject`/`SerializedProperty`,
   which also shows what is actually wired.
8. **Scene operations throw during Play mode.** Stop play before any editor scene edit.

## 8. Still open

- **Audio — completely untouched.** Zero clips. Biggest remaining gap in game feel.
- **Lobby screen** never restyled; still on the old flat look and now inconsistent with everything
  else.
- **1024×1024 App Store icon** missing — uploads fail with error 91111. Set it in Player Settings →
  iOS → Icon, not Xcode, or the next Unity export overwrites it.
- **App Store Connect:** create `remove_ads` (Non-Consumable, Tier 2 / $1.99 — there is no $2.00
  tier) and the gem consumables. Products must reach "Ready to Submit" or the shop shows
  "Unavailable" instead of a price. The listing name is currently the typo **"Captue the flag"**.
- `PlayerButton` in the new menu has no handler — `MainMenuController` has no profile method.
- Four remaining skins are placeholder blobs. Generate the **red** variant only; the hue-swap
  script derives blue in seconds.
- `brew install cocoapods` before attempting an iOS build.

## 9. Verifying UI work

You cannot screenshot a screen-space canvas. To see any menu: set the Canvas to `WorldSpace`,
`sizeDelta` 1920×1080 (or 1080×1920 portrait), `localScale` 0.01, position zero; capture the region;
then restore `ScreenSpaceOverlay`, scale 1, and save.

Every real UI bug this session was invisible in the inspector and obvious in a capture — the
instruction text colliding with the score HUD, the enlarged coin icon covering its own value, the
shop's unreachable close button.
