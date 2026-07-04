# ESK8 Ride Tracker — Design Handoff

**For:** Claude Design
**Platform:** Android (phone-first, portrait primary)
**Architecture:** Local-first. On-device storage only. No login, no account, no server. Fully functional offline.
**Deliverable from you (Design):** Screen designs, component specs, states, and a token system for a production-ready Android app.

> This document is the design brief. It defines *what* each screen does, the states it must handle, and the visual language. It intentionally leaves implementation to the build phase — but every screen, state, and edge case here is required for a production release, so treat the screen inventory as a completeness checklist, not a menu.

---

## 1. Product in one line

A ride tracker for electric skateboards that turns every ride into a glanceable live dashboard and a saved, mappable, analyzable record — with lifetime stats, a board garage, and range estimates — running entirely on the phone with no account.

**Primary user:** An esk8 rider (commuter or session rider) who mounts their phone or pockets it and wants speed/distance/battery awareness live, plus a history they own. GPS-based (works with *any* board — no board hardware/Bluetooth dependency in v1).

**The job of the app:** Never lose a ride, be readable at 30 km/h in daylight, and make the data feel like *their* riding story.

---

## 2. Design principles (the north stars)

1. **Glanceable at speed.** The live screen must be legible in a sub-second glance while moving. Big numbers, huge contrast, no clutter. If a rider has to focus to read it, it failed.
2. **Thumb-first, one-handed.** Every action a rider takes *during* a ride (pause, resume, stop, lock) must sit in the bottom third, be finger-sized (≥56dp targets), and tolerate gloves and bumps. No small controls near the top while riding.
3. **Dark-first, outdoor-readable.** The app lives outside, often at night. Dark is the default theme, tuned for OLED and for sunlight legibility (true contrast, not muted grey-on-grey). A light theme exists but is secondary.
4. **Trust the save.** A rider must never lose a ride to a crash, a killed process, or a dead battery. Recording is durable and recoverable; the summary/save step is unmissable. This is the #1 trust factor — competitor reviews are full of "it didn't save my ride."
5. **Local-first & private.** Data is theirs, on their device. No sign-in wall, no "sync to see your rides." Export is the backup story; make it visible, not buried.
6. **Safety-aware, never safety-theater.** Reduce reasons to look at the phone (voice cues, auto-pause). Don't gamify reckless speed. Records are celebrated soberly.

---

## 3. Visual language

Dark-first. The identity should feel like a **telemetry instrument cluster** — the world of VESC dashboards, motorsport gauges, and data logs — not a generic fitness app. Precise, technical, high-contrast, with one bold expressive element: speed rendered as color.

### 3.1 Signature element — the "velocity ramp"

**This is the one thing the app is remembered by.** A single speed→color gradient, used *identically* everywhere speed appears:

- the **speedometer** arc/fill on the live dashboard,
- the **route polyline** on every map (the "speed-colorized map"),
- the **speed graph** line/area in ride detail,
- speed-related accents (top-speed badge, PR highlights).

The ramp maps to the rider's *current board's top speed* (from Garage), so the same colors mean the same relative effort across boards. Because color is reserved for speed as a semantic, avoid using ramp colors decoratively elsewhere.

```
Velocity ramp (slow → fast)
  cruise    flow     push     send
  #2E7CF6 → #22C55E → #F5C51E → #F03E3E
  (blue)    (green)   (amber)   (red)
```
Design the interpolation as smooth (not 4 hard bands) but make the four stops legible on a map at a glance. Provide a subtle glow/edge on the polyline so red segments read against a dark map.

### 3.2 Core palette (dark theme)

```
--bg           #0B0E11   near-black, cool (app background)
--surface      #151A1F   cards, sheets
--surface-2    #1E252B   elevated / pressed
--line         #2A333B   hairline dividers, gauge ticks
--text         #F2F5F7   primary text & the big numerics
--text-muted   #8A97A2   labels, captions, secondary
--brand        #4C8DFF   interactive/brand (buttons, links, active nav)
--go           #22C55E   start / positive
--stop         #F03E3E   stop / destructive / high-speed
--warn         #F5C51E   caution (low battery, GPS weak)
```
Light theme: derive a legible inversion; keep the velocity ramp identical (it's semantic). Support Material You dynamic color as an *optional* accent override for `--brand` only — never let it override the velocity ramp or the go/stop semantics.

### 3.3 Typography

Give the numerics a real instrument character; keep body text clean and quiet.

- **Data / numerics (the hero):** a technical face with **tabular figures** so digits don't shift as speed changes — e.g. `Azeret Mono`, `JetBrains Mono`, or `Chakra Petch` (motorsport feel). This is the face for the giant speed readout, stat tiles, and graph axes.
- **Display / headings:** `Space Grotesk` (or similar grotesque with slight character) for screen titles and section heads.
- **Body / UI:** `Inter` for labels, list rows, settings, longer text.

Type scale: the live speed readout is deliberately enormous (target ~40–52% of screen height in the active state). Everything else steps down sharply from it. Use weight and size, not color, to establish hierarchy — color belongs to the velocity ramp.

### 3.4 Iconography, shape, motion

- Material 3 (Material Symbols), rounded-but-precise. Medium corner radius (12–16dp) on cards; the primary start button is a large pill or circle.
- **Motion is functional, not decorative.** Worth animating: the idle→active transition (dashboard "powers on"), the speedometer needle/fill responding to speed, the route drawing in on the summary, auto-pause freeze/thaw. Respect reduced-motion. No confetti-style flourishes on the live screen ever.

---

## 4. Information architecture

Bottom navigation, 5 destinations. The **Ride** tab owns a state machine rather than being a static screen.

```
Bottom Nav
├── Ride        ← default / home
│    ├─ state: IDLE      (ready to ride)
│    ├─ state: ACTIVE    (live dashboard)
│    ├─ state: PAUSED    (auto or manual)
│    └─ state: SUMMARY   (post-ride save)  ← modal-ish over Ride
├── History     ← list of saved rides → Ride Detail
├── Garage      ← boards / setups → Board Editor
├── Stats       ← lifetime, records, charts, achievements
└── Settings    ← units, voice, auto-pause, data, theme, about

First-run only:
Onboarding & Permissions  ← full-screen flow, precedes nav
```

**Rule:** while a ride is ACTIVE or PAUSED, the bottom nav should be suppressed or locked to the Ride tab (with a clear affordance to peek at the map). A rider mid-ride should never accidentally tab away and think tracking stopped.

---

## 5. Screens

Each screen: **purpose → layout → key elements → states → interactions.** ASCII wireframes are directional, not pixel-accurate — establish the real hierarchy in the visual design.

### 5.1 Onboarding & Permissions (first run only)

**Purpose:** Get the rider to a trackable state, and — critically on Android — earn the background-location and battery-optimization permissions with context, so tracking survives a locked screen in a pocket.

**Sequence (3–4 light screens, skippable where legal):**
1. **Welcome** — one sentence of value, one illustration. "Track every ride. All on your phone."
2. **Location priming** — explain *why* before the OS dialog: "We use GPS to draw your route and measure speed. Nothing leaves your phone." Then trigger the OS permission. Handle "While using the app" vs "Allow all the time."
3. **Background & battery** — the make-or-break screen. Explain that Android may kill tracking to save battery, and walk them to the battery-optimization exemption with a single primed CTA. Show a clear "why" (a ride silently stopping = lost data).
4. **Set up first board (optional)** — light prompt to add a board now or later ("You can skip and add boards in the Garage").

**States:** permission granted / denied / partially granted (foreground only). For denied, design a persistent but calm banner on the Ride idle screen explaining what's limited and a one-tap route to fix it.

---

### 5.2 Ride — IDLE (home / ready)

**Purpose:** One-tap start. Ambient sense of your riding so far. Confirm you're ready (GPS + board selected).

```
┌─────────────────────────────┐
│  ●GPS strong      🛹 Street 1│  ← GPS status • active board chip
│                             │
│      Lifetime               │
│      1,284 km   ·   96 rides│  ← quiet lifetime teaser
│                             │
│                             │
│         ╭───────────╮       │
│         │   START   │       │  ← huge go button (--go)
│         │    RIDE   │       │
│         ╰───────────╯       │
│                             │
│  Last ride · Tue            │
│  ┌───────────────────────┐  │
│  │ 12.4 km · 34min · ▁▃▅▇ │  │  ← last-ride card w/ mini speed spark
│  └───────────────────────┘  │
├─────────────────────────────┤
│ Ride  History Garage Stats ⚙│
└─────────────────────────────┘
```

**Key elements:** GPS-signal indicator (acquiring / weak / strong), active-board chip (tap → switch board), giant START RIDE, lifetime odometer teaser, last-ride quick card.

**States:**
- **GPS acquiring** — START is available but shows a subtle "finding satellites" state; warn if the rider starts before a fix.
- **No board in garage** — board chip becomes "Add a board" (optional; riding without one is allowed, range estimate just won't show).
- **Permission limited** — calm banner (see 5.1).
- **Resume interrupted ride** — if the app was killed mid-ride, on next open surface a "Resume unsaved ride?" recovery card at top. This is part of the "trust the save" promise.

**Interaction:** START → transitions (with a "powering on" motion) to ACTIVE.

---

### 5.3 Ride — ACTIVE (live dashboard)

**Purpose:** The heart of the app. Glanceable, at speed, one-handed. This screen is judged in a fraction of a second.

```
┌─────────────────────────────┐
│  ⟲ elapsed 00:12:47     🔒   │  ← elapsed • lock-glance toggle
│                             │
│                             │
│         28                  │  ← ENORMOUS speed number
│         km/h                │     (color tint follows velocity ramp)
│    ◟▁▃▅▇▇▅▃ speedo arc ▇◞   │  ← gauge arc filled on velocity ramp
│                             │
│  ┌────────┐ ┌────────┐      │
│  │DISTANCE│ │TOP SPEED│     │  ← secondary stat tiles
│  │ 6.2 km │ │ 41 km/h │     │     (tabular numerics)
│  └────────┘ └────────┘      │
│  ┌────────┐ ┌────────┐      │
│  │ RANGE  │ │ AVG     │     │
│  │ ~14 km │ │ 22 km/h │     │
│  └────────┘ └────────┘      │
│                             │
│   [   PAUSE   ]   [ ▣ MAP ] │  ← thumb-zone controls
│        ═══ STOP (hold) ═══   │  ← stop requires hold-to-confirm
└─────────────────────────────┘
```

**Key elements:**
- **Speed** — the dominant element; digits tabular so the layout never jitters; whole-number readout tinted along the velocity ramp.
- **Speedometer arc** — fills along the ramp, scaled to the active board's top speed.
- **Stat tiles** — Distance, Top speed, Avg speed, Elapsed, **Range estimate** (only if board specs exist). Let the rider pick which 4 are shown (see customization note).
- **Controls (thumb zone):** big PAUSE, MAP peek, and a **hold-to-stop** (prevents accidental end).
- **Lock/glance toggle** — a minimal, ultra-high-contrast mode: just speed + distance, dimmed chrome, tap-to-wake. For pocket/mount riding.

**States:**
- **Riding** (as above).
- **Glance/lock mode** — stripped to 1–2 values, maximum contrast, prevents accidental touches.
- **GPS lost mid-ride** — non-blocking warn banner ("GPS weak — still recording"), speedometer shows last-known/greyed, distance holds. Never stop recording silently; never throw the rider to an error screen mid-ride.
- **Auto-paused** — see 5.4.

**Customization:** rider chooses which metrics fill the stat tiles (Settings or long-press a tile). Mirror Metr's "pick your cells" idea — riders disagree on what matters.

**Voice cues:** at rider-set intervals (every X km/mi or Y min), announce distance + avg speed (+ optionally top speed / range) to earbuds/helmet. Design the *settings* for this (5.9); the cue itself is audio, but show a small "audio cues on" indicator on the dashboard.

---

### 5.4 Ride — PAUSED (auto & manual)

**Purpose:** Make it unmistakable that tracking is frozen, and effortless to resume. Support both manual pause and **smart auto-pause** (stopped at a light → freezes; rolls again → resumes).

```
┌─────────────────────────────┐
│         ‖ PAUSED            │  ← unmistakable freeze state
│      (auto — stopped)       │     tells rider WHY it paused
│                             │
│         28  → frozen        │  ← last speed, de-emphasized/greyed
│                             │
│   distance & timers HELD    │  ← visibly not counting
│                             │
│      ╭───────────────╮      │
│      │    RESUME     │      │  ← giant resume (or auto on roll)
│      ╰───────────────╯      │
│        ═══ STOP (hold) ═══  │
└─────────────────────────────┘
```

**Key elements:** big "PAUSED" with reason (manual vs auto), frozen/greyed metrics so it's obvious the clock stopped, giant RESUME, hold-to-stop.

**Auto-pause behavior (design the feedback, not the algorithm):** when speed drops below a threshold for N seconds → auto-pause with a gentle haptic + the freeze animation. On movement → auto-resume with a haptic and the "thaw." Sensitivity is configurable in Settings (Off / Low / Normal / High). Elapsed "moving time" excludes paused time; also retain "total time" for the summary.

---

### 5.5 Ride — SUMMARY (post-ride save)

**Purpose:** The payoff and the save. Show the ride beautifully, let the rider name/tag it, and save durably. **This step must be unmissable** — auto-save a draft immediately on STOP so nothing is lost even if they leave.

```
┌─────────────────────────────┐
│  ✕                    Save ✓ │
│  ┌───────────────────────┐  │
│  │   speed-colorized     │  │  ← HERO: route on velocity ramp
│  │        route map      │  │
│  └───────────────────────┘  │
│  Evening commute        ✎   │  ← editable ride name
│  🛹 Street 1   🏷 Commute    │  ← board • tags
│                             │
│  12.4 km   34:02   22 km/h  │  ← distance • moving time • avg
│  ↑ 41 km/h top   ↑ 86m gain │  ← top speed • elevation gain
│                             │
│  ┌───────────────────────┐  │
│  │ speed graph (ramp)    │  │  ← speed vs distance, ramp-colored
│  └───────────────────────┘  │
│  ┌───────────────────────┐  │
│  │ 🏅 New top speed!     │  │  ← record/achievement banner (if any)
│  └───────────────────────┘  │
│  Notes… ☁ 14°C, clear       │  ← free notes + captured weather
│  [ Add photo ]  [ Share ]   │
└─────────────────────────────┘
```

**Key elements:** hero colorized route map, editable name, board + tags (Commute / Session / Group / Custom), the headline stats, speed graph, any record/achievement earned, notes, captured weather at ride time & place, add photo, share (render an image card), Save / Discard.

**States:** normal save; **discard** (confirm — destructive); **no-GPS ride** (no map — show stats-only layout gracefully rather than a broken map); very short/accidental ride (offer quick discard).

---

### 5.6 History

**Purpose:** The rider's owned diary. Scannable, filterable, quick to open any ride.

```
┌─────────────────────────────┐
│  History            🔍  ⇅    │  ← search • sort/filter
│  This month · 214 km · 18 rd │  ← aggregate header
│  ┌───────────────────────┐  │
│  │▁▃▅ 12.4km 34m 22km/h Tue│ │  ← ride card: thumb route +
│  └───────────────────────┘  │     key stats + date
│  ┌───────────────────────┐  │
│  │▁▇▅  8.1km 19m 25km/h Mon│ │
│  └───────────────────────┘  │
│  ┌───────────────────────┐  │
│  │▃▅▇ 21.0km 58m 21km/h Sun│ │
│  └───────────────────────┘  │
│           …                 │
└─────────────────────────────┘
```

**Key elements:** aggregate header (toggle week/month/year/all), ride cards (mini colorized route thumbnail, distance, moving time, avg speed, date, board), sort (date/distance/speed), filter (board, tag, date range), search by name.

**States:** **empty** (first-run: "No rides yet — hit Start Ride to log your first," inviting not apologetic); loading; grouped by day/week headers as you scroll.

**Interaction:** tap card → Ride Detail. Swipe or long-press → quick actions (rename, delete, export single ride).

---

### 5.7 Ride Detail

**Purpose:** The full, analyzable record of one ride. Reuses the Summary layout, read-mode.

**Adds beyond Summary:**
- Full-screen colorized map with scrub: drag along the route and the speed/elevation/time readout follows the dot.
- **Layered graphs** over distance or time: speed, elevation, and (future) any imported telemetry — toggleable layers (competitor reviewers specifically praise layered charts).
- Elevation profile.
- Splits (per-km/mi pace & top speed).
- Board used, tags, notes, photos, weather.
- Actions: edit name/tags/notes, re-share, **export this ride (GPX/JSON)**, delete.

**States:** GPS-less ride → stats + graphs, no map. Deleted → confirm (destructive).

---

### 5.8 Garage (boards / setups)

**Purpose:** Model the rider's real quiver. Boards carry the battery specs that power range estimates and the top-speed that scales the velocity ramp. Per-board lifetime stats make it personal.

```
┌─────────────────────────────┐
│  Garage                  +   │
│  ┌───────────────────────┐  │
│  │ 🛹 Street 1     ● active│ │  ← board card
│  │ 10S4P · 12Ah · 41km/h  │  │     (specs summary)
│  │ 640 km on this board   │  │  ← per-board odometer
│  └───────────────────────┘  │
│  ┌───────────────────────┐  │
│  │ 🛹 AT Beast            │  │
│  │ 12S · Pneumatics · 45  │  │
│  │ 210 km                 │  │
│  └───────────────────────┘  │
└─────────────────────────────┘

Board Editor
  • Name, photo/color
  • Wheel type (Street urethane / All-terrain pneumatic / Custom)
  • Battery: cells (e.g. 10S4P) OR voltage + capacity (Wh or Ah)
  • Top speed (used to scale the velocity ramp & speedometer)
  • Notes
  • Set as active · Delete
```

**Key elements:** board list with active indicator and per-board odometer; add/edit board; set-active (also switchable from the idle screen chip).

**States:** empty ("Add your first board — powers range estimates and speed zones"); active-board deletion (reassign or warn).

---

### 5.9 Stats & Achievements

**Purpose:** The lifetime story and the retention engine. Lifetime odometer, personal records, trends, and an achievements system.

```
┌─────────────────────────────┐
│  Stats                       │
│  ┌───────────────────────┐  │
│  │  LIFETIME              │  │
│  │  1,284 km · 96 rides   │  │
│  │  64h moving · ↑ 9.2km  │  │  ← total climb
│  └───────────────────────┘  │
│  Records                     │
│  ⚡ Top speed 47 km/h        │
│  📏 Longest ride 38 km       │
│  ⏱ Longest session 1:52     │
│  🔥 Best streak 11 days      │
│  ┌───────────────────────┐  │
│  │ distance / week chart │  │  ← trend chart
│  └───────────────────────┘  │
│  Achievements   12 / 40 ●●●○ │
│  ┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐   │  ← badge grid (earned/locked)
│  └──┘└──┘└──┘└──┘└──┘└──┘   │
└─────────────────────────────┘
```

**Key elements:** lifetime odometer block, records list, trend chart (distance/rides per week/month, toggle), achievements grid.

**Achievements** — categories worth designing badges for: **Distance milestones** (first 10/100/1000 km), **Speed** (break your own top speed, hit speed tiers), **Consistency/streaks** (ride N days in a row, weekly goals), **Exploration** (rides in N distinct areas), **Session** (long single rides, night rides, elevation climbed), **Collection** (rides on N different boards). Each: icon, title, plain-language criteria, earned date, locked/earned/in-progress states with a progress ring. Earning one surfaces a sober celebration on the ride summary — not a slot-machine.

**States:** early user (mostly locked, progress-forward framing); tap badge → detail sheet (criteria + progress + earned date).

---

### 5.10 Settings

**Purpose:** Control the behaviors that make riding smooth, and own your data.

Sections:
- **Units** — Metric / Imperial (instant, app-wide).
- **Live dashboard** — choose which 4 stat tiles show; default glance-mode metric.
- **Voice cues** — on/off; interval (every X km/mi or Y min); what to announce (distance, avg, top, range); volume ducking.
- **Auto-pause** — Off / Low / Normal / High sensitivity.
- **Tracking & battery** — background tracking status, re-open the battery-optimization exemption, GPS accuracy (Balanced / High), foreground-service notification style.
- **Map** — style (dark/satellite/standard), always-colorize-by-speed toggle.
- **Data** — **Export all** (GPX/JSON), **Export single** (from ride detail), **Import**, storage used, **Backup reminder** setting. This is the local-first "your data is safe" story — make it prominent, not buried.
- **Theme** — Dark (default) / Light / System; optional Material You accent.
- **About** — version, privacy statement ("Everything stays on your device"), open-source licenses.

---

## 6. Signature components (design once, reuse)

1. **Velocity speedometer** — big number + arc, tinted along the velocity ramp, scaled to active board top speed. Idle/active/paused variants.
2. **Stat tile** — label (muted, small) + tabular numeric value + unit. The dashboard grid unit; also used in summary/detail.
3. **Ride card** — mini colorized route thumbnail + distance/time/avg + date + board. History's atom.
4. **Colorized route map** — the polyline renders on the velocity ramp; needs a legend and a scrub interaction (detail) and a static thumbnail (cards, share).
5. **Speed / elevation graph** — ramp-colored line/area, scrubbable, layer toggles.
6. **Board card & editor** — specs summary + per-board odometer + active state.
7. **Achievement badge** — earned / in-progress (ring) / locked, with detail sheet.
8. **Primary action button** — the START (go) and hold-to-STOP (destructive) treatments; oversized, thumb-zone.
9. **Foreground-service notification** — a designed persistent Android notification shown during tracking (live distance/time, tap→return, pause/stop actions). This is a real surface a rider sees constantly; design it, don't leave it default.
10. **Status chips/banners** — GPS strength, permission-limited, auto-pause reason, GPS-lost warn. Calm, informative, never alarmist.

---

## 7. Key flows

**Record a ride:**
`IDLE → (START) → ACTIVE ⇄ PAUSED (manual/auto) → (hold STOP) → SUMMARY → (Save) → History` — with a draft auto-saved at STOP and a kill-recovery card on relaunch.

**Auto-pause:** rolling → stop ≥N s → freeze (haptic) → roll → thaw (haptic). Moving time excludes pauses.

**Review & analyze:** `History → Ride Detail → scrub map / toggle graph layers / view splits → export or share`.

**Set up a board:** `Garage → + → specs → Set active` (also switchable from idle chip). Board top speed scales the ramp; battery specs enable range estimate.

**Own your data:** `Settings → Data → Export all (GPX/JSON)`; single-ride export from Detail; import to restore.

---

## 8. Data model (for design context, not schema)

Design should know what entities exist so screens stay consistent:

- **Ride** — id, name, date/time, board ref, path (points: lat/lng/elevation/timestamp/speed), distance, moving time, total time, avg speed, top speed, elevation gain, tags[], notes, weather snapshot, photos[], derived splits, achievements earned.
- **Board** — id, name, photo/color, wheel type, battery (cells or voltage+capacity), top speed, per-board odometer, notes, active flag.
- **Lifetime stats** — total distance, total rides, total moving time, total climb, records (top speed, longest ride, longest session, best streak).
- **Achievement** — id, category, title, criteria (plain language), state (locked/in-progress/earned), progress, earned date.
- **Settings** — units, dashboard tiles, voice-cue config, auto-pause sensitivity, GPS accuracy, map style, theme.

---

## 9. States & edge cases (production checklist)

Every screen must design for:
- **Empty** — no rides / no boards / no achievements. Direction, not apology; an invitation to act.
- **GPS acquiring / weak / lost** — before, during, after a ride. Never block the rider; never lose data.
- **Permission denied / foreground-only** — calm recovery path.
- **App killed mid-ride** — recovery card, draft restore.
- **No-GPS ride** — stats-only summary/detail (no broken map).
- **Very short/accidental ride** — easy discard.
- **Low phone battery** — optional warn; ensure recording persists.
- **Storage full / large history** — graceful list performance, export prompt.
- **Light theme** — full parity, ramp unchanged.
- **Accessibility floor** — large hit targets, visible focus, reduced motion respected, sufficient contrast in *both* themes, TalkBack labels on all controls, dynamic font scaling doesn't break the dashboard.

---

## 10. Android-specific design notes

- **Foreground service + notification is a designed surface.** Tracking runs as a foreground service; its persistent notification (live stats + pause/stop actions) is seen every ride. Design it.
- **Battery-optimization exemption** is the difference between reliable and broken background tracking. The onboarding must earn it with context; Settings must let the rider re-check it.
- **Permissions:** location (fine), background location, notifications (Android 13+), physical activity (optional, aids auto-pause). Prime each with a "why" before the OS dialog.
- **Material 3** as the base system; Material You dynamic color optional and scoped to `--brand` only.
- **Home-screen widget** (nice-to-have, v1.1): lifetime odometer + one-tap "Start ride." Design a small + medium size.
- **Quick-tile** (later): start a ride from the notification shade.
- **Predictive back / gesture nav:** confirm-on-back during an active ride so a back-swipe never silently ends tracking.

---

## 11. Build order (prioritization)

**v1 (production release):** Onboarding/permissions · Ride idle/active/paused/summary · durable save + kill recovery · History + Ride Detail (map, graph, scrub) · Garage · units · dark theme · background tracking + foreground notification · export. This is the trustworthy daily-use core.

**v1.1:** Smart auto-pause tuning · voice cues · speed-colorized map polish · range estimator · achievements + records · home-screen widget · light theme.

**Later:** community/shared routes · route PR/ghost comparison · VESC/Bluetooth telemetry (adds a hardware-compatibility matrix — deliberately out of the GPS-only v1) · Wear OS companion.

---

## 12. What I need back from Design

1. Token system realized (color incl. the velocity ramp, type scale, spacing, elevation, motion) in both themes.
2. High-fidelity designs for every screen in §5, in their key states from §9.
3. The 10 signature components in §6, with variants/states.
4. The two hero moments nailed: the **live dashboard** (glanceability) and the **colorized route summary** (the payoff).
5. The foreground-service notification and onboarding permission screens — the reliability surfaces that competitors neglect and riders punish.

**One risk worth taking (justified):** commit fully to the telemetry-instrument identity and the velocity ramp as a semantic color language — resist making this look like a generic fitness tracker with a map. Speed *is* the brand; let color carry it everywhere and keep everything else quiet.
