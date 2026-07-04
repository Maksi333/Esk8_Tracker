# Handoff: ESK8 Ride Tracker

## Overview
A telemetry-grade ride tracker for electric skateboards (Android, phone-first, portrait). It records rides over GPS and renders them on a **velocity color ramp** (slow = blue → fast = red). This handoff covers the full production screen inventory: onboarding/permissions, the live Ride state machine (Idle → Active → Paused → Summary), History, Ride Detail, Garage + Board Editor, Stats/achievements, Settings, plus the foreground-notification shade and system toasts.

Target platform per the product team: **.NET MAUI** (Android primary). If the MAUI solution doesn't exist yet, scaffold a new MAUI (net8.0-android) app and implement there.

---

## ⚠️ READ FIRST — No fake data in the shipped app
This app is going to the **Google Play Store**. The production build must contain **zero mock/seed/placeholder data**. Every number the user sees — speed, distance, elevation, routes, records, achievements, lifetime totals — must come from **real GPS + real saved rides on the device**. A brand-new install must show genuine empty states (no rides yet, 0 km lifetime, all achievements locked/at-0), not the demo content in these prototypes.

- **During development you may absolutely create test/seed data** to exercise the UI — behind a debug flag, `#if DEBUG` block, a dev-only "seed" button, or unit/UI-test fixtures. Just make sure none of it can reach a release build.
- Before release: verify a clean install has no seeded rides, boards (beyond an empty Garage / first-board prompt), records, or achievement progress.
- The colorized sample routes, the 7 demo rides, the 3 demo boards, "1,284 km lifetime", "96 rides", pre-earned badges, weekly chart bars, etc. in the prototype are **illustrative only**. Delete them from anything that ships.

---

## About the Design Files
The files in this bundle are **design references created in HTML** — a single interactive prototype (`ESK8 Tracker.dc.html`) demonstrating intended look, layout, motion, and interaction. **They are not production code to copy directly.** The task is to **recreate these designs in the target codebase (.NET MAUI / XAML)** using its established patterns (MVVM, `CommunityToolkit.Mvvm`, `SkiaSharp` for the custom graphs/route/speedometer, `Microsoft.Maui.Maps` or an OSM tile provider for real maps, etc.).

The prototype is a self-contained "Design Component" HTML file. It uses React internally only as a rendering mechanism for the mockup — **do not port the React/JS**; port the *design*.

## Fidelity
**High-fidelity (hifi).** Final colors, typography, spacing, iconography, and interaction behavior are all intended as spec. Recreate pixel-faithfully in MAUI, adapting only where a native platform convention is clearly better (e.g. real Android system notification vs. the simulated shade, real permission dialogs vs. the onboarding mock).

---

## Design Tokens

### Color — surfaces & text (dark theme; dark is the primary/only theme for v1)
| Token | Hex | Use |
|---|---|---|
| `bg/base` | `#0B0E11` | App background, screen base |
| `bg/raised` | `#0F1318` | Insets inside cards (graph wells, thumbnails) |
| `surface/card` | `#151A1F` | Cards, tiles, list rows, sheets |
| `surface/card-2` | `#12181E` | Secondary/quieter cards |
| `surface/elevated` | `#1E252B` | Buttons on cards, active segment |
| `border/hairline` | `#1C242B` | Nav divider, subtle separators |
| `border/default` | `#2A333B` | Card & control borders |
| `border/strong` | `#3A4550` | Active/selected control border |
| `text/primary` | `#F2F5F7` | Headlines, values |
| `text/secondary` | `#B9C4CC` | Body |
| `text/tertiary` | `#8A97A2` | Labels, captions, inactive |
| `text/faint` | `#4A5560` | Disabled, paused values |

### Color — brand / semantic
| Token | Hex | Use |
|---|---|---|
| `accent/primary` | `#4C8DFF` | Primary actions, active nav, links, selection |
| `success/go` | `#22C55E` | START, active recording, save, "active board" |
| `warn` | `#F5C51E` | Paused, records/PR, background-permission warnings |
| `danger` | `#F03E3E` | Stop, discard, delete, top-speed extreme |
| Board colors | `#4C8DFF` `#F5C51E` `#22C55E` `#F03E3E` `#B47CFF` | User picks per board |

Faint fills = the accent at ~10–16% alpha (e.g. `rgba(76,141,255,0.14)` for icon chips, active pills).

### Color — the Velocity Ramp (the core visual system)
A speed value `v` normalized `0..1` against the board's **top speed** maps to a color by linear interpolation between these stops:
| Stop (fraction of top speed) | Hex |
|---|---|
| `0.00` | `#2E7CF6` (blue) |
| `0.40` | `#22C55E` (green) |
| `0.72` | `#F5C51E` (yellow) |
| `1.00` | `#F03E3E` (red) |

Interpolate in RGB between adjacent stops. This ramp colors: the live speedometer, the route polyline (per-segment, using the mean speed of the segment), sparklines, per-km split bars, speed graphs, and the editor's ramp preview bar. **Each board scales the ramp by its own top speed** — a 32 km/h board hits red at 32, a 45 km/h board at 45.

Route glow: draw each polyline segment twice — a wide (`strokeWidth+7`) pass at `0.16` opacity for bloom, then the crisp segment on top. Toggleable ("ramp glow").

### Typography
Three families:
- **Chakra Petch** — all numerics/telemetry (speed, distance, timers, stats, splits). Tabular figures (`font-variant-numeric: tabular-nums`). This is the "instrument" typeface.
- **Space Grotesk** — display/headings (screen titles, START label, big state words like PAUSED, board names, CTA buttons). Weights 600–700, tight letter-spacing (~-0.4px on titles).
- **Inter** — UI body, labels, captions, list text.
- **Material Symbols Rounded** — all icons (see Assets).

Type scale (px, as used in the 412-wide frame):
| Role | Family | Size / Weight | Notes |
|---|---|---|---|
| Hero speed (glance) | Chakra Petch | 220 / 700 | Glance mode, colorized |
| Hero speed (arc/bar) | Chakra Petch | 92–150 / 700 | letter-spacing -2px, colored glow shadow |
| Big state word | Space Grotesk | 34 / 700 | "PAUSED", letter-spacing 2px |
| Screen title | Space Grotesk | 26 / 600 | -0.4px |
| Onboarding title | Space Grotesk | 27 / 600 | line-height 1.15, supports `\n` |
| Card/stat value | Chakra Petch | 24–40 / 600–700 | tabular |
| Tile value | Chakra Petch | 26 / 600 | live dashboard tiles |
| Body | Inter | 15 / 400 | onboarding body 15, list 14 |
| Label / caption | Inter | 10.5–13 / 500–600 | uppercase labels: letter-spacing ~1px |
| Uppercase section label | Inter | 11 / 600 | letter-spacing 1–1.4px, `text/tertiary` |

### Spacing, radius, motion
- Screen padding: `16–18px` horizontal. Cards: `12–16px` internal.
- Gaps: tiles `10px`, list items `10px`, chip rows `8px`.
- Radius: pills/toggles `100px`; big buttons `16–18px`; cards `14–18px`; tiles/rows `13–14px`; sheets `22px` (top corners); small chips/icon wells `10–12px`; START button is a full circle `210px`.
- Motion: rise/power-on `esk-power` (scale .9→1, opacity, ~.5s ease); list/hero enter `esk-rise` (translateY 10px, .4s); toast `esk-toast` (.3s); bottom sheet `esk-sheet` (translateY 100%→0, .3s); notification shade `esk-shade` (translateY -105%→0, .32s); active-record pulse `esk-pulse` (opacity 1↔.35, 2s infinite). Hold-to-stop fills over **850ms**.

---

## Screens / Views

### 1. Onboarding / Permissions (4 steps)
- **Purpose**: earn the two make-or-break permissions and set up the first board.
- **Layout**: full-screen, centered illustration (210×210 concentric-ring + partial velocity-ramp arc + Material icon), title (supports two lines), body, optional note card. Bottom: 4 progress dots (active dot widens to 26px, green), a primary CTA button (56px, `success/go`, dark text), and a text skip link.
- **Steps & exact copy**:
  1. Title "Track every ride. / All on your phone." · body about telemetry-grade tracking, history is yours to keep · CTA "Get started" (icon `arrow_forward`).
  2. Title "We use GPS to draw / your route" · CTA "Allow location" (`my_location`) · skip "Not now" · **note (lock icon):** "Choose "Allow all the time" so tracking keeps working with the screen off."
  3. Title "Keep tracking alive / in your pocket" · body about Android killing background tracking · CTA "Allow background" (`battery_charging_full`) · skip "I'll risk it" · **note (warning icon):** "This is the #1 reason rides don't save. One tap fixes it for good."
  4. Title "Add your first board" · body: board top speed scales the ramp, battery powers range · CTA "Add a board" (`add`) · skip "Skip for now".
- **Native mapping**: wire the CTAs to the **real** Android runtime permission requests (`ACCESS_FINE_LOCATION` + background location) and the battery-optimization exemption intent (`ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`). Step 4 opens the Board Editor.

### 2. Ride · IDLE (home)
- **Purpose**: launch a ride.
- **Layout** (top→bottom): status row [GPS strength chip (`gps_fixed`, green "GPS strong") · board switcher pill (color swatch + name + `expand_more`)]; optional **ride-recovery banner** (warn border: "Resume unsaved ride?" + Resume/close); optional **limited-permission banner**; centered **Lifetime** microstat; the big circular **START** button (210px, radial green gradient, glow, `play_arrow` + "START / RIDE"); **Last ride** card (sparkline thumb + name + stat line + chevron).
- **START** → Active. Recovery "Resume" → Active resuming a recovered ride. Switcher pill → Board Switcher sheet. Last-ride card → Ride Detail.

### 3. Ride · ACTIVE (live dashboard) — the hero screen
- **Purpose**: at-a-glance telemetry while riding.
- **Layout**: header row [recording dot (pulsing green `fiber_manual_record`) + elapsed timer (Chakra Petch); right: voice-cue indicator + lock/glance button]. Center: the **speedometer** (see Variations). Below: 2×2 **telemetry tiles** — Distance, Top speed (colorized to ramp), Range (~, from board battery), Avg speed; each tile = uppercase label + small colored icon + big Chakra Petch value + unit. Then a control row: **PAUSE** (elevated, `pause`) + square **MAP** button (opens live route peek). Bottom: **hold-to-stop** bar (red fill grows L→R over 850ms; label "STOP · HOLD" → "KEEP HOLDING…"; completing stops the ride).
- **Header is tappable** to pull down the notification shade (simulating the foreground service notification).
- **Glance/lock button** → Glance mode: pure black screen, one enormous colorized speed number + unit + distance; "Tap to exit glance mode". Tap anywhere exits.
- **MAP** → Live route peek overlay: 340px route on the dark map field drawn up to "now" with a white pulsing head marker, plus a ramp legend (0 → top).

### 4. Ride · PAUSED
- **Purpose**: clear paused state, resume or stop.
- **Layout**: centered `pause_circle` (warn, pulsing) + "PAUSED" + reason line ("manual" or "auto — stopped moving") + frozen speed (faint) + two held stats (distance, moving time). Big **RESUME** button (`success/go`) + the same hold-to-stop bar.
- **Auto-pause**: when the sim/GPS detects a stop and sensitivity ≠ Off, transition here with reason "auto — stopped moving", then auto-resume when movement continues. Manual pause has no auto-resume.

### 5. Ride · SUMMARY (post-ride, pre-save)
- **Purpose**: review, name, then Save or Discard. **Save/Discard are the only exits — no bottom nav here.**
- **Layout** (map-hero variant): header [Discard (left, `close`) · Save (right, green)]; colorized route map (280px); editable ride name + `edit`; board + tag chips; stats row (distance / moving / avg / **top colored to ramp**); **speed-over-distance** graph (colorized line + gradient area); a **PR callout** if a record was set (warn card, `emoji_events`, "New top speed! … personal best"); weather/notes row; Photo + Share buttons.
- **Save** → persists ride, returns to Idle, toast "Ride saved to History". **Discard** → confirm dialog → discards, toast.

### 6. History
- **Layout**: title + search/filter buttons; period segmented control (Week/Month/**Month active**/Year/All); aggregate card (period label + "distance · rides" + sparkline); vertical list of ride cards [route thumbnail (66×52, no glow/bg) + name + stat line + board dot & name + date + chevron].
- Row → Ride Detail.

### 7. Ride Detail
- **Layout**: header [back · export (`ios_share`) · more]; ride name + meta line (date · time · board · weather); **scrub header** (4 live values that update as you scrub: speed(colored)/dist/elev/time); **route map** (230px) drawn **up to the scrub position**; **scrubber** slider (accent `#4C8DFF`) "Drag to scrub the route"; 3×2 **stat grid** (distance, top(colored), moving, avg, ↑gain, total); **Speed** graph (colorized, with a scrub marker line + dot); **Elevation** area graph; **Splits** — per-km (or per-mi) rows: index + ramp-colored avg-speed bar + avg + top(colored).
- Opening a detail resets scrub to **100% (end)** so the full route shows first. Export → toast "Exported ride.gpx" (wire to real GPX export).

### 8. Garage
- **Layout**: title + add button (`add`, accent). List of **board cards**: 4px left color spine; icon well (board color faint) + `skateboarding` icon; name (Space Grotesk) + specs line (cells · Wh · top speed); "Active" pill (green dot) on the active board; footer with odometer ("X km on this board") + "Set active" button on non-active boards. Card tap → Board Editor.

### 9. Board Editor (full-screen sheet)
- **Layout**: header [close · title "New/Edit board" · Save (accent)]. Body: icon well + **name** text field; **board color** swatch row (5 colors, selected has white border); **wheel type** radio list (Street urethane / All-terrain pneumatic / Custom, each w/ icon + `radio_button_checked` when selected); **battery** + **top speed** read-fields (top speed shown in danger red, labeled "scales ramp"); **velocity-ramp preview bar** (full ramp gradient) with "0 → {top}" caption; **Delete board** (danger outline) when editing an existing board.
- Save → persists, toast "Board saved". Delete → removes, reassigns active if needed.
- **Note**: battery/top-speed are display-only in the prototype; in production make them editable inputs (steppers/pickers). Top speed is what scales that board's ramp.

### 10. Stats
- **Layout**: title; **Lifetime** hero card (big distance + rides/moving/climbed row); **Records** list (top speed / longest ride / longest session / best streak, each icon+label+colored value); **Distance/week** bar chart (last 12 weeks, latest bar highlighted accent + value label); **Achievements** 4-column grid of badges.
- **Badges**: earned (accent tint bg + accent icon), in-progress (grey icon + bottom progress bar), locked (dimmed). Badge tap → **Achievement sheet**: big badge tile + title + criteria + status (earned date w/ `verified` / progress ring + % / locked lock).

### 11. Settings
- **Layout**: title; **Units** segmented (Metric km / Imperial mi) — switches every unit + speed display app-wide; **Auto-pause sensitivity** (Off/Low/Normal/High); **Live dashboard & cues** toggle rows (Voice cues, Auto glance-mode, Always colorize map) — 46×27 pill toggles, knob slides, track green when on; **Your data · local-first** (Export all rides GPX/JSON, Storage used); a reassurance card ("Everything stays on your device. No account, no server, works fully offline."); version footer.

### 12. Foreground Notification Shade (overlay)
- Pull-down from the Active header. Mimics the Android **foreground-service notification** the ride recording must post. Shows app row + live distance + elapsed + PAUSE / STOP actions. In production this is a **real** persistent notification (required for background location on Android), not an in-app overlay — build it with `Notification` + a foreground `Service`.

### Bottom Navigation
5 tabs: **Ride** (`skateboarding`), **History** (`history`), **Garage** (`garage`), **Stats** (`leaderboard`), **Settings** (`tune`). Active = accent icon (filled) in a faint accent pill + accent label. **Hidden during Active/Paused/Summary ride states.**

---

## Interactions & Behavior
- **Ride state machine**: `idle → active → (paused ⇄ active) → summary → (save|discard) → idle`. Recovery path: idle → active (resume recovered).
- **Hold-to-stop**: pointer-down starts an 850ms fill; release before 100% cancels (resets to 0); reaching 100% stops → Summary. Present on both Active and Paused.
- **Auto-pause**: on detected stop (speed ≈ 0 while mid-ride) and sensitivity ≠ Off → Paused("auto"); resume automatically when moving. Sensitivity controls the speed/time threshold.
- **Glance mode**: toggles a black speed-only overlay; tap to exit.
- **Scrubbing (Detail)**: slider maps 0–100% to sample index; updates the scrub header values, the route "drawn-up-to" length + head position, and the speed-graph marker. Resets to 100% on open.
- **Units**: Metric/Imperial converts all distances (km↔mi ×0.621371), speeds, and unit labels everywhere, including the ramp scaling labels.
- **Toasts**: pill toast bottom-center, auto-dismiss ~2.2s (save, discard, export, board saved/deleted, set active).
- **Board switcher**: bottom sheet; selecting sets active and dismisses.

## State Management (MVVM)
- **RideSession**: state enum, elapsed, current speed, distance, elevation, samples[] (t, d, v, x/y or lat/lng, ele, stop flag), seenTop, active board, tags, name. Live-updated from the location service at ~1–5 Hz.
- **Persistence (local-first, offline, no server)**: saved rides, boards, per-board odometers, records, achievement progress, settings (units, auto-pause, toggles). Use SQLite (`sqlite-net`) or the file system. **Everything on-device.**
- **Derived**: lifetime totals, records, weekly distance, achievement evaluation — all computed from real saved rides.
- **Crash recovery**: persist the in-progress ride periodically so an interrupted ride surfaces the recovery banner.
- **Settings** drive live behavior (units formatting, auto-pause thresholds, colorize toggle, voice cues, auto-glance).

## Rendering the custom graphics (SkiaSharp recommended)
The speedometer, route polyline, sparklines, speed/elevation graphs, split bars, ramp bars, and legend are all custom-drawn in the prototype via SVG. In MAUI, implement them with **SkiaSharp** (`SKCanvasView`). Port the geometry from the prototype's builder methods (`heroSpeedo`, `routeSvg`, `spark`, `speedGraph`, `eleGraph`, `weekChartEl`, `rampBar`, `legendEl`) and the `ramp(fraction)` interpolation. For real maps, draw the ramp-colored polyline over `Microsoft.Maui.Maps` or an OSM tile view instead of the abstract dark field.

## Speedometer Variations (design decision needed)
The prototype exposes three treatments — pick one (or make it a setting):
- **Arc** (default): 250° bottom-arc gauge, ramp-segmented, white marker, big number beneath.
- **Radial**: near-full 280° ring, number centered.
- **Bar**: vertical stacked ramp segments filling upward, number above.
All three color-fill up to the current speed on the ramp.

## Summary Layout Variations
- **map-hero** (default): full-width route map on top, stats below.
- **split**: route + key stats side-by-side, then graphs.

---

## Assets
- **Icons**: Material Symbols Rounded (Google, Apache-2.0). Names used incl. `skateboarding, gps_fixed, my_location, battery_charging_full, play_arrow, pause, pause_circle, stop_circle, lock, map, bolt, straighten, speed, battery_5_bar, timer, terrain, schedule, show_chart, emoji_events, verified, garage, leaderboard, tune, history, search, filter_list, add, radio_button_checked, delete, ios_share, more_vert, arrow_back, chevron_right, expand_more, close, check, check_circle, restore, gpp_maybe, cloud, add_a_photo, sell, palette, graphic_eq, download, sd_storage, fiber_manual_record, touch_app, wb_sunny, ac_unit, nightlight, local_fire_department, rocket_launch, public, looks_one, route, explore, calendar_month, groups, star`. Use MAUI FontImageSource or a native vector set.
- **Fonts**: Chakra Petch, Space Grotesk, Inter (Google Fonts, OFL). Bundle as MAUI custom fonts.
- **No raster image assets** — all visuals are drawn. User-added ride photos (Summary "Photo") come from the device.

## Files
- `ESK8 Tracker.dc.html` — the complete interactive design prototype (all screens + states). Open in a browser to explore; use the in-app Tweaks to switch speedometer/summary variants and units.
- `android-frame.jsx` — the device-bezel wrapper used only to present the mock (not part of the app).
- `support.js` — runtime for the prototype format (ignore for implementation).
- `screenshots/` — reference captures (if included).

Original brief: `ESK8_TRACKER_DESIGN_HANDOFF.md` (in the project `uploads/`).
