# Stitch → Godot UI Pipeline

*How to design UI in Google Stitch and render it in Godot via gdcef.*

---

## Architecture

```
Stitch (browser)  →  HTML/Tailwind/JS  →  ui/<screen>/index.html
                                              ↓
                                         godot-cef addon
                                              ↓
                                    CefTexture (renders HTML as texture)
                                              ↓
                                   C# Bridge Class (Scripts/UI/<Screen>.cs)
                                    - loads HTML via CEF
                                    - handles IPC messages (button clicks)
                                    - translates to GameManager actions
                                    - has fallback Godot UI if CEF missing
```

---

## Quick Start (New Screen)

### 1. Design in Stitch

Open https://stitch.withgoogle.com/ and describe your screen.

**Always include this style context in your prompt:**
> Dark sci-fi theme. Background #0b1326. Primary cyan #c5eaff / #7dd3fc.
> Font: Space Grotesk for everything. Material Symbols Outlined for icons.
> Tailwind CSS via CDN. Glass-panel style with backdrop blur.
> Scanline overlay + radial gradient background + corner decorations.
> All interactive elements fire IPC via window.sendIpcData().

### 2. Save the HTML

Save Stitch output to: `Godot_TD/ui/<screen-name>/index.html`

### 3. Create the C# Bridge

Follow the established pattern (see reference implementations below). Every bridge class:
- Extends `Control` (for full-rect CEF) or `CanvasLayer` (for code-built fallback)
- Creates `CefTexture` in `_Ready()`, sets URL, connects signals
- Injects `__stitchBridge` helper on `load_finished`
- Handles `ipc_data_message` with action dispatch
- Exposes a `window.__<screenName>UI` JS API for Godot→HTML data push
- Has a complete fallback native UI for when CEF is unavailable

### 4. Create the Scene

Minimal `.tscn` with a Control node and the script attached:
```
[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://Scripts/UI/YourScreen.cs" id="1"]
[node name="YourScreen" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1")
```

### 5. Register in Constants + GameManager

Add `SCENE_YOUR_SCREEN` to `Constants.cs`, add a `ShowYourScreen()` method to `GameManager.cs`.

---

## HTML ↔ C# Communication (Actual Pattern)

**HTML → Godot (IPC messages):**

All screens use `window.sendIpcData()` — this is the godot-cef built-in function:
```javascript
window.sendIpcData({ action: 'navigate', data: { target: 'territory' } });
```

Standard actions: `ready`, `navigate`, `menu-select`, `save-suit`, `skip-suit`, `continue-to-hub`, `relic-selected`, `equip-relic`, `unequip-relic`

**Godot → HTML (eval JS):**

From C#, push data by calling JS functions on a `window.__<screen>UI` API object:
```csharp
_cefTexture.Call("eval", $"window.__metaHubUI.setResources({amount})");
_cefTexture.Call("eval", $"window.__debriefUI.init(true, 500, 500, 12, 5, 'Harvest', 'P1')");
```

**Bridge injection (on load_finished):**
```csharp
string bridgeJs = @"
    if (!window.__stitchBridge) {
        window.__stitchBridge = {
            sendAction: function(action, data) {
                window.sendIpcData({ action: action, data: data || {} });
            }
        };
    }
";
_cefTexture.Call("eval", bridgeJs);
```

**IPC handler pattern:**
```csharp
private void OnIpcData(Variant data)
{
    if (data.VariantType != Variant.Type.Dictionary) return;
    var dict = data.AsGodotDictionary();
    string action = dict.ContainsKey("action") ? dict["action"].AsString() : "";
    var actionData = dict.ContainsKey("data")
        ? dict["data"].AsGodotDictionary()
        : new Dictionary();

    switch (action)
    {
        case "navigate":
            string target = actionData.ContainsKey("target") ? actionData["target"].AsString() : "";
            HandleNavigate(target);
            break;
        case "ready":
            GD.Print("[Screen] Ready");
            break;
    }
}
```

---

## Scene Navigation

All scene changes go through `GameManager` methods which use `TransitionManager` for fade transitions:

```
MainMenu → StartPlanetSelect() → LaunchFromPlanetSelect() → IntroCinematic → VineDraft → VineBattle
VineBattle → Victory/Defeat → ScheduleDebrief(2s) → Debrief → ShowMetaHub() → Meta Hub
Meta Hub → ShowTerritory() / ShowSuitInventory() / ShowRelicInventory() / StartPlanetSelect()
Territory/Suits/Relics → ESC/Back → ShowMetaHub()
Meta Hub → Main Menu → ReturnToMainMenu()
```

---

## Implemented Screens

| Screen | HTML | C# Bridge | Scene | Status |
|--------|------|-----------|-------|--------|
| Title | `ui/title/index.html` | `MainMenuUI.cs` | `MainMenu.tscn` | Working |
| Planet Select | `ui/code.html` | `MainMenuUI.cs` (URL swap) | `MainMenu.tscn` | Working |
| Meta Hub | `ui/meta-hub/index.html` | `MetaHubScreen.cs` | `MetaHub.tscn` | Working |
| Debrief | `ui/debrief/index.html` | `DebriefScreen.cs` | `Debrief.tscn` | Working |
| Relic Inventory | `ui/relic-inventory/index.html` | `RelicInventoryScreen.cs` | `RelicInventory.tscn` | Working |
| Loadouts | (Godot-native) | `LoadoutsScreen.cs` | `Loadouts.tscn` | Working (no CEF) |
| Territory | (Godot-native) | `TerritoryScreen.cs` | `Territory.tscn` | Working (no CEF) |
| Boss Confirm | (Godot-native) | `BossConfirmScreen.cs` | `BossConfirm.tscn` | Working (no CEF) |

## Screens Still Needed

| Screen | HTML Path | C# Class | Notes |
|--------|-----------|----------|-------|
| Suit Detail | `ui/suit-detail/index.html` | SuitDetailScreen | Left: 3 suit slots, Right: grid viz + stats + relic equip |
| Settings | `ui/settings/index.html` | SettingsScreen | Volume, fullscreen, controls |
| Territory (CEF rewrite) | `ui/territory/index.html` | TerritoryScreen | Replace current code-built version |
| Boss Confirm (CEF rewrite) | `ui/boss-confirm/index.html` | BossConfirmScreen | Replace current code-built version |

---

## Transition System

`TransitionManager.cs` is an autoload singleton (CanvasLayer, Layer 99, ProcessMode.Always):
- `TransitionToScene(path)` — fade out 0.4s → change scene → wait frame → fade in 0.4s
- `FadeOut()` / `FadeIn()` — standalone controls
- Guard flag prevents concurrent transitions
- All `GameManager` scene changes go through `ChangeScene()` which uses TransitionManager if available

---

## Style Reference

From the established screen designs:

- **Background:** `#060e20` (deepest) / `#0b1326` (surface) / `#171f33` (container)
- **Primary:** `#c5eaff` (text) / `#7dd3fc` (container) / `#7bd1fa` (dim)
- **Tertiary:** `#65fce6` (accent green/teal) / `#3cddc7` (dim)
- **Error:** `#ffb4ab`
- **Text:** `#dae2fd` (on-surface) / `#bec8ce` (on-surface-variant) / `#bec6e0` (secondary)
- **Font:** Space Grotesk (all weights, headline + body + label)
- **Icons:** Material Symbols Outlined
- **CSS:** Tailwind via CDN
- **Panels:** `.glass-panel` — gradient bg + backdrop-blur-12px
- **Buttons:** `.hologram-btn` — gradient bg, border glow, sweep animation on hover
- **Overlays:** Scanline texture, radial gradient, corner decorations (4px borders), vignette inset shadow
- **Animations:** fade-up entries with staggered delays, count-up counters, pulse-glow on CTA buttons

---

## Gotchas

- Stitch loads Tailwind from CDN — needs internet on first load. For offline, inline the Tailwind output or use a local build.
- gdcef is Windows-only currently (the DLLs in the addon are x86_64-pc-windows-msvc)
- CEF adds ~200MB to the project (all the Chromium DLLs and locale files)
- Always build a fallback UI in case CEF isn't available (Linux, Mac, stripped builds)
- IPC action names must match exactly between HTML `sendIpcData()` calls and C# `switch` cases
- Escape single quotes in strings passed via `eval()` — use `EscapeJs()` helper
- For JSON data, use `Json.Stringify()` on the C# side and `JSON.parse()` on the JS side
