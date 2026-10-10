# M4 contract: HUD and menus (GTA V on PC look and feel)

Owns `src/GtaSkylines.Mod/Hud/` and `src/GtaSkylines.Mod/Menus/` (net35, Unity IMGUI + GL).
Draws only from the Game's `HudModel`, `MenuRequest`, `PhoneModel`, `WeaponWheelModel`,
`MissionStatus` and `GameSettings`; it never decides game rules. Menu choices go back to the
session through a small command interface (buy item, set waypoint, change setting, resume,
save, return to city builder). No per-frame allocation in steady state: cache `GUIContent`s,
`GUIStyle`s, textures and formatted strings by value; draw text with outline by drawing the
string offset in black first (or a shadow).

## Visual language

GTA V's PC HUD: flat, high-contrast, no frames around HUD elements; white text with a 1-2 px
dark outline; condensed bold sans font (CS1's UI font or Unity's built-in Arial bold if the
game font cannot be used from IMGUI; research `UIView`'s default font and whether its
`baseFont` works with `GUIStyle`); scale by screen height (1080p reference) times the HUD scale
setting; safe-zone margins of 3 % of the screen. Colours: health green #57A85E, armour blue
#5DA9E9, cash green #72CC72 (+) and red #E05050 (−), stars white, mission yellow #F0C850,
objective highlight colours yellow/blue/red/green by span kind, help box black at 70 %
opacity.

## HUD elements (DESIGN.md "HUD")

- Minimap bottom-left (aspect 1.55:1, height 18 % of the screen): a city map texture
  rasterised once per city load at ~1 px per 2 m around the playable area (roads from the road
  graph drawn as thick light-grey lines with widths by lane count, highways yellowish, buildings
  as darker grey footprints, water blue, parks green from the city adapter), redrawn when the
  road graph version changes; per frame a GL pass into a RenderTexture rotates and scales it
  around the player (north-up option in settings), zoom from the HUD model, then draws the GPS
  route (purple, GTA style), the search-zone circle (flashing red/blue), blips (player arrow,
  waypoint, mission yellow, job blue, shop/safehouse icons, cops as small blue/red dots while
  wanted, targets red), edge-clamped blips for far targets; rounded-corner mask optional.
  Under it the health (left 55 %) and armour (right 45 %) bars, and a breath bar while
  swimming. Z toggles an enlarged minimap.
- Top-right column: wanted stars (5 outlines, filled ones white; while evading the filled ones
  pulse grey/white), cash ($1,234,567 with thousands separators) with a short-lived delta line
  under it (+$500 green / −$200 red), weapon name and ammo (magazine / reserve) when armed.
- Top-left help box (max ~40 % width, word-wrapped, key names in brackets styled like keys),
  fading in/out.
- Bottom-centre: subtitle line (speaker name coloured) and the objective line with highlight
  spans; a timer when a step has one; race position.
- Bottom-right: street / district banner (two lines, fade in/out over 4 s); vehicle name
  banner on entering; speedometer (km/h) while driving (small, unobtrusive).
- Notification feed stacked above the minimap (phone texts with contact name, Chirper posts
  with the citizen's name and @handle style), each 6 s.
- Crosshair: small dot when aiming or armed in first person; turns red over hostiles; the ped
  card (name, age, job, home, happiness) beside the crosshair when the HUD model has one.
- Interaction markers' labels are world-space in M3; prompts are the help box here.
- Full-screen states: WASTED (red tinted, desaturating vignette via a full-screen texture
  overlay, big serif-like title), BUSTED (blue tint), MISSION PASSED (gold title, reward line),
  MISSION FAILED (red title, reason), each animated in and out; black fades for interior
  transitions and respawns.

## Menus

- Pause menu (Esc/P): GTA-style tab bar — MAP (full-screen map from the same city texture: pan
  with drag or WASD, zoom with the wheel, legend, right-click/Enter sets or clears the
  waypoint which drives the GPS route), BRIEF (current objective, mission log, side-job
  levels), STATS (profile counters), SETTINGS (every `GameSettings` field live, and key
  rebinding identical in behaviour to the options page from M1), CONTROLS (the bindings table),
  GAME (Resume, Save game (triggers CS1's own save), Return to City Builder with a confirmation).
  Keyboard and mouse navigation; Backspace/Esc goes back.
- Weapon wheel (hold Tab): radial segments with weapon names and ammo, selection by mouse
  direction, slows the game while open (session pauses the fixed step).
- Phone (Up arrow): a phone-shaped panel bottom-right with apps: Contacts (call → mission
  info), Messages, Jobs, GPS shortcuts; arrow keys + Enter; Backspace closes.
- Interaction menu (M): quick GPS, outfit (when in a safehouse), current mission retry/abort.
- Shop / service menus from `MenuRequest`: GTA-style vertical list on the left with header
  banner (shop name), item price on the right, owned/insufficient-funds states, description
  line, Enter buys, Backspace closes; clothes shop previews the outfit name; property purchase
  confirmation; respray; hospital heal; police pay-off; safehouse bed (Save / Sleep / Cancel).

## Tests

Pure helpers (cash formatting, star pulse timing, text wrapping widths with a fake measurer,
blip edge clamping, minimap world↔map transform, radial selection from a direction, menu list
navigation state machine) get net10 tests in `tests/GtaSkylines.Mod.Tests`. Drawing itself is
covered by the in-game self-test screenshots (M6).
