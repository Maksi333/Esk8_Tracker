# Esk8 Tracker — v1 Design

**Date:** 2026-07-02
**Status:** Approved by user (all four sections) on 2026-07-02

## Purpose

A personal electric-skateboard companion app: record rides live with GPS (map, speed,
distance) and keep a local ride history with a stats dashboard. Single user, no account,
no cloud — everything lives on the phone.

## Goals (v1)

- Live ride recording: position on a moving map, current speed, distance, duration.
- Pocket-proof: recording continues with the screen off or the app backgrounded.
- Ride history: list of past rides; detail page with route on a map and per-ride stats.
- Stats dashboard: all-time totals, records, monthly breakdown, per-board totals.
- Multiple boards: rides are tagged to a board.
- Metric units everywhere (km, km/h).

## Non-goals (v1)

- Cloud sync, accounts, or sharing.
- Ride notes / custom ride names.
- GPX export (the data model keeps all GPS points, so this is easy to add later).
- Maintenance/battery tracking.
- Auto-pause detection (pause is manual).
- iOS support (the MAUI targets stay in the csproj, but nothing is built or tested for iOS).

## Platforms

- **Android** is the real target — built, deployed, and tested on the user's phone.
- **Windows** stays compilable as a dev preview: History/Stats/Boards work against the
  local database; the Ride tab shows "recording not supported on this platform".

## App structure

MAUI Shell with four bottom tabs:

1. **Ride** — live map (Mapsui, OpenStreetMap tiles) filling most of the screen, position
   centered, route polyline drawn as it grows. Below the map: current speed (large),
   distance, duration, and a GPS-signal indicator. Buttons: Start / Pause / Stop.
   A board picker is shown before start, defaulting to the last-used board.
2. **History** — rides newest-first: date, board name, distance, duration, avg/top speed.
   Tap → ride detail page: full route on a map + the ride's stats.
3. **Stats** — all-time totals (distance, ride count, total riding time), records
   (top speed ever, longest ride by distance), per-month totals, per-board totals.
4. **Boards** — add / rename / delete boards. Deleting archives the board: it disappears
   from the picker but its name still shows on historical rides.

## Ride recording architecture

- **Android foreground service** (`Platforms/Android`) with a persistent
  "Recording ride…" notification. Declares `FOREGROUND_SERVICE_LOCATION`
  (required on Android 14+) and requests fused/GPS location updates at ~1 s intervals.
- **RideRecorder** (shared, platform-agnostic singleton, DI-registered) owns all logic:
  - State machine: `Idle → Recording ⇄ Paused → Idle` (Stop finalizes).
  - Accumulates track points; computes distance (haversine), current speed
    (GPS-provided speed, falling back to position delta), average speed over moving
    time, top speed, and moving time (time while speed > 1 km/h).
  - **Fix filtering:** drop fixes with horizontal accuracy worse than 30 m; drop fixes
    implying > 120 km/h (junk spikes must not pollute top-speed records).
  - **GPS-loss handling:** no accepted fix for > 5 s → UI indicator shows signal lost;
    a gap > 30 s between accepted fixes does not accumulate distance across the gap.
  - Raises events/observable state; ViewModels subscribe. The service and the UI never
    talk to each other directly — both only know RideRecorder.
- **Crash safety:** buffered points are flushed to SQLite every 10 seconds (or every
  10 points, whichever first). The ride row is created at Start with `EndedAt = null`;
  Stop fills in the summary. On app launch, any ride with `EndedAt = null` is finalized
  from its saved points and marked recovered.
- **Permissions:** location (while-in-use) and notifications requested on first use,
  with an explanatory prompt and a deep link to app settings if denied.

## Data model (SQLite, local only)

- **Board:** `Id`, `Name`, `CreatedAt`, `IsArchived`.
- **Ride:** `Id`, `BoardId`, `StartedAt`, `EndedAt` (null while in progress),
  `DistanceMeters`, `MovingSeconds`, `AvgSpeedMps`, `MaxSpeedMps`, `WasRecovered`.
- **TrackPoint:** `Id`, `RideId`, `Timestamp`, `Latitude`, `Longitude`, `SpeedMps`,
  `AccuracyMeters`, `AltitudeMeters` (stored for future use, unused in v1 UI).

Dashboard numbers are computed with queries over `Ride` — no derived data is stored twice.

## Tech stack

- .NET MAUI (net10.0), single project, MVVM via **CommunityToolkit.Mvvm**.
- **sqlite-net-pcl** (+ SQLitePCLRaw bundle) for storage.
- **Mapsui** for the map — chosen over Microsoft.Maui.Controls.Maps because it needs no
  Google Cloud account/API key, and over a custom canvas because street context matters
  for a live riding map. OSM tiles download over the network and are cached locally.
- Calculation-heavy logic (distance math, stat aggregation, recorder state machine)
  lives in plain classes with no MAUI dependencies so it is unit-testable.

## Error handling summary

| Situation | Behavior |
|---|---|
| GPS signal lost mid-ride | Indicator on Ride tab; no distance accumulated across gaps > 30 s |
| Inaccurate / spike fixes | Dropped (accuracy > 30 m, implied speed > 120 km/h) |
| App killed / crash mid-ride | Ride recovered and finalized from flushed points on next launch |
| Location permission denied | Explanation + link to system settings; recording blocked until granted |
| Map tiles unavailable (offline) | Map shows cached tiles / blank background; recording is unaffected |

## Testing

- **Unit tests (xUnit, separate test project):** haversine distance, speed/moving-time
  stats, fix filtering thresholds, recorder state machine, ride recovery finalization,
  dashboard aggregation queries (against an in-memory/temp SQLite database).
- **On-device manual verification:** GPS accuracy, foreground-service behavior with
  screen off, notification, permission flows, map rendering, battery sanity check.

## Risks / to verify at planning time

- Mapsui version compatibility with .NET 10 MAUI — verify the current stable package
  targets net10.0 before committing to a version.
- Android 14+/15 foreground-service-location policy details (manifest declarations,
  runtime prompt ordering).
