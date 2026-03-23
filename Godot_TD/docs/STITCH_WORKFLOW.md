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
> Dark sci-fi theme. Background #0b1326. Primary cyan #81ecff.
> Font: Space Grotesk for headings, Manrope for body.
> Material Symbols Outlined for icons. Tailwind CSS.
> Animated grid background with scanline overlay.
> All interactive elements should have data-action="action-name" attributes.

**Example prompt for a debrief screen:**
> "Design an extraction results screen for a sci-fi tower defense game. Show a large
> resource number in the center (the extraction score), wave reached below it,
> enemies eliminated, and personal best comparison. Two buttons at the bottom:
> 'EXTRACT AGAIN' (data-action='play-again') and 'RETURN TO BASE'
> (data-action='return-to-menu'). Dark theme, cyan accents, Space Grotesk font."

### 2. Save the HTML

Save Stitch output to: `Godot_TD/ui/<screen-name>/index.html`

### 3. Run the scaffold tool

```bash
python tools/stitch_scaffold.py ui/debrief/index.html DebriefScreen --auto
```

Or specify buttons manually:
```bash
python tools/stitch_scaffold.py ui/debrief/index.html DebriefScreen --buttons play-again,return-to-menu,save-suit
```

This generates:
- `Scripts/UI/DebriefScreen.cs` — C# bridge with CEF loading + IPC handlers + fallback UI
- `Scenes/DebriefScreen.tscn` — Godot scene file
- Prints code snippets to paste into Constants.cs and GameManager.cs

### 4. Wire up the handlers

Open the generated C# file and fill in the `On<ButtonName>()` methods:

```csharp
private void OnPlayAgain()
{
    GameManager.Instance?.StartVineBattle();
}

private void OnReturnToMenu()
{
    GameManager.Instance?.ReturnToMainMenu();
}
```

### 5. Register in GameManager

Add the constants and navigation method the scaffold printed.

---

## HTML ↔ C# Communication

**HTML → Godot (button clicks):**

In your Stitch HTML, add this to interactive elements:
```html
<button data-action="play-again" onclick="sendToGodot('play-again')">PLAY AGAIN</button>
```

And this JS at the bottom:
```javascript
function sendToGodot(action, payload) {
    if (window.cefQuery) {
        window.cefQuery({ request: JSON.stringify({ action: action, payload: payload || '' }) });
    }
}
// Tell Godot the UI is ready
sendToGodot('ready');
```

**Godot → HTML (update data):**

From C#, you can execute JS in the browser:
```csharp
_cefTexture.Call("execute_javascript",
    $"document.getElementById('score').textContent = '{score}'");
```

---

## Existing Screens (Adam's Work)

| Screen | HTML | C# Bridge | Status |
|--------|------|-----------|--------|
| Title Screen | `ui/title/index.html` | `Scripts/UI/MainMenuUI.cs` | Working |
| Planet Select | `ui/code.html` | `Scripts/UI/PlanetSelectScreen.cs` | Working |
| Loadouts | (Godot-native) | `Scripts/UI/LoadoutsScreen.cs` | Working (no CEF) |

## Screens Still Needed

| Screen | HTML Path | C# Class | Buttons |
|--------|-----------|----------|---------|
| Meta Hub | `ui/meta-hub/index.html` | MetaHubScreen | territory, suits, relics, node-shop, start-run |
| Debrief | `ui/debrief/index.html` | DebriefScreen | play-again, return-to-menu, save-suit |
| Boss Confirm | `ui/boss-confirm/index.html` | BossConfirmScreen | confirm-run, cancel, change-suit |
| Relic Inventory | `ui/relic-inventory/index.html` | RelicInventoryScreen | equip, unequip, back |
| Suit Management | `ui/suit-management/index.html` | SuitManagementScreen | rename, equip-relic, delete, back |
| Settings | `ui/settings/index.html` | SettingsScreen | save, reset-defaults, back |

---

## Style Reference

From Adam's existing screens, the Stitch design language is:

- **Background:** `#0b1326` (deep navy) or `#0e0e0e` (near black)
- **Primary:** `#81ecff` (cyan) / `#c5eaff` (light cyan)
- **Accent:** `#00e3fd` (bright teal)
- **Error/Danger:** `#ff716c` / `#fe1543` (red)
- **Text:** `#ffffff` on dark, `#dae2fd` for secondary
- **Font Heading:** Space Grotesk (300-900 weight)
- **Font Body:** Manrope (300-700 weight)
- **Icons:** Material Symbols Outlined
- **CSS Framework:** Tailwind via CDN
- **Animations:** Grid backgrounds, scanlines, fade-ins, metallic shine
- **Border radius:** Tight (0.125rem default, 0.75rem for "full")

---

## Gotchas

- Stitch loads Tailwind from CDN — needs internet on first load. For offline, inline the Tailwind output or use a local build.
- gdcef is Windows-only currently (the DLLs in the addon are x86_64-pc-windows-msvc)
- CEF adds ~200MB to the project (all the Chromium DLLs and locale files)
- Always build a fallback UI in case CEF isn't available (Linux, Mac, stripped builds)
- IPC message names must match exactly between HTML `data-action` values and C# `switch` cases
