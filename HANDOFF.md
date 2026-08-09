# Capture The Flag — Handoff

Mobile (iOS) top-down multiplayer CTF in Unity. Written for whoever picks this up next,
so they don't re-derive what's already known or repeat traps that have already been hit.

Last updated: 2026-08-04

---

## Scenes & flow

`MainMenu` (0) → `LoadingScreen` (1) → `Lobby` (2) → `fsfs` (3) — all in Build Settings, MainMenu is the entry scene.

`fsfs.unity` is the match arena. Bounds are **x −25.5..25.5, y −10.5..10.5**. Midline x=0 divides team territory.

## Networking

Unity Netcode + **real Unity Gaming Services** (Lobby / Relay / Authentication). Cloud Project ID is
configured and matchmaking genuinely reaches "Searching for match".

- `Resources/NetworkBootstrap.prefab` holds the persistent `NetworkManager` + `UnityTransport` +
  `MatchmakingService`. **It must exist** — without it `MatchmakingService.Instance` is null and Play
  does nothing. `MainMenuController.EnsureServicesExist()` instantiates it.
- `fsfs` has **no** NetworkManager of its own. Don't add one back; two managers conflict.

## Gameplay

- **Scoring**: carry the enemy flag to your base while your own flag is home.
- **Tagging**: proximity-based (radius 1.25), resolved once per frame in `NetworkGameManager.FixedUpdate`.
  Classic CTF territory rule — you're safe on your own half, vulnerable on the enemy's, so "who loses"
  is never ambiguous. 1.75s spawn protection with a blink tell.
- **Dash**: 2.8× speed for 0.26s, 6s cooldown, server-authoritative. Space on desktop, HUD button on mobile.
  Bots use the same ability on the same cooldown.
- **Match timer**: 120s. Highest score wins; equal scores are a genuine draw (`EndMatch(null)`).
- **Bots**: fill after 30s if no second player. Roles Attacker/Defender/Interceptor, weighted toward
  offence. Steering-based with obstacle avoidance — not pathfinding.
- **Bushes**: walk-through concealment. Crates block movement. Hidden enemies are invisible; teammates
  fade to 45%; **flag carriers are always revealed** (otherwise camping the flag in a bush is unbeatable).

## Art

- Characters: `Assets/animation-pack/` — knight, 6×6 grid = 36 frames per sheet (idle, walk, tag, bow),
  bottom-centre pivots, PPU 340 → **1.5 world units**, ≈13% of screen height.
- Blue team is generated from red by **hue-isolated channel swap** (`R<->B` on saturated reds only).
  Script pattern is in the git history — it takes seconds to regenerate for any new skin, so
  **only ever generate the red variant by hand**.
- Two animator controllers: `Knight.controller` / `Knight_Blue.controller`. `TeamMember` swaps the whole
  controller by team rather than tinting, because the sash colour is baked in.
- Only the **knight** is animated. Ninja/Ranger/Medic/Commander fall back to `Resources/ctf_spritesheet`
  (`ctf_char_0..4`, tinted). The 10 files in `Resources/Skins/` are **32px placeholder blobs** — not usable.

## UI

Design system is code, not a document:
- `Assets/Scripts/GameUI.cs` — palette, type scale, metrics. Every colour derives from an in-game asset.
- `Assets/Editor/GameUIBuilder.cs` — component factory (panel, button, chip, coin, scrim).
- `Assets/Resources/UI/ui_*.png` — procedurally generated rounded-rect / pill / circle sprites, as
  **separate fill and outline** so each can be tinted independently.

Rebuilt to spec: Main Menu, Match HUD, Results, Pause, Shop, Settings. **Lobby is not restyled yet.**

### Main menu

Built around a large animated character, Brawl-Stars style. Layout: title small in the top-left,
character centre on a lit stage with a gold podium ring, PLAY oversized bottom-right, Shop/Skins
icon rail bottom-left, currency + profile + settings across the top.

- `UISpriteAnimator.cs` — cycles sprite frames on a UI `Image`. The Animator clips bind to
  `SpriteRenderer.m_Sprite`, which a Canvas `Image` cannot use; this avoids mixing a world-space
  SpriteRenderer into an overlay canvas. Menu character runs the 36 idle frames at 24fps.
- `MenuMotion.cs` — entrance sequence (character pops first, then panels stagger in) plus an
  ambient PLAY pulse and drifting stage light. All on unscaled time.
- `MenuFreeCoins.cs` — rewarded-ad button that **hides itself unless an ad is actually loaded**,
  so it is never a dead control.
- Backdrop is `Generated/UI/MenuBackdrop.png`, generated square (2048) and scale-to-cover cropped
  top/bottom. Deliberately dark and empty in the centre so the character reads without a scrim.
- Stage glow uses `Resources/UI/ui_glow.png`, a squared radial falloff. A flat circle sprite at low
  alpha shows a hard rim — it reads as a disc, not light.

## Monetisation

Unity Ads 4.19.0 installed. `AdService.cs` wraps init / rewarded / interstitial with a 120s cooldown.
Wired to: results-screen "double coins", shop "watch ad for 75 coins", interstitial on match exit.

Entry points: results-screen "double coins", shop "watch ad for 75 coins", menu top-bar FREE button,
interstitial on match exit.

**Live and verified in-editor** (2026-08-04): `Advertisement.isInitialized`, `RewardedReady` and
`InterstitialReady` all true, placements `Rewarded_iOS` / `Interstitial_iOS`.

Game IDs on the `AdService` component — **these two are not interchangeable**:

| Platform | Game ID |
|---|---|
| iOS | `800111804` |
| Android | `800111807` |

The editor runs against the **iOS** ID. A wrong ID still *initialises* (the SDK accepts any valid
project ID) but every placement load then fails with "placement not found" — the symptom looks like
a placement-name bug and isn't. Check the ID against the Dashboard → Monetization → Placements page,
which lists the Game ID the placements belong to.

**`testMode` is now OFF** on the `AdService` object in `Scenes/MainMenu.unity` — the build serves real
ads. It is a serialized field, not an Xcode or dashboard setting, so changing it needs a fresh Unity
export. (`_Recovery/0 (2).unity` still has it on; that scene isn't in the build.)

Never tap your own live ads — Unity treats self-clicks as invalid traffic and will withhold revenue or
ban the account. Flip `testMode` back on for any real play-testing.

**Testing ads**: press Play from `MainMenu`. The top-bar **FREE** button and the results-screen
**double coins** button only appear once `RewardedReady` is true, so their appearance *is* the check
that ads loaded. Interstitials fire on match exit, rate-limited to one per 120s.

IAP (`IAPService.cs`) is still a dev stub that grants gems free.

**Known build blocker**: installing Unity Ads pulled in the Mobile Dependency Resolver, which fails to
install CocoaPods because system Ruby is 2.6 and it needs 3.0+. Harmless in the editor, but it **will
break an iOS build**. Fix with `brew install cocoapods`.

---

## Traps already hit — don't repeat these

1. **`GameObject.Find` skips inactive objects.** Cleanup code using it silently missed hidden panels and
   produced duplicate Pause/Results/Shop panels. Traverse children instead.
2. **A `NetworkBehaviour` without a `NetworkObject` has `IsServer == false`, silently.** This made
   `NetworkFlagBase` unable to score — the game was literally unwinnable for most of a session. If a
   script only needs server gating, prefer a plain MonoBehaviour + `NetworkManager.Singleton.IsServer`.
3. **An Animator owns `m_Sprite`.** Any code assigning `spriteRenderer.sprite` gets overwritten every
   frame. `TeamMember.ApplySkin` checks for an animator and backs off.
4. **A compile error blocks the whole assembly** and Unity keeps loading the stale DLL — new types
   appear "missing" for unrelated reasons. Check `Logs/Editor.log` for `error CS` before assuming the
   editor is wedged.
5. **Screen-space UI can't be screenshotted.** To review a menu: temporarily set the Canvas to
   `WorldSpace` at scale 0.01, capture the region, then restore. This is the only way to see UI work.
6. **Timed-out asset generations still write a PNG — a blank one.** Open every generated file before
   wiring it in; a blank sprite looks identical to a good one in the inspector.
7. **Generated image dimensions must be a multiple of 16, between 128 and 2048.**
8. **Characters are front-facing, not overhead.** Movement direction uses `flipX`, which only reads
   correctly on a camera-facing sprite. Ground markings are true top-down; characters and upright props
   face the camera.
9. **Stop Play mode before `EditorSceneManager.OpenScene`** or it throws.
10. **Reading pixels via the imported Texture2D bakes in DXT artifacts.** Decode the PNG from disk with
    `LoadImage` when doing colour work.
11. **Watch for stale `localScale`.** The joystick carried a 3.15× scale that made a 264px control render
    at 832px; the player prefab carried 1.7× from the old spritesheet.
12. **A component must never `SetActive(false)` its own GameObject to hide itself** — that stops its
    `Update()`, so it can never turn itself back on. `MenuFreeCoins` did this and the FREE button was
    permanently invisible even with ads loaded. Hide with a `CanvasGroup` (`alpha` / `interactable` /
    `blocksRaycasts`) instead.
13. **A valid-but-wrong Unity Ads Game ID initialises successfully**, then fails every placement load.
    See Monetisation above.
14. **A solid magenta screen on device means a stripped shader, not a broken material.** The whole
    menu rendered magenta because `UI/Default` was missing from the build: the engine resolves it
    by name at runtime, so nothing in the project marks it as used and the build strips it, while
    `Sprites/Default` survives because real sprite assets reference it. `Always Included Shaders`
    was empty from the initial commit and now lists `UI/Default`, `UI/Default Font`,
    `UI/DefaultETC1`, `Sprites/Default`, `Sprites/Mask`, `Legacy Shaders/Diffuse`.
    Diagnose by grepping the built `Data/` for the shader name — do not guess.
    **The fix only lands in a fresh Unity export**; rebuilding in Xcode alone changes nothing.
15. **A serialized scene value silently beats the code default.** `botFillDelay` was 30 in
    `fsfs.unity`, so editing the field initialiser did nothing. Change both, or change the scene.
16. **Reflection is blocked in Unity RunCommand.** Use `SerializedObject`/`SerializedProperty`
    to inspect components — it also shows what is actually wired, which reflection does not.
17. **`ShopController` and the other panel controllers never call `onClick.AddListener`.** Every
    button is scene-wired, so assigning a serialized field is not enough — add a persistent
    listener with `UnityEventTools.AddPersistentListener` or the button does nothing.

---

## Orientation, onboarding, IAP (added 2026-08-05)

- **`MenuResponsiveLayout.cs`** on `MainMenu`'s `SafeAreaPanel`. The menu was composed for
  1920x1080 and cannot simply be scaled into portrait: `TopBar` is a fixed 1920 wide, and
  `PlayWrap` (560) + `IconRail` (400) + margins need ~1072 of the 1080 available. So it keeps
  two presets and swaps, capturing the landscape baseline at runtime rather than duplicating
  the numbers. It also drives `CanvasScaler` to match **width** in portrait and **height** in
  landscape — the authored `match=0.5` is what made portrait shrink everything and still clip.
  It also reshapes the Shop card, which is authored 1500 wide and would otherwise spill off
  a portrait screen entirely.
  **Not `[ExecuteAlways]` on purpose** — in edit mode it would capture whatever the Game view
  aspect happened to be and serialize the portrait overrides as the authored layout.
- **Bots now fill instantly** (`botFillDelay` 0, in code *and* the `fsfs.unity` serialized
  value — the scene value silently wins over the code default). `BotFillRoutine` always yields
  one frame first, or bots claim spawn points before the host's own player exists.
  Trade-off: a human who matchmakes a moment later joins an already-full game.
- **`TutorialController.cs`** on `fsfs`. Four steps (move / dash / grab flag / score), each
  gated on the player actually performing the action. Builds its overlay in code so it cannot
  drift from the HUD it points at, and **never blocks raycasts** — every step needs the real
  controls, so an input-eating overlay would soft-lock first launch. Gate key
  `tutorial_completed_v1`; `TutorialController.ResetProgress()` re-arms it.
- **Remove Ads** is a real non-consumable (`remove_ads`). Only interstitials are suppressed;
  rewarded stays live because it is opt-in and a payer still wants the coins. The entitlement
  is never revoked when the store reports no receipt — an offline launch must not strip a
  purchase, at the cost of a refund not restoring ads until PlayerPrefs are cleared.
  `IAPService` no longer grants gems free; the dev stub is gone.

## Testing multiplayer

`com.unity.multiplayer.center` is the **setup wizard**, not the feature. Install
`com.unity.multiplayer.playmode`, then Window → Multiplayer → Multiplayer Play Mode and enable
Player 2. Each virtual player gets its own Library, so budget several GB of disk.

## Outstanding

- **Audio — completely untouched.** Zero clips in the project. Biggest remaining gap for game feel,
  and the most self-contained next task.
- Four remaining skins (art + animation). Generate the **red** variant only — the hue-swap script
  produces blue in seconds.
- Lobby screen not restyled — the last screen still on the old flat look.
- **1024x1024 App Store icon** is missing. Uploads fail with error 91111 without it; set it in
  Player Settings → iOS → Icon, not Xcode, or the next Unity export overwrites it.
- App Store Connect: create `remove_ads` (Non-Consumable, Tier 2 / $1.99 — there is no $2.00
  tier) and the gem consumables. Products must reach "Ready to Submit" or the shop shows
  "Unavailable" instead of a price. The listing name is currently the typo "Captue the flag".
- `brew install cocoapods` before attempting an iOS build.
- Match pacing is defence-heavy; tunable via `tagRadius`, `chaseRange`, `scoreToWin`, role rotation.

## Disk (this bit will bite you)

The boot volume repeatedly hit 0 bytes free, and every failure it caused looked like something
else: "The file could not be unlocked", `IOException: Directory not empty` from Unity's iOS
post-processor, and an App Store upload rejected as **"Invalid package. The uploaded package is
corrupt"** — which was really `ditto` failing to copy `UnityFramework` and `global-metadata.dat`
into the .ipa. Xcode logs actual free space at the top of `ContentDelivery.log`; check it first.
Archive + upload stages the .ipa in `/var/folders` on the **boot** disk, so relocating DerivedData
does not help. Keep 20GB free.

`rm -rf` failing with "Directory not empty" means Finder is recreating `.DS_Store` faster than
the delete walks the tree — close the Finder window, or delete the `.DS_Store` files and the
folder in a single command.

## Reviewing UI work

You cannot screenshot a screen-space canvas. To see any menu:

1. Set the Canvas to `WorldSpace`, `sizeDelta` 1920×1080, `localScale` 0.01, position zero.
2. Capture the region `(-9.6, -5.4)` size `19.2 × 10.8`.
3. Restore `ScreenSpaceOverlay`, `localScale` 1, and **save the scene**.

Every real UI bug this session — the 3.15× joystick, the hard-rimmed glow, overlapping panels,
duplicate panels, an empty name field — was invisible in the inspector and obvious in a capture.

## Reference

- UI kit: https://claude.ai/code/artifact/280bd765-8b6a-447c-a3da-43bd5f183264
- Sprite prompt pack: https://claude.ai/code/artifact/c73ab465-2281-4e31-9912-4eccbc88d4fc
