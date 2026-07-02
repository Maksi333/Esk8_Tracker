# Esk8 Tracker v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A .NET MAUI Android app that records e-skateboard rides live (GPS map, speed, distance) into a local SQLite database, with ride history, a stats dashboard, and multi-board support.

**Architecture:** All calculation and persistence logic lives in a plain `net10.0` class library (`Esk8_Tracker.Core`) that is fully unit-tested and has no MAUI dependencies. The MAUI app is a thin shell: ViewModels bind Core to XAML, an Android foreground service feeds GPS fixes into the shared `RideRecorder` singleton, and Mapsui renders the live/replay maps. The service and the UI never talk directly — both only know `RideRecorder`.

**Tech Stack:** .NET MAUI (net10.0, SDK 10.0.301), CommunityToolkit.Mvvm, sqlite-net-pcl, Mapsui.Maui, xUnit.

**Spec:** `docs/superpowers/specs/2026-07-02-esk8-tracker-design.md` (approved)

## Global Constraints

- Metric units everywhere: km, km/h. Speeds stored in m/s, distances in meters; formatting only at display time.
- All timestamps stored as UTC; converted to local time only for display.
- Fix filtering thresholds (from spec, use exactly these): drop fixes with horizontal accuracy > 30 m; drop fixes implying > 120 km/h (33.333 m/s); segment gap threshold 30 s (no distance across bigger gaps); moving threshold 1 km/h (0.2778 m/s); GPS-signal-lost indicator after 5 s without an accepted fix.
- Crash-safety flush: buffered points written to SQLite every 10 accepted points or 10 seconds, whichever first.
- "Duration" in any UI means moving time, not wall-clock elapsed.
- Board deletion is archival: `IsArchived = true`; archived boards keep their name in history but leave the picker.
- Android is the deploy target; the Windows (net10.0-windows10.0.19041.0) build must stay compilable as a dev preview with recording disabled.
- No cloud, no accounts, no telemetry. Local SQLite only.
- Windows PowerShell 5.1 is the shell: chain commands with `;`, never `&&`.
- All builds/tests run from the repo root: `C:\Users\simon\Desktop\Git\Esk8_Tracker\Esk8_Tracker`.

## File Structure (end state)

```
/ (repo root)
├── Esk8_Tracker.slnx                      # solution, updated to reference all 3 projects
├── src/
│   ├── Esk8_Tracker/                      # MAUI app (moved from repo root)
│   │   ├── Esk8_Tracker.csproj
│   │   ├── MauiProgram.cs                 # DI wiring
│   │   ├── AppShell.xaml(.cs)             # 4 tabs
│   │   ├── Views/                         # RidePage, HistoryPage, RideDetailPage, StatsPage, BoardsPage
│   │   ├── ViewModels/                    # one per page
│   │   ├── Maps/RideMap.cs                # all Mapsui code isolated here
│   │   ├── Services/IRideRecordingController.cs   # platform start/stop abstraction
│   │   └── Platforms/Android/
│   │       ├── AndroidManifest.xml        # location + FGS permissions
│   │       ├── RideRecordingService.cs    # foreground service + LocationManager
│   │       └── AndroidRideRecordingController.cs
│   └── Esk8_Tracker.Core/                 # plain net10.0 classlib, no MAUI deps
│       ├── Esk8_Tracker.Core.csproj
│       ├── Models/Board.cs, Ride.cs, TrackPoint.cs
│       ├── GpsFix.cs                      # input DTO for the recorder
│       ├── GeoMath.cs                     # haversine
│       ├── FixFilter.cs                   # accuracy / speed-spike acceptance
│       ├── StatsAccumulator.cs            # incremental distance/speed/moving-time math
│       ├── RideRecorder.cs                # state machine, buffering, events
│       ├── Format.cs                      # m/s→km/h etc. display strings
│       └── Data/
│           ├── IRideStore.cs              # recorder's persistence seam
│           ├── Esk8Database.cs            # sqlite-net-pcl; CRUD, recovery, aggregates
│           └── DashboardStats.cs
└── tests/
    └── Esk8_Tracker.Core.Tests/           # xunit, net10.0
        ├── Esk8_Tracker.Core.Tests.csproj
        ├── GeoMathTests.cs
        ├── FixFilterTests.cs
        ├── StatsAccumulatorTests.cs
        ├── RideRecorderTests.cs
        ├── FormatTests.cs
        └── DatabaseTests.cs
```

Why the move to `src/`: the MAUI csproj currently sits at the repo root, where its SDK-style globs (`**/*.cs`, XAML, images) would swallow any sibling project's files. Moving it is a one-time `git mv` at day zero.

---

### Task 1: Solution restructure — src/ layout, Core library, test project

**Files:**
- Move: everything app-related from repo root into `src/Esk8_Tracker/` (all `*.xaml`, `*.xaml.cs`, `*.cs`, `Esk8_Tracker.csproj`, `Esk8_Tracker.csproj.user`, `Platforms/`, `Properties/`, `Resources/`)
- Create: `src/Esk8_Tracker.Core/Esk8_Tracker.Core.csproj`
- Create: `tests/Esk8_Tracker.Core.Tests/Esk8_Tracker.Core.Tests.csproj`
- Modify: `Esk8_Tracker.slnx`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: a building solution where `Esk8_Tracker` (MAUI) references `Esk8_Tracker.Core`, and `Esk8_Tracker.Core.Tests` references `Esk8_Tracker.Core`. Later tasks add code to these projects and run `dotnet test tests/Esk8_Tracker.Core.Tests` from the repo root.

- [ ] **Step 1: Move the app into src/Esk8_Tracker**

```powershell
New-Item -ItemType Directory -Force src\Esk8_Tracker
git mv App.xaml src/Esk8_Tracker/; git mv App.xaml.cs src/Esk8_Tracker/
git mv AppShell.xaml src/Esk8_Tracker/; git mv AppShell.xaml.cs src/Esk8_Tracker/
git mv MainPage.xaml src/Esk8_Tracker/; git mv MainPage.xaml.cs src/Esk8_Tracker/
git mv MauiProgram.cs src/Esk8_Tracker/
git mv Esk8_Tracker.csproj src/Esk8_Tracker/
git mv Platforms src/Esk8_Tracker/Platforms
git mv Properties src/Esk8_Tracker/Properties
git mv Resources src/Esk8_Tracker/Resources
Move-Item Esk8_Tracker.csproj.user src\Esk8_Tracker\ -ErrorAction SilentlyContinue
```

(`Esk8_Tracker.csproj.user` is untracked — plain `Move-Item`, not `git mv`. Old `bin/`, `obj/`, `.vs/` at the root are git-ignored; delete `bin` and `obj` so stale outputs don't confuse anyone: `Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue`.)

- [ ] **Step 2: Create the Core class library**

```powershell
dotnet new classlib -f net10.0 -o src/Esk8_Tracker.Core -n Esk8_Tracker.Core
Remove-Item src\Esk8_Tracker.Core\Class1.cs
```

Then overwrite `src/Esk8_Tracker.Core/Esk8_Tracker.Core.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Esk8_Tracker.Core</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="sqlite-net-pcl" Version="1.9.172" />
    <PackageReference Include="SQLitePCLRaw.bundle_green" Version="2.1.11" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create the test project**

```powershell
dotnet new xunit -f net10.0 -o tests/Esk8_Tracker.Core.Tests -n Esk8_Tracker.Core.Tests
Remove-Item tests\Esk8_Tracker.Core.Tests\UnitTest1.cs
dotnet add tests/Esk8_Tracker.Core.Tests reference src/Esk8_Tracker.Core
```

- [ ] **Step 4: Reference Core from the MAUI app**

```powershell
dotnet add src/Esk8_Tracker reference src/Esk8_Tracker.Core
```

- [ ] **Step 5: Update the solution file**

```powershell
dotnet sln Esk8_Tracker.slnx add src/Esk8_Tracker/Esk8_Tracker.csproj src/Esk8_Tracker.Core/Esk8_Tracker.Core.csproj tests/Esk8_Tracker.Core.Tests/Esk8_Tracker.Core.Tests.csproj
```

Then open `Esk8_Tracker.slnx` and remove any stale entry pointing at the old root-level `Esk8_Tracker.csproj` path (keep only the three new entries).

- [ ] **Step 6: Verify everything builds and the (empty) test suite runs**

```powershell
dotnet build src/Esk8_Tracker.Core --nologo
dotnet test tests/Esk8_Tracker.Core.Tests --nologo
dotnet build src/Esk8_Tracker -f net10.0-android --nologo
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 --nologo
```

Expected: all four succeed; `dotnet test` reports 0 tests, exit code 0. If the Android build fails with workload errors, stop and report — do not improvise workload installs.

- [ ] **Step 7: Commit**

```powershell
git add -A; git commit -m "chore: restructure to src/ layout with Core library and test project"
```

---

### Task 2: Models + GeoMath (haversine)

**Files:**
- Create: `src/Esk8_Tracker.Core/Models/Board.cs`, `src/Esk8_Tracker.Core/Models/Ride.cs`, `src/Esk8_Tracker.Core/Models/TrackPoint.cs`
- Create: `src/Esk8_Tracker.Core/GpsFix.cs`
- Create: `src/Esk8_Tracker.Core/GeoMath.cs`
- Test: `tests/Esk8_Tracker.Core.Tests/GeoMathTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `Esk8_Tracker.Core.Models.Board { int Id; string Name; DateTime CreatedAt; bool IsArchived }`
  - `Esk8_Tracker.Core.Models.Ride { int Id; int BoardId; DateTime StartedAt; DateTime? EndedAt; double DistanceMeters; double MovingSeconds; double AvgSpeedMps; double MaxSpeedMps; bool WasRecovered }`
  - `Esk8_Tracker.Core.Models.TrackPoint { int Id; int RideId; DateTime Timestamp; double Latitude; double Longitude; double SpeedMps; double AccuracyMeters; double? AltitudeMeters }`
  - `Esk8_Tracker.Core.GpsFix(DateTime TimestampUtc, double Latitude, double Longitude, double? SpeedMps, double AccuracyMeters, double? AltitudeMeters)` — record
  - `static double GeoMath.HaversineMeters(double lat1, double lon1, double lat2, double lon2)`

- [ ] **Step 1: Write the failing tests**

`tests/Esk8_Tracker.Core.Tests/GeoMathTests.cs`:

```csharp
using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class GeoMathTests
{
    [Fact]
    public void SamePoint_IsZero()
    {
        Assert.Equal(0.0, GeoMath.HaversineMeters(55.6761, 12.5683, 55.6761, 12.5683), 6);
    }

    [Fact]
    public void OneDegreeOfLongitudeAtEquator_IsAbout111Km()
    {
        // 2 * pi * 6371000 / 360 = 111194.93 m
        var d = GeoMath.HaversineMeters(0, 0, 0, 1);
        Assert.InRange(d, 111_100, 111_300);
    }

    [Fact]
    public void IsSymmetric()
    {
        var a = GeoMath.HaversineMeters(55.6761, 12.5683, 55.6867, 12.5700);
        var b = GeoMath.HaversineMeters(55.6867, 12.5700, 55.6761, 12.5683);
        Assert.Equal(a, b, 9);
    }

    [Fact]
    public void ShortHop_IsPlausible()
    {
        // ~0.0001 deg latitude is ~11.1 m
        var d = GeoMath.HaversineMeters(55.6761, 12.5683, 55.6762, 12.5683);
        Assert.InRange(d, 10.5, 11.7);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: FAIL to compile — `GeoMath` does not exist.

- [ ] **Step 3: Write the implementation**

`src/Esk8_Tracker.Core/GeoMath.cs`:

```csharp
namespace Esk8_Tracker.Core;

public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
```

`src/Esk8_Tracker.Core/GpsFix.cs`:

```csharp
namespace Esk8_Tracker.Core;

/// <summary>A single GPS fix as delivered by a platform location source.</summary>
public record GpsFix(
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    double? SpeedMps,
    double AccuracyMeters,
    double? AltitudeMeters);
```

`src/Esk8_Tracker.Core/Models/Board.cs`:

```csharp
using SQLite;

namespace Esk8_Tracker.Core.Models;

public class Board
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public bool IsArchived { get; set; }
}
```

`src/Esk8_Tracker.Core/Models/Ride.cs`:

```csharp
using SQLite;

namespace Esk8_Tracker.Core.Models;

public class Ride
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int BoardId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Null while the ride is in progress; set when finalized.</summary>
    public DateTime? EndedAt { get; set; }

    public double DistanceMeters { get; set; }

    public double MovingSeconds { get; set; }

    public double AvgSpeedMps { get; set; }

    public double MaxSpeedMps { get; set; }

    public bool WasRecovered { get; set; }
}
```

`src/Esk8_Tracker.Core/Models/TrackPoint.cs`:

```csharp
using SQLite;

namespace Esk8_Tracker.Core.Models;

public class TrackPoint
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int RideId { get; set; }

    public DateTime Timestamp { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double SpeedMps { get; set; }

    public double AccuracyMeters { get; set; }

    /// <summary>Stored for future use (elevation charts); unused in v1 UI.</summary>
    public double? AltitudeMeters { get; set; }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```powershell
git add src/Esk8_Tracker.Core tests; git commit -m "feat: add data models, GpsFix and haversine distance"
```

---

### Task 3: FixFilter + StatsAccumulator (the math heart)

**Files:**
- Create: `src/Esk8_Tracker.Core/FixFilter.cs`
- Create: `src/Esk8_Tracker.Core/StatsAccumulator.cs`
- Test: `tests/Esk8_Tracker.Core.Tests/FixFilterTests.cs`, `tests/Esk8_Tracker.Core.Tests/StatsAccumulatorTests.cs`

**Interfaces:**
- Consumes: `GpsFix`, `GeoMath.HaversineMeters` (Task 2).
- Produces:
  - `static class FixFilter` with `const double MaxAccuracyMeters = 30.0`, `const double MaxPlausibleSpeedMps = 120.0 / 3.6`, and `static bool ShouldAccept(GpsFix? previousAccepted, GpsFix candidate)`.
  - `class StatsAccumulator` with `const double GapSeconds = 30.0`, `const double MovingThresholdMps = 1.0 / 3.6`; method `void Add(GpsFix fix)`; method `void BreakSegment()`; properties `double DistanceMeters`, `double MovingSeconds`, `double MaxSpeedMps`, `double AvgSpeedMps` (distance / moving seconds, 0 when no moving time), `double CurrentSpeedMps`, `DateTime? LastFixUtc`.
- Note: `FixFilter` decides *acceptance* (drop junk before it's stored); `StatsAccumulator` assumes fixes are already accepted and handles *gaps, distance, and moving time*. Recovery (Task 5) replays stored points through `StatsAccumulator` only — stored points were already filtered.

- [ ] **Step 1: Write the failing FixFilter tests**

`tests/Esk8_Tracker.Core.Tests/FixFilterTests.cs`:

```csharp
using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class FixFilterTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double lat, double lon, double secondsAfterT0,
        double accuracy = 5, double? speed = null) =>
        new(T0.AddSeconds(secondsAfterT0), lat, lon, speed, accuracy, null);

    [Fact]
    public void FirstFix_WithGoodAccuracy_IsAccepted()
    {
        Assert.True(FixFilter.ShouldAccept(null, Fix(55.0, 12.0, 0)));
    }

    [Fact]
    public void PoorAccuracy_IsRejected()
    {
        Assert.False(FixFilter.ShouldAccept(null, Fix(55.0, 12.0, 0, accuracy: 31)));
    }

    [Fact]
    public void AccuracyExactlyAtThreshold_IsAccepted()
    {
        Assert.True(FixFilter.ShouldAccept(null, Fix(55.0, 12.0, 0, accuracy: 30)));
    }

    [Fact]
    public void ImpliedSpeedAbove120Kmh_IsRejected()
    {
        var prev = Fix(55.0, 12.0, 0);
        // 0.01 deg latitude = ~1112 m in 10 s = ~400 km/h
        var spike = Fix(55.01, 12.0, 10);
        Assert.False(FixFilter.ShouldAccept(prev, spike));
    }

    [Fact]
    public void ReportedSpeedAbove120Kmh_IsRejected()
    {
        var prev = Fix(55.0, 12.0, 0);
        var next = Fix(55.00001, 12.0, 1, speed: 40.0); // 144 km/h claimed
        Assert.False(FixFilter.ShouldAccept(prev, next));
    }

    [Fact]
    public void NormalRidingFix_IsAccepted()
    {
        var prev = Fix(55.0, 12.0, 0);
        // ~11 m in 2 s = ~20 km/h
        var next = Fix(55.0001, 12.0, 2, speed: 5.5);
        Assert.True(FixFilter.ShouldAccept(prev, next));
    }

    [Fact]
    public void ImpliedSpeed_NotChecked_AcrossLargeTimeGaps()
    {
        // After a 60 s signal gap the rider may legitimately be far away;
        // gap handling is StatsAccumulator's job, not the filter's.
        var prev = Fix(55.0, 12.0, 0);
        var afterGap = Fix(55.01, 12.0, 60); // ~1112 m in 60 s = ~67 km/h, fine
        Assert.True(FixFilter.ShouldAccept(prev, afterGap));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: FAIL to compile — `FixFilter` does not exist.

- [ ] **Step 3: Implement FixFilter**

`src/Esk8_Tracker.Core/FixFilter.cs`:

```csharp
namespace Esk8_Tracker.Core;

/// <summary>
/// Decides whether a raw GPS fix is trustworthy enough to store and feed into stats.
/// Junk fixes must never pollute the database or the top-speed record.
/// </summary>
public static class FixFilter
{
    public const double MaxAccuracyMeters = 30.0;
    public const double MaxPlausibleSpeedMps = 120.0 / 3.6;

    public static bool ShouldAccept(GpsFix? previousAccepted, GpsFix candidate)
    {
        if (candidate.AccuracyMeters > MaxAccuracyMeters)
            return false;

        if (candidate.SpeedMps is > MaxPlausibleSpeedMps)
            return false;

        if (previousAccepted is not null)
        {
            var dt = (candidate.TimestampUtc - previousAccepted.TimestampUtc).TotalSeconds;
            if (dt > 0 && dt <= StatsAccumulator.GapSeconds)
            {
                var meters = GeoMath.HaversineMeters(
                    previousAccepted.Latitude, previousAccepted.Longitude,
                    candidate.Latitude, candidate.Longitude);
                if (meters / dt > MaxPlausibleSpeedMps)
                    return false;
            }
        }

        return true;
    }
}
```

- [ ] **Step 4: Run FixFilter tests to verify they pass**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: compile error — `StatsAccumulator.GapSeconds` doesn't exist yet. Create the constant-only skeleton `src/Esk8_Tracker.Core/StatsAccumulator.cs`:

```csharp
namespace Esk8_Tracker.Core;

public class StatsAccumulator
{
    public const double GapSeconds = 30.0;
    public const double MovingThresholdMps = 1.0 / 3.6;
}
```

Re-run. Expected: PASS (11 tests total).

- [ ] **Step 5: Write the failing StatsAccumulator tests**

`tests/Esk8_Tracker.Core.Tests/StatsAccumulatorTests.cs`:

```csharp
using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class StatsAccumulatorTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double lat, double lon, double secondsAfterT0, double? speed = null) =>
        new(T0.AddSeconds(secondsAfterT0), lat, lon, speed, 5, null);

    [Fact]
    public void FreshAccumulator_IsAllZero()
    {
        var acc = new StatsAccumulator();
        Assert.Equal(0, acc.DistanceMeters);
        Assert.Equal(0, acc.MovingSeconds);
        Assert.Equal(0, acc.MaxSpeedMps);
        Assert.Equal(0, acc.AvgSpeedMps);
        Assert.Equal(0, acc.CurrentSpeedMps);
        Assert.Null(acc.LastFixUtc);
    }

    [Fact]
    public void FirstFix_SetsCurrentSpeed_ButNoDistance()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0, speed: 6.0));
        Assert.Equal(0, acc.DistanceMeters);
        Assert.Equal(6.0, acc.CurrentSpeedMps);
        Assert.Equal(T0, acc.LastFixUtc);
    }

    [Fact]
    public void TwoFixes_AccumulateHaversineDistance()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2)); // ~11.1 m
        Assert.InRange(acc.DistanceMeters, 10.5, 11.7);
    }

    [Fact]
    public void MovingTime_OnlyCountsAboveThreshold()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2));      // ~5.6 m/s -> moving, +2 s
        acc.Add(Fix(55.0001, 12.0, 10));     // same spot -> 0 m/s -> not moving
        Assert.Equal(2.0, acc.MovingSeconds, 3);
    }

    [Fact]
    public void GapOver30Seconds_DoesNotAccumulateDistanceOrTime()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.01, 12.0, 45)); // 45 s gap: segment break
        Assert.Equal(0, acc.DistanceMeters);
        Assert.Equal(0, acc.MovingSeconds);
        // but the fix still becomes the new anchor:
        acc.Add(Fix(55.0101, 12.0, 47)); // ~11.1 m in 2 s
        Assert.InRange(acc.DistanceMeters, 10.5, 11.7);
    }

    [Fact]
    public void ReportedSpeed_PreferredOverImpliedSpeed()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2, speed: 7.7));
        Assert.Equal(7.7, acc.CurrentSpeedMps);
        Assert.Equal(7.7, acc.MaxSpeedMps);
    }

    [Fact]
    public void ImpliedSpeed_UsedWhenReportedMissing()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2)); // ~11.1 m / 2 s ~ 5.56 m/s
        Assert.InRange(acc.CurrentSpeedMps, 5.2, 5.9);
    }

    [Fact]
    public void MaxSpeed_TracksHighestSeen()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0, speed: 3));
        acc.Add(Fix(55.0001, 12.0, 2, speed: 9));
        acc.Add(Fix(55.0002, 12.0, 4, speed: 5));
        Assert.Equal(9, acc.MaxSpeedMps);
    }

    [Fact]
    public void AvgSpeed_IsDistanceOverMovingTime()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.Add(Fix(55.0001, 12.0, 2));
        acc.Add(Fix(55.0002, 12.0, 4));
        Assert.Equal(acc.DistanceMeters / acc.MovingSeconds, acc.AvgSpeedMps, 6);
    }

    [Fact]
    public void BreakSegment_PreventsDistanceAcrossPause()
    {
        var acc = new StatsAccumulator();
        acc.Add(Fix(55.0, 12.0, 0));
        acc.BreakSegment(); // e.g. user paused and rode elsewhere
        acc.Add(Fix(55.01, 12.0, 10)); // would be ~1112 m if not broken
        Assert.Equal(0, acc.DistanceMeters);
    }
}
```

- [ ] **Step 6: Run tests to verify the new ones fail**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: FAIL to compile — `Add`, `BreakSegment`, properties missing.

- [ ] **Step 7: Implement StatsAccumulator**

Replace `src/Esk8_Tracker.Core/StatsAccumulator.cs` with:

```csharp
namespace Esk8_Tracker.Core;

/// <summary>
/// Incremental ride statistics over an ordered stream of *accepted* fixes.
/// Filtering junk fixes is FixFilter's job; this class handles distance,
/// speed, moving time, and signal-gap segmentation.
/// </summary>
public class StatsAccumulator
{
    public const double GapSeconds = 30.0;
    public const double MovingThresholdMps = 1.0 / 3.6;

    private GpsFix? _prev;

    public double DistanceMeters { get; private set; }
    public double MovingSeconds { get; private set; }
    public double MaxSpeedMps { get; private set; }
    public double CurrentSpeedMps { get; private set; }
    public DateTime? LastFixUtc { get; private set; }

    public double AvgSpeedMps => MovingSeconds > 0 ? DistanceMeters / MovingSeconds : 0;

    public void Add(GpsFix fix)
    {
        LastFixUtc = fix.TimestampUtc;

        if (_prev is null)
        {
            CurrentSpeedMps = ClampNonNegative(fix.SpeedMps ?? 0);
            MaxSpeedMps = Math.Max(MaxSpeedMps, CurrentSpeedMps);
            _prev = fix;
            return;
        }

        var dt = (fix.TimestampUtc - _prev.TimestampUtc).TotalSeconds;
        if (dt <= 0 || dt > GapSeconds)
        {
            // Out-of-order or signal gap: restart the segment at this fix.
            CurrentSpeedMps = ClampNonNegative(fix.SpeedMps ?? 0);
            MaxSpeedMps = Math.Max(MaxSpeedMps, CurrentSpeedMps);
            _prev = fix;
            return;
        }

        var meters = GeoMath.HaversineMeters(_prev.Latitude, _prev.Longitude, fix.Latitude, fix.Longitude);
        var speed = ClampNonNegative(fix.SpeedMps ?? meters / dt);

        DistanceMeters += meters;
        if (speed > MovingThresholdMps)
            MovingSeconds += dt;

        CurrentSpeedMps = speed;
        MaxSpeedMps = Math.Max(MaxSpeedMps, speed);
        _prev = fix;
    }

    /// <summary>Forget the previous fix so nothing accumulates across a pause.</summary>
    public void BreakSegment() => _prev = null;

    private static double ClampNonNegative(double v) => v < 0 ? 0 : v;
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: PASS (21 tests).

- [ ] **Step 9: Commit**

```powershell
git add src/Esk8_Tracker.Core tests; git commit -m "feat: add fix filtering and incremental ride statistics"
```

---

### Task 4: RideRecorder — state machine, buffering, events

**Files:**
- Create: `src/Esk8_Tracker.Core/Data/IRideStore.cs`
- Create: `src/Esk8_Tracker.Core/RideRecorder.cs`
- Test: `tests/Esk8_Tracker.Core.Tests/RideRecorderTests.cs`

**Interfaces:**
- Consumes: `GpsFix`, `FixFilter.ShouldAccept`, `StatsAccumulator` (Tasks 2–3), `Models.TrackPoint`.
- Produces:
  - `interface IRideStore` — `Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc)`, `Task SavePointsAsync(IReadOnlyList<TrackPoint> points)`, `Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds, double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered)`. Implemented by `Esk8Database` in Task 5.
  - `enum RecorderState { Idle, Recording, Paused }`
  - `record RideLiveStats(double DistanceMeters, double CurrentSpeedMps, double AvgSpeedMps, double MaxSpeedMps, double MovingSeconds)`
  - `class RideRecorder` — constants `FlushEveryPoints = 10`, `FlushEverySeconds = 10.0`, `SignalLostSeconds = 5.0`; ctor `RideRecorder(IRideStore store)`; members `RecorderState State`, `int? ActiveRideId`, `DateTime? RideStartedUtc`, `IReadOnlyList<(double Lat, double Lon)> RoutePoints` (in-memory copy of accepted positions, for map restore), `RideLiveStats CurrentStats`, `event Action? StateChanged`, `event Action<RideLiveStats>? StatsUpdated`, `event Action<GpsFix>? FixAccepted`, `Task<int> StartAsync(int boardId, DateTime nowUtc)`, `void Pause()`, `void Resume()`, `Task StopAsync(DateTime nowUtc)`, `Task OnFixAsync(GpsFix fix)`, `bool IsSignalLost(DateTime nowUtc)`.
- Threading note: fixes arrive on the Android main looper and UI calls come from the MAUI main thread — same thread. The recorder is written for single-threaded use; the returned `Task` from `OnFixAsync` completes when any triggered flush has been persisted.

- [ ] **Step 1: Write IRideStore (no test — it's an interface)**

`src/Esk8_Tracker.Core/Data/IRideStore.cs`:

```csharp
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Data;

/// <summary>Persistence seam used by RideRecorder; implemented by Esk8Database.</summary>
public interface IRideStore
{
    Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc);

    Task SavePointsAsync(IReadOnlyList<TrackPoint> points);

    Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds,
        double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered);
}
```

- [ ] **Step 2: Write the failing RideRecorder tests**

`tests/Esk8_Tracker.Core.Tests/RideRecorderTests.cs`:

```csharp
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public class FakeRideStore : IRideStore
{
    public int NextRideId = 42;
    public List<TrackPoint> SavedPoints = new();
    public int SaveCalls;
    public (int RideId, double Distance, double Moving, double Avg, double Max, DateTime EndedAt, bool Recovered)? Finalized;

    public Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc) => Task.FromResult(NextRideId);

    public Task SavePointsAsync(IReadOnlyList<TrackPoint> points)
    {
        SaveCalls++;
        SavedPoints.AddRange(points);
        return Task.CompletedTask;
    }

    public Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds,
        double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered)
    {
        Finalized = (rideId, distanceMeters, movingSeconds, avgSpeedMps, maxSpeedMps, endedAtUtc, wasRecovered);
        return Task.CompletedTask;
    }
}

public class RideRecorderTests
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private static GpsFix Fix(double lat, double lon, double secondsAfterT0,
        double accuracy = 5, double? speed = null) =>
        new(T0.AddSeconds(secondsAfterT0), lat, lon, speed, accuracy, null);

    private readonly FakeRideStore _store = new();
    private readonly RideRecorder _recorder;

    public RideRecorderTests() => _recorder = new RideRecorder(_store);

    [Fact]
    public async Task Start_CreatesRide_AndEntersRecording()
    {
        var stateChanges = 0;
        _recorder.StateChanged += () => stateChanges++;

        var id = await _recorder.StartAsync(boardId: 7, T0);

        Assert.Equal(42, id);
        Assert.Equal(RecorderState.Recording, _recorder.State);
        Assert.Equal(42, _recorder.ActiveRideId);
        Assert.Equal(T0, _recorder.RideStartedUtc);
        Assert.Equal(1, stateChanges);
    }

    [Fact]
    public async Task Start_WhileRecording_Throws()
    {
        await _recorder.StartAsync(7, T0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _recorder.StartAsync(8, T0));
    }

    [Fact]
    public async Task Fix_WhileIdle_IsIgnored()
    {
        var events = 0;
        _recorder.FixAccepted += _ => events++;
        await _recorder.OnFixAsync(Fix(55, 12, 0));
        Assert.Equal(0, events);
        Assert.Empty(_store.SavedPoints);
    }

    [Fact]
    public async Task AcceptedFix_RaisesEvents_AndTracksRoute()
    {
        await _recorder.StartAsync(7, T0);
        GpsFix? accepted = null;
        RideLiveStats? stats = null;
        _recorder.FixAccepted += f => accepted = f;
        _recorder.StatsUpdated += s => stats = s;

        await _recorder.OnFixAsync(Fix(55, 12, 1, speed: 5));

        Assert.NotNull(accepted);
        Assert.NotNull(stats);
        Assert.Equal(5, stats!.CurrentSpeedMps);
        Assert.Single(_recorder.RoutePoints);
    }

    [Fact]
    public async Task RejectedFix_RaisesNothing()
    {
        await _recorder.StartAsync(7, T0);
        var events = 0;
        _recorder.FixAccepted += _ => events++;

        await _recorder.OnFixAsync(Fix(55, 12, 1, accuracy: 99));

        Assert.Equal(0, events);
        Assert.Empty(_recorder.RoutePoints);
    }

    [Fact]
    public async Task Buffer_FlushesAfterTenPoints()
    {
        await _recorder.StartAsync(7, T0);
        for (var i = 0; i < 9; i++)
            await _recorder.OnFixAsync(Fix(55 + i * 0.00001, 12, i, speed: 3));
        Assert.Equal(0, _store.SaveCalls);

        await _recorder.OnFixAsync(Fix(55.0001, 12, 9.5, speed: 3));

        Assert.Equal(1, _store.SaveCalls);
        Assert.Equal(10, _store.SavedPoints.Count);
        Assert.All(_store.SavedPoints, p => Assert.Equal(42, p.RideId));
    }

    [Fact]
    public async Task Buffer_FlushesAfterTenSeconds()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        Assert.Equal(0, _store.SaveCalls);

        await _recorder.OnFixAsync(Fix(55.0001, 12, 11, speed: 3));

        Assert.Equal(1, _store.SaveCalls);
        Assert.Equal(2, _store.SavedPoints.Count);
    }

    [Fact]
    public async Task Pause_DiscardsFixes_AndResumeBreaksSegment()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        _recorder.Pause();
        Assert.Equal(RecorderState.Paused, _recorder.State);

        await _recorder.OnFixAsync(Fix(55.005, 12, 5, speed: 3)); // ignored
        Assert.Single(_recorder.RoutePoints);

        _recorder.Resume();
        Assert.Equal(RecorderState.Recording, _recorder.State);

        // Far away after pause: must not add ~550 m of distance across the pause.
        await _recorder.OnFixAsync(Fix(55.005, 12, 20, speed: 3));
        Assert.Equal(0, _recorder.CurrentStats.DistanceMeters, 3);
    }

    [Fact]
    public async Task Stop_FlushesRemainder_Finalizes_AndGoesIdle()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        await _recorder.OnFixAsync(Fix(55.0001, 12, 2, speed: 3));

        await _recorder.StopAsync(T0.AddSeconds(30));

        Assert.Equal(RecorderState.Idle, _recorder.State);
        Assert.Null(_recorder.ActiveRideId);
        Assert.Equal(2, _store.SavedPoints.Count);
        Assert.NotNull(_store.Finalized);
        Assert.Equal(42, _store.Finalized!.Value.RideId);
        Assert.Equal(T0.AddSeconds(30), _store.Finalized!.Value.EndedAt);
        Assert.False(_store.Finalized!.Value.Recovered);
        Assert.InRange(_store.Finalized!.Value.Distance, 10.5, 11.7);
    }

    [Fact]
    public async Task Stop_WhilePaused_IsAllowed()
    {
        await _recorder.StartAsync(7, T0);
        _recorder.Pause();
        await _recorder.StopAsync(T0.AddSeconds(10));
        Assert.Equal(RecorderState.Idle, _recorder.State);
        Assert.NotNull(_store.Finalized);
    }

    [Fact]
    public async Task SignalLost_WhenNoFixForFiveSeconds()
    {
        await _recorder.StartAsync(7, T0);
        // No fix yet: lost once 5 s have passed since start.
        Assert.False(_recorder.IsSignalLost(T0.AddSeconds(4)));
        Assert.True(_recorder.IsSignalLost(T0.AddSeconds(6)));

        await _recorder.OnFixAsync(Fix(55, 12, 10));
        Assert.False(_recorder.IsSignalLost(T0.AddSeconds(14)));
        Assert.True(_recorder.IsSignalLost(T0.AddSeconds(16)));
    }

    [Fact]
    public void SignalLost_IsFalseWhenIdle()
    {
        Assert.False(_recorder.IsSignalLost(T0));
    }

    [Fact]
    public async Task SecondStart_BeginsCleanRide()
    {
        await _recorder.StartAsync(7, T0);
        await _recorder.OnFixAsync(Fix(55, 12, 0, speed: 3));
        await _recorder.OnFixAsync(Fix(55.0001, 12, 2, speed: 3));
        await _recorder.StopAsync(T0.AddSeconds(5));

        _store.NextRideId = 43;
        await _recorder.StartAsync(7, T0.AddMinutes(5));

        Assert.Empty(_recorder.RoutePoints);
        Assert.Equal(0, _recorder.CurrentStats.DistanceMeters);
        Assert.Equal(43, _recorder.ActiveRideId);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: FAIL to compile — `RideRecorder`, `RecorderState`, `RideLiveStats`, `IRideStore` members missing.

- [ ] **Step 4: Implement RideRecorder**

`src/Esk8_Tracker.Core/RideRecorder.cs`:

```csharp
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core;

public enum RecorderState { Idle, Recording, Paused }

public record RideLiveStats(
    double DistanceMeters,
    double CurrentSpeedMps,
    double AvgSpeedMps,
    double MaxSpeedMps,
    double MovingSeconds);

/// <summary>
/// Owns the active ride: filters fixes, accumulates stats, buffers points to the
/// store every 10 points / 10 seconds (crash safety), and raises UI events.
/// Single-threaded by design: platform fixes and UI calls both arrive on the main thread.
/// </summary>
public class RideRecorder(IRideStore store)
{
    public const int FlushEveryPoints = 10;
    public const double FlushEverySeconds = 10.0;
    public const double SignalLostSeconds = 5.0;

    private StatsAccumulator _acc = new();
    private GpsFix? _lastAccepted;
    private readonly List<TrackPoint> _buffer = new();
    private readonly List<(double Lat, double Lon)> _route = new();
    private DateTime? _lastFlushUtc;

    public RecorderState State { get; private set; } = RecorderState.Idle;
    public int? ActiveRideId { get; private set; }
    public DateTime? RideStartedUtc { get; private set; }
    public IReadOnlyList<(double Lat, double Lon)> RoutePoints => _route;

    public RideLiveStats CurrentStats => new(
        _acc.DistanceMeters, _acc.CurrentSpeedMps, _acc.AvgSpeedMps, _acc.MaxSpeedMps, _acc.MovingSeconds);

    public event Action? StateChanged;
    public event Action<RideLiveStats>? StatsUpdated;
    public event Action<GpsFix>? FixAccepted;

    public async Task<int> StartAsync(int boardId, DateTime nowUtc)
    {
        if (State != RecorderState.Idle)
            throw new InvalidOperationException($"Cannot start a ride while {State}.");

        _acc = new StatsAccumulator();
        _lastAccepted = null;
        _buffer.Clear();
        _route.Clear();
        _lastFlushUtc = null;

        ActiveRideId = await store.CreateRideAsync(boardId, nowUtc).ConfigureAwait(false);
        RideStartedUtc = nowUtc;
        State = RecorderState.Recording;
        StateChanged?.Invoke();
        return ActiveRideId.Value;
    }

    public void Pause()
    {
        if (State != RecorderState.Recording) return;
        State = RecorderState.Paused;
        _acc.BreakSegment();
        _lastAccepted = null;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (State != RecorderState.Paused) return;
        State = RecorderState.Recording;
        StateChanged?.Invoke();
    }

    public async Task StopAsync(DateTime nowUtc)
    {
        if (State == RecorderState.Idle)
            throw new InvalidOperationException("No active ride to stop.");

        var rideId = ActiveRideId!.Value;
        await FlushAsync().ConfigureAwait(false);
        await store.FinalizeRideAsync(rideId, _acc.DistanceMeters, _acc.MovingSeconds,
            _acc.AvgSpeedMps, _acc.MaxSpeedMps, nowUtc, wasRecovered: false).ConfigureAwait(false);

        State = RecorderState.Idle;
        ActiveRideId = null;
        RideStartedUtc = null;
        StateChanged?.Invoke();
    }

    public async Task OnFixAsync(GpsFix fix)
    {
        if (State != RecorderState.Recording) return;
        if (!FixFilter.ShouldAccept(_lastAccepted, fix)) return;

        _lastAccepted = fix;
        _acc.Add(fix);
        _route.Add((fix.Latitude, fix.Longitude));

        _buffer.Add(new TrackPoint
        {
            RideId = ActiveRideId!.Value,
            Timestamp = fix.TimestampUtc,
            Latitude = fix.Latitude,
            Longitude = fix.Longitude,
            SpeedMps = _acc.CurrentSpeedMps,
            AccuracyMeters = fix.AccuracyMeters,
            AltitudeMeters = fix.AltitudeMeters,
        });

        FixAccepted?.Invoke(fix);
        StatsUpdated?.Invoke(CurrentStats);

        _lastFlushUtc ??= fix.TimestampUtc;
        var due = _buffer.Count >= FlushEveryPoints
               || (fix.TimestampUtc - _lastFlushUtc.Value).TotalSeconds >= FlushEverySeconds;
        if (due)
        {
            _lastFlushUtc = fix.TimestampUtc;
            await FlushAsync().ConfigureAwait(false);
        }
    }

    public bool IsSignalLost(DateTime nowUtc)
    {
        if (State != RecorderState.Recording) return false;
        var reference = _acc.LastFixUtc ?? RideStartedUtc;
        return reference is not null && (nowUtc - reference.Value).TotalSeconds > SignalLostSeconds;
    }

    private async Task FlushAsync()
    {
        if (_buffer.Count == 0) return;
        var batch = _buffer.ToList();
        _buffer.Clear();
        await store.SavePointsAsync(batch).ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: PASS (34 tests).

- [ ] **Step 6: Commit**

```powershell
git add src/Esk8_Tracker.Core tests; git commit -m "feat: add RideRecorder with buffered persistence and live events"
```

---

### Task 5: Esk8Database — boards, rides, points, crash recovery

**Files:**
- Create: `src/Esk8_Tracker.Core/Data/Esk8Database.cs`
- Test: `tests/Esk8_Tracker.Core.Tests/DatabaseTests.cs`

**Interfaces:**
- Consumes: `Models.*`, `IRideStore`, `StatsAccumulator`, `GpsFix` (Tasks 2–4).
- Produces: `class Esk8Database : IRideStore` — ctor `Esk8Database(string dbPath)`; every public method self-initializes the schema, so no explicit Init call is needed. Methods (all `Task`-returning, names exact):
  - Boards: `Task<List<Board>> GetActiveBoardsAsync()`, `Task<Board> AddBoardAsync(string name)`, `Task RenameBoardAsync(int id, string name)`, `Task ArchiveBoardAsync(int id)`, `Task<Dictionary<int, string>> GetBoardNamesAsync()` (includes archived).
  - Rides: `CreateRideAsync` / `SavePointsAsync` / `FinalizeRideAsync` (the `IRideStore` methods), `Task<List<Ride>> GetCompletedRidesAsync()` (EndedAt set, newest first), `Task<Ride?> GetRideAsync(int id)`, `Task<List<TrackPoint>> GetTrackPointsAsync(int rideId)` (by Timestamp ascending).
  - Recovery: `Task<int> RecoverUnfinishedRidesAsync()` — for each ride with `EndedAt == null`: no points → delete the ride row; otherwise replay points through `StatsAccumulator`, finalize with `EndedAt` = last point's timestamp and `WasRecovered = true`. Returns number recovered (deleted empty rides don't count).
  - `Task CloseAsync()` for tests.

- [ ] **Step 1: Write the failing tests**

`tests/Esk8_Tracker.Core.Tests/DatabaseTests.cs`:

```csharp
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Tests;

public sealed class DatabaseTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"esk8test_{Guid.NewGuid():N}.db3");
    private Esk8Database _db = null!;

    public Task InitializeAsync()
    {
        _db = new Esk8Database(_dbPath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        File.Delete(_dbPath);
    }

    private static TrackPoint Point(int rideId, double lat, double lon, double secondsAfterT0, double speed = 5) =>
        new()
        {
            RideId = rideId, Timestamp = T0.AddSeconds(secondsAfterT0),
            Latitude = lat, Longitude = lon, SpeedMps = speed, AccuracyMeters = 5,
        };

    [Fact]
    public async Task AddBoard_AppearsInActiveBoards()
    {
        var board = await _db.AddBoardAsync("Meepo V5");
        var boards = await _db.GetActiveBoardsAsync();
        Assert.Single(boards);
        Assert.Equal("Meepo V5", boards[0].Name);
        Assert.True(board.Id > 0);
    }

    [Fact]
    public async Task ArchivedBoard_LeavesActiveList_ButKeepsName()
    {
        var board = await _db.AddBoardAsync("Old faithful");
        await _db.ArchiveBoardAsync(board.Id);

        Assert.Empty(await _db.GetActiveBoardsAsync());
        var names = await _db.GetBoardNamesAsync();
        Assert.Equal("Old faithful", names[board.Id]);
    }

    [Fact]
    public async Task RenameBoard_ChangesName()
    {
        var board = await _db.AddBoardAsync("Tpyo");
        await _db.RenameBoardAsync(board.Id, "Typo");
        var boards = await _db.GetActiveBoardsAsync();
        Assert.Equal("Typo", boards[0].Name);
    }

    [Fact]
    public async Task CreateRide_IsInProgress_AndExcludedFromCompleted()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);

        Assert.True(rideId > 0);
        Assert.Empty(await _db.GetCompletedRidesAsync());
        var ride = await _db.GetRideAsync(rideId);
        Assert.NotNull(ride);
        Assert.Null(ride!.EndedAt);
    }

    [Fact]
    public async Task FinalizeRide_ShowsUpInCompleted_NewestFirst()
    {
        var board = await _db.AddBoardAsync("Board");
        var first = await _db.CreateRideAsync(board.Id, T0);
        await _db.FinalizeRideAsync(first, 1000, 300, 3.33, 8, T0.AddMinutes(10), false);
        var second = await _db.CreateRideAsync(board.Id, T0.AddHours(2));
        await _db.FinalizeRideAsync(second, 2000, 500, 4, 9, T0.AddHours(2).AddMinutes(15), false);

        var rides = await _db.GetCompletedRidesAsync();
        Assert.Equal(2, rides.Count);
        Assert.Equal(second, rides[0].Id);
        Assert.Equal(1000, rides[1].DistanceMeters);
    }

    [Fact]
    public async Task SavePoints_RoundTripsOrderedByTimestamp()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[] { Point(rideId, 55.0001, 12, 2), Point(rideId, 55, 12, 0) });

        var points = await _db.GetTrackPointsAsync(rideId);
        Assert.Equal(2, points.Count);
        Assert.Equal(55.0, points[0].Latitude);   // earliest first
        Assert.Equal(55.0001, points[1].Latitude);
    }

    [Fact]
    public async Task Recovery_FinalizesUnfinishedRideFromPoints()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.SavePointsAsync(new[]
        {
            Point(rideId, 55.0000, 12, 0),
            Point(rideId, 55.0001, 12, 2),
            Point(rideId, 55.0002, 12, 4),
        });

        var recovered = await _db.RecoverUnfinishedRidesAsync();

        Assert.Equal(1, recovered);
        var ride = await _db.GetRideAsync(rideId);
        Assert.NotNull(ride!.EndedAt);
        Assert.Equal(T0.AddSeconds(4), ride.EndedAt!.Value);
        Assert.True(ride.WasRecovered);
        Assert.InRange(ride.DistanceMeters, 21, 23.4); // 2 hops of ~11.1 m
        Assert.Equal(4, ride.MovingSeconds, 3);
    }

    [Fact]
    public async Task Recovery_DeletesEmptyUnfinishedRide()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);

        var recovered = await _db.RecoverUnfinishedRidesAsync();

        Assert.Equal(0, recovered);
        Assert.Null(await _db.GetRideAsync(rideId));
    }

    [Fact]
    public async Task Recovery_LeavesCompletedRidesAlone()
    {
        var board = await _db.AddBoardAsync("Board");
        var rideId = await _db.CreateRideAsync(board.Id, T0);
        await _db.FinalizeRideAsync(rideId, 500, 100, 5, 7, T0.AddMinutes(5), false);

        var recovered = await _db.RecoverUnfinishedRidesAsync();

        Assert.Equal(0, recovered);
        var ride = await _db.GetRideAsync(rideId);
        Assert.False(ride!.WasRecovered);
        Assert.Equal(500, ride.DistanceMeters);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: FAIL to compile — `Esk8Database` does not exist.

- [ ] **Step 3: Implement Esk8Database**

`src/Esk8_Tracker.Core/Data/Esk8Database.cs`:

```csharp
using Esk8_Tracker.Core.Models;
using SQLite;

namespace Esk8_Tracker.Core.Data;

/// <summary>
/// Local SQLite persistence. Schema is created lazily on first use;
/// safe to resolve as a singleton and call from anywhere.
/// </summary>
public class Esk8Database : IRideStore
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public Esk8Database(string dbPath)
    {
        _connection = new SQLiteAsyncConnection(dbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            await _connection.CreateTableAsync<Board>().ConfigureAwait(false);
            await _connection.CreateTableAsync<Ride>().ConfigureAwait(false);
            await _connection.CreateTableAsync<TrackPoint>().ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task CloseAsync() => _connection.CloseAsync();

    // ---- Boards ----

    public async Task<List<Board>> GetActiveBoardsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Board>()
            .Where(b => !b.IsArchived)
            .OrderBy(b => b.Name)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Board> AddBoardAsync(string name)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var board = new Board { Name = name, CreatedAt = DateTime.UtcNow };
        await _connection.InsertAsync(board).ConfigureAwait(false);
        return board;
    }

    public async Task RenameBoardAsync(int id, string name)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Board SET Name = ? WHERE Id = ?", name, id).ConfigureAwait(false);
    }

    public async Task ArchiveBoardAsync(int id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Board SET IsArchived = 1 WHERE Id = ?", id).ConfigureAwait(false);
    }

    public async Task<Dictionary<int, string>> GetBoardNamesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var boards = await _connection.Table<Board>().ToListAsync().ConfigureAwait(false);
        return boards.ToDictionary(b => b.Id, b => b.Name);
    }

    // ---- Rides (IRideStore) ----

    public async Task<int> CreateRideAsync(int boardId, DateTime startedAtUtc)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var ride = new Ride { BoardId = boardId, StartedAt = startedAtUtc };
        await _connection.InsertAsync(ride).ConfigureAwait(false);
        return ride.Id;
    }

    public async Task SavePointsAsync(IReadOnlyList<TrackPoint> points)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.InsertAllAsync(points).ConfigureAwait(false);
    }

    public async Task FinalizeRideAsync(int rideId, double distanceMeters, double movingSeconds,
        double avgSpeedMps, double maxSpeedMps, DateTime endedAtUtc, bool wasRecovered)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _connection.ExecuteAsync(
            "UPDATE Ride SET DistanceMeters = ?, MovingSeconds = ?, AvgSpeedMps = ?, " +
            "MaxSpeedMps = ?, EndedAt = ?, WasRecovered = ? WHERE Id = ?",
            distanceMeters, movingSeconds, avgSpeedMps, maxSpeedMps,
            endedAtUtc, wasRecovered, rideId).ConfigureAwait(false);
    }

    // ---- Ride queries ----

    public async Task<List<Ride>> GetCompletedRidesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Ride>()
            .Where(r => r.EndedAt != null)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Ride?> GetRideAsync(int id)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<Ride>()
            .Where(r => r.Id == id)
            .FirstOrDefaultAsync().ConfigureAwait(false);
    }

    public async Task<List<TrackPoint>> GetTrackPointsAsync(int rideId)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        return await _connection.Table<TrackPoint>()
            .Where(p => p.RideId == rideId)
            .OrderBy(p => p.Timestamp)
            .ToListAsync().ConfigureAwait(false);
    }

    // ---- Crash recovery ----

    public async Task<int> RecoverUnfinishedRidesAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        var unfinished = await _connection.Table<Ride>()
            .Where(r => r.EndedAt == null)
            .ToListAsync().ConfigureAwait(false);

        var recovered = 0;
        foreach (var ride in unfinished)
        {
            var points = await GetTrackPointsAsync(ride.Id).ConfigureAwait(false);
            if (points.Count == 0)
            {
                await _connection.DeleteAsync(ride).ConfigureAwait(false);
                continue;
            }

            var acc = new StatsAccumulator();
            foreach (var p in points)
                acc.Add(new GpsFix(p.Timestamp, p.Latitude, p.Longitude,
                    p.SpeedMps, p.AccuracyMeters, p.AltitudeMeters));

            await FinalizeRideAsync(ride.Id, acc.DistanceMeters, acc.MovingSeconds,
                acc.AvgSpeedMps, acc.MaxSpeedMps, points[^1].Timestamp,
                wasRecovered: true).ConfigureAwait(false);
            recovered++;
        }

        return recovered;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: PASS (43 tests).

- [ ] **Step 5: Commit**

```powershell
git add src/Esk8_Tracker.Core tests; git commit -m "feat: add SQLite database with boards, rides and crash recovery"
```

---

### Task 6: Dashboard aggregates + display formatting

**Files:**
- Create: `src/Esk8_Tracker.Core/Data/DashboardStats.cs`
- Modify: `src/Esk8_Tracker.Core/Data/Esk8Database.cs` (add `GetDashboardStatsAsync`)
- Create: `src/Esk8_Tracker.Core/Format.cs`
- Test: `tests/Esk8_Tracker.Core.Tests/DashboardStatsTests.cs`, `tests/Esk8_Tracker.Core.Tests/FormatTests.cs`

**Interfaces:**
- Consumes: `Esk8Database`, models (Task 5).
- Produces:
  - `record MonthlyStat(int Year, int Month, double DistanceMeters, int RideCount)`
  - `record BoardStat(string BoardName, double DistanceMeters, int RideCount)`
  - `class DashboardStats { double TotalDistanceMeters; int RideCount; double TotalMovingSeconds; double TopSpeedMps; double LongestRideMeters; List<MonthlyStat> Monthly; List<BoardStat> PerBoard; }`
  - `Task<DashboardStats> Esk8Database.GetDashboardStatsAsync()` — completed rides only; Monthly newest-first (by ride start, local time); PerBoard by distance descending, using board names (archived included).
  - `static class Format` — `string SpeedKmh(double mps)` ("0.0" format), `string DistanceKm(double meters)` ("0.00"), `string Duration(double seconds)` ("h\:mm\:ss").

- [ ] **Step 1: Write the failing dashboard tests**

`tests/Esk8_Tracker.Core.Tests/DashboardStatsTests.cs`:

```csharp
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.Core.Tests;

public sealed class DashboardStatsTests : IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"esk8test_{Guid.NewGuid():N}.db3");
    private Esk8Database _db = null!;

    public Task InitializeAsync()
    {
        _db = new Esk8Database(_dbPath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        File.Delete(_dbPath);
    }

    private async Task<int> AddCompletedRide(int boardId, DateTime startUtc,
        double distance, double moving, double max)
    {
        var id = await _db.CreateRideAsync(boardId, startUtc);
        await _db.FinalizeRideAsync(id, distance, moving,
            moving > 0 ? distance / moving : 0, max, startUtc.AddSeconds(moving), false);
        return id;
    }

    [Fact]
    public async Task EmptyDatabase_GivesZeroes()
    {
        var stats = await _db.GetDashboardStatsAsync();
        Assert.Equal(0, stats.TotalDistanceMeters);
        Assert.Equal(0, stats.RideCount);
        Assert.Equal(0, stats.TopSpeedMps);
        Assert.Equal(0, stats.LongestRideMeters);
        Assert.Empty(stats.Monthly);
        Assert.Empty(stats.PerBoard);
    }

    [Fact]
    public async Task TotalsRecordsMonthlyAndPerBoard_AreComputed()
    {
        var may = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var june = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var board1 = await _db.AddBoardAsync("Alpha");
        var board2 = await _db.AddBoardAsync("Beta");

        await AddCompletedRide(board1.Id, may, 5000, 1000, 10);
        await AddCompletedRide(board1.Id, june, 3000, 600, 12);
        await AddCompletedRide(board2.Id, june.AddDays(1), 9000, 1500, 8);

        var stats = await _db.GetDashboardStatsAsync();

        Assert.Equal(17000, stats.TotalDistanceMeters);
        Assert.Equal(3, stats.RideCount);
        Assert.Equal(3100, stats.TotalMovingSeconds);
        Assert.Equal(12, stats.TopSpeedMps);
        Assert.Equal(9000, stats.LongestRideMeters);

        Assert.Equal(2, stats.Monthly.Count);
        Assert.Equal((2026, 6), (stats.Monthly[0].Year, stats.Monthly[0].Month)); // newest first
        Assert.Equal(12000, stats.Monthly[0].DistanceMeters);
        Assert.Equal(2, stats.Monthly[0].RideCount);
        Assert.Equal(5000, stats.Monthly[1].DistanceMeters);

        Assert.Equal(2, stats.PerBoard.Count);
        Assert.Equal("Beta", stats.PerBoard[0].BoardName);   // most distance first
        Assert.Equal(9000, stats.PerBoard[0].DistanceMeters);
        Assert.Equal("Alpha", stats.PerBoard[1].BoardName);
        Assert.Equal(2, stats.PerBoard[1].RideCount);
    }

    [Fact]
    public async Task InProgressRides_AreExcluded()
    {
        var board = await _db.AddBoardAsync("Alpha");
        await _db.CreateRideAsync(board.Id, DateTime.UtcNow); // never finalized

        var stats = await _db.GetDashboardStatsAsync();

        Assert.Equal(0, stats.RideCount);
        Assert.Empty(stats.PerBoard);
    }

    [Fact]
    public async Task ArchivedBoard_StillNamedInPerBoard()
    {
        var board = await _db.AddBoardAsync("Retired");
        await AddCompletedRide(board.Id, new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc), 1000, 200, 5);
        await _db.ArchiveBoardAsync(board.Id);

        var stats = await _db.GetDashboardStatsAsync();

        Assert.Equal("Retired", stats.PerBoard[0].BoardName);
    }
}
```

- [ ] **Step 2: Write the failing Format tests**

`tests/Esk8_Tracker.Core.Tests/FormatTests.cs`:

```csharp
using System.Globalization;
using Esk8_Tracker.Core;

namespace Esk8_Tracker.Core.Tests;

public class FormatTests
{
    public FormatTests()
    {
        // Machine culture is da-DK (comma decimals); pin per-test-thread for stable assertions.
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void SpeedKmh_ConvertsAndFormats()
    {
        Assert.Equal("36.0", Format.SpeedKmh(10));
        Assert.Equal("0.0", Format.SpeedKmh(0));
    }

    [Fact]
    public void DistanceKm_ConvertsAndFormats()
    {
        Assert.Equal("1.50", Format.DistanceKm(1500));
        Assert.Equal("0.00", Format.DistanceKm(0));
    }

    [Fact]
    public void Duration_FormatsHoursMinutesSeconds()
    {
        Assert.Equal("0:05:30", Format.Duration(330));
        Assert.Equal("1:00:00", Format.Duration(3600));
        Assert.Equal("0:00:00", Format.Duration(0));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: FAIL to compile — `GetDashboardStatsAsync`, `Format` missing.

- [ ] **Step 4: Implement DashboardStats, the query, and Format**

`src/Esk8_Tracker.Core/Data/DashboardStats.cs`:

```csharp
namespace Esk8_Tracker.Core.Data;

public record MonthlyStat(int Year, int Month, double DistanceMeters, int RideCount);

public record BoardStat(string BoardName, double DistanceMeters, int RideCount);

public class DashboardStats
{
    public double TotalDistanceMeters { get; init; }
    public int RideCount { get; init; }
    public double TotalMovingSeconds { get; init; }
    public double TopSpeedMps { get; init; }
    public double LongestRideMeters { get; init; }
    public List<MonthlyStat> Monthly { get; init; } = new();
    public List<BoardStat> PerBoard { get; init; } = new();
}
```

Add to `src/Esk8_Tracker.Core/Data/Esk8Database.cs` (new section before crash recovery; ride volumes are small — hundreds per year — so aggregating in memory is simpler and safer than SQL date math over tick-encoded columns):

```csharp
    // ---- Dashboard ----

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var rides = await GetCompletedRidesAsync().ConfigureAwait(false);
        var names = await GetBoardNamesAsync().ConfigureAwait(false);

        var monthly = rides
            .GroupBy(r => { var local = r.StartedAt.ToLocalTime(); return (local.Year, local.Month); })
            .Select(g => new MonthlyStat(g.Key.Year, g.Key.Month,
                g.Sum(r => r.DistanceMeters), g.Count()))
            .OrderByDescending(m => (m.Year, m.Month))
            .ToList();

        var perBoard = rides
            .GroupBy(r => r.BoardId)
            .Select(g => new BoardStat(
                names.TryGetValue(g.Key, out var name) ? name : "(unknown board)",
                g.Sum(r => r.DistanceMeters), g.Count()))
            .OrderByDescending(b => b.DistanceMeters)
            .ToList();

        return new DashboardStats
        {
            TotalDistanceMeters = rides.Sum(r => r.DistanceMeters),
            RideCount = rides.Count,
            TotalMovingSeconds = rides.Sum(r => r.MovingSeconds),
            TopSpeedMps = rides.Count > 0 ? rides.Max(r => r.MaxSpeedMps) : 0,
            LongestRideMeters = rides.Count > 0 ? rides.Max(r => r.DistanceMeters) : 0,
            Monthly = monthly,
            PerBoard = perBoard,
        };
    }
```

`src/Esk8_Tracker.Core/Format.cs`:

```csharp
namespace Esk8_Tracker.Core;

/// <summary>Display formatting (current culture): km, km/h, h:mm:ss.</summary>
public static class Format
{
    public static string SpeedKmh(double mps) => (mps * 3.6).ToString("0.0");

    public static string DistanceKm(double meters) => (meters / 1000.0).ToString("0.00");

    public static string Duration(double seconds) =>
        TimeSpan.FromSeconds(seconds).ToString(@"h\:mm\:ss");
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Esk8_Tracker.Core.Tests --nologo`
Expected: PASS (50 tests).

- [ ] **Step 6: Commit**

```powershell
git add src/Esk8_Tracker.Core tests; git commit -m "feat: add dashboard aggregates and display formatting"
```

---

### Task 7: MAUI app skeleton — packages, DI, Shell tabs, placeholder pages

No unit tests in Tasks 7–14 (thin UI over the tested Core); each ends with a build/run verification instead.

**Files:**
- Modify: `src/Esk8_Tracker/Esk8_Tracker.csproj` (packages, Android min version)
- Modify: `src/Esk8_Tracker/MauiProgram.cs`, `src/Esk8_Tracker/App.xaml.cs`, `src/Esk8_Tracker/AppShell.xaml`, `src/Esk8_Tracker/AppShell.xaml.cs`
- Delete: `src/Esk8_Tracker/MainPage.xaml`, `src/Esk8_Tracker/MainPage.xaml.cs`
- Create: `src/Esk8_Tracker/Views/RidePage.xaml(.cs)`, `Views/HistoryPage.xaml(.cs)`, `Views/RideDetailPage.xaml(.cs)`, `Views/StatsPage.xaml(.cs)`, `Views/BoardsPage.xaml(.cs)` — placeholders

**Interfaces:**
- Consumes: `Esk8Database`, `RideRecorder`, `IRideStore` (Tasks 4–5).
- Produces: DI container with `Esk8Database` + `RideRecorder` singletons; Shell with routes `RidePage`, `HistoryPage`, `StatsPage`, `BoardsPage` (tabs) and `RideDetailPage` (pushed); crash recovery invoked at app start. Later tasks fill the placeholder pages and register their ViewModels.

- [ ] **Step 1: Add packages and raise Android minimum to API 26**

In `src/Esk8_Tracker/Esk8_Tracker.csproj`, change the Android supported version line (foreground-service APIs used in Task 10 need API 26+):

```xml
<SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">26.0</SupportedOSPlatformVersion>
```

and add to the existing `<ItemGroup>` with PackageReferences:

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
<PackageReference Include="Mapsui.Maui" Version="5.1.0" />
```

(Mapsui.Maui 5.1.0 ships net9.0/net9.0-android35.0 assets — consumable from net10 targets; the build in Step 6 is the compatibility smoke test.)

- [ ] **Step 2: Wire MauiProgram**

Replace `src/Esk8_Tracker/MauiProgram.cs` with:

```csharp
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Esk8_Tracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp() // required by Mapsui
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton(_ =>
                new Esk8Database(Path.Combine(FileSystem.AppDataDirectory, "esk8.db3")));
            builder.Services.AddSingleton<IRideStore>(sp => sp.GetRequiredService<Esk8Database>());
            builder.Services.AddSingleton<RideRecorder>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
```

- [ ] **Step 3: Run crash recovery at startup**

Replace `src/Esk8_Tracker/App.xaml.cs` with:

```csharp
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker
{
    public partial class App : Application
    {
        private readonly Esk8Database _database;

        public App(Esk8Database database)
        {
            InitializeComponent();
            _database = database;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override void OnStart()
        {
            base.OnStart();
            _ = RecoverAsync();
        }

        private async Task RecoverAsync()
        {
            try
            {
                await _database.RecoverUnfinishedRidesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ride recovery failed: {ex}");
            }
        }
    }
}
```

(If the template's `App.xaml.cs` differs slightly — e.g. it already overrides `CreateWindow` — keep its structure and add the constructor parameter, `OnStart`, and `RecoverAsync`.)

- [ ] **Step 4: Replace the shell with four tabs**

`src/Esk8_Tracker/AppShell.xaml`:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell
    x:Class="Esk8_Tracker.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:views="clr-namespace:Esk8_Tracker.Views"
    Title="Esk8 Tracker">

    <TabBar>
        <ShellContent Title="Ride" ContentTemplate="{DataTemplate views:RidePage}" Route="RidePage" />
        <ShellContent Title="History" ContentTemplate="{DataTemplate views:HistoryPage}" Route="HistoryPage" />
        <ShellContent Title="Stats" ContentTemplate="{DataTemplate views:StatsPage}" Route="StatsPage" />
        <ShellContent Title="Boards" ContentTemplate="{DataTemplate views:BoardsPage}" Route="BoardsPage" />
    </TabBar>

</Shell>
```

`src/Esk8_Tracker/AppShell.xaml.cs`:

```csharp
using Esk8_Tracker.Views;

namespace Esk8_Tracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(RideDetailPage), typeof(RideDetailPage));
        }
    }
}
```

Delete `src/Esk8_Tracker/MainPage.xaml` and `src/Esk8_Tracker/MainPage.xaml.cs` (`git rm src/Esk8_Tracker/MainPage.xaml src/Esk8_Tracker/MainPage.xaml.cs`).

- [ ] **Step 5: Create the five placeholder pages**

Each page follows this exact pattern (shown for RidePage; repeat for HistoryPage, RideDetailPage, StatsPage, BoardsPage — same content, different class name and title):

`src/Esk8_Tracker/Views/RidePage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="Esk8_Tracker.Views.RidePage"
             Title="Ride">
    <Label Text="Ride" HorizontalOptions="Center" VerticalOptions="Center" />
</ContentPage>
```

`src/Esk8_Tracker/Views/RidePage.xaml.cs`:

```csharp
namespace Esk8_Tracker.Views;

public partial class RidePage : ContentPage
{
    public RidePage()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 6: Verify both platform builds**

```powershell
dotnet build src/Esk8_Tracker -f net10.0-android --nologo
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 --nologo
dotnet test tests/Esk8_Tracker.Core.Tests --nologo
```

Expected: all succeed. This is the Mapsui/net10 compatibility gate — if restore or build fails on the Mapsui package, STOP and report (fallback decision is the user's).

- [ ] **Step 7: Commit**

```powershell
git add -A; git commit -m "feat: app skeleton with tabs, DI and startup recovery"
```

---

### Task 8: Boards page

**Files:**
- Create: `src/Esk8_Tracker/ViewModels/BoardsViewModel.cs`
- Modify: `src/Esk8_Tracker/Views/BoardsPage.xaml(.cs)`, `src/Esk8_Tracker/MauiProgram.cs`

**Interfaces:**
- Consumes: `Esk8Database` board methods (Task 5).
- Produces: `BoardsViewModel` with `ObservableCollection<Board> Boards`, `string NewBoardName`, commands `LoadCommand`, `AddBoardCommand`, `RenameBoardCommand(Board)`, `ArchiveBoardCommand(Board)`. Registered in DI together with `BoardsPage`.

- [ ] **Step 1: Write the ViewModel**

`src/Esk8_Tracker/ViewModels/BoardsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.ViewModels;

public partial class BoardsViewModel(Esk8Database db) : ObservableObject
{
    public ObservableCollection<Board> Boards { get; } = new();

    [ObservableProperty]
    private string _newBoardName = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        var boards = await db.GetActiveBoardsAsync();
        Boards.Clear();
        foreach (var board in boards)
            Boards.Add(board);
    }

    [RelayCommand]
    private async Task AddBoardAsync()
    {
        var name = NewBoardName.Trim();
        if (name.Length == 0) return;
        await db.AddBoardAsync(name);
        NewBoardName = "";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RenameBoardAsync(Board board)
    {
        var newName = await Shell.Current.DisplayPromptAsync(
            "Rename board", "New name:", initialValue: board.Name);
        if (string.IsNullOrWhiteSpace(newName)) return;
        await db.RenameBoardAsync(board.Id, newName.Trim());
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ArchiveBoardAsync(Board board)
    {
        var confirmed = await Shell.Current.DisplayAlert("Delete board",
            $"Remove \"{board.Name}\"? Its rides keep their history.", "Delete", "Cancel");
        if (!confirmed) return;
        await db.ArchiveBoardAsync(board.Id);
        await LoadAsync();
    }
}
```

(If `DisplayAlert`/`DisplayPromptAsync` produce obsolete-API warnings on .NET 10, switch to the `DisplayAlertAsync`/`DisplayPromptAsync` equivalents the warning names — behavior is identical.)

- [ ] **Step 2: Write the page**

`src/Esk8_Tracker/Views/BoardsPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Esk8_Tracker.ViewModels"
             xmlns:models="clr-namespace:Esk8_Tracker.Core.Models;assembly=Esk8_Tracker.Core"
             x:Class="Esk8_Tracker.Views.BoardsPage"
             x:DataType="vm:BoardsViewModel"
             Title="Boards">
    <Grid RowDefinitions="Auto,*" Padding="15" RowSpacing="10">

        <Grid ColumnDefinitions="*,Auto" ColumnSpacing="10">
            <Entry Placeholder="New board name" Text="{Binding NewBoardName}" />
            <Button Grid.Column="1" Text="Add" Command="{Binding AddBoardCommand}" />
        </Grid>

        <CollectionView Grid.Row="1" ItemsSource="{Binding Boards}">
            <CollectionView.EmptyView>
                <Label Text="No boards yet — add your first board above."
                       HorizontalOptions="Center" VerticalOptions="Center" />
            </CollectionView.EmptyView>
            <CollectionView.ItemTemplate>
                <DataTemplate x:DataType="models:Board">
                    <Grid ColumnDefinitions="*,Auto,Auto" Padding="0,8" ColumnSpacing="10">
                        <Label Text="{Binding Name}" VerticalOptions="Center" FontSize="18" />
                        <Button Grid.Column="1" Text="Rename"
                                Command="{Binding Source={RelativeSource AncestorType={x:Type vm:BoardsViewModel}}, Path=RenameBoardCommand}"
                                CommandParameter="{Binding .}" />
                        <Button Grid.Column="2" Text="Delete"
                                Command="{Binding Source={RelativeSource AncestorType={x:Type vm:BoardsViewModel}}, Path=ArchiveBoardCommand}"
                                CommandParameter="{Binding .}" />
                    </Grid>
                </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>
    </Grid>
</ContentPage>
```

`src/Esk8_Tracker/Views/BoardsPage.xaml.cs`:

```csharp
using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class BoardsPage : ContentPage
{
    private readonly BoardsViewModel _viewModel;

    public BoardsPage(BoardsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
```

- [ ] **Step 3: Register in DI**

In `MauiProgram.CreateMauiApp`, after the existing service registrations, add:

```csharp
            builder.Services.AddTransient<ViewModels.BoardsViewModel>();
            builder.Services.AddTransient<Views.BoardsPage>();
```

- [ ] **Step 4: Verify on Windows**

```powershell
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 -t:Run --nologo
```

Manual check: Boards tab → add "Test board" → appears; rename it; delete it (confirm dialog); add one back, close the app, run again → board persisted.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: boards management page"
```

---

### Task 9: Ride page — recording UI without the map

**Files:**
- Create: `src/Esk8_Tracker/Services/IRideRecordingController.cs`
- Create: `src/Esk8_Tracker/ViewModels/RideViewModel.cs`
- Modify: `src/Esk8_Tracker/Views/RidePage.xaml(.cs)`, `src/Esk8_Tracker/MauiProgram.cs`

**Interfaces:**
- Consumes: `RideRecorder` (Task 4), `Esk8Database.GetActiveBoardsAsync` (Task 5), `Format` (Task 6).
- Produces:
  - `interface IRideRecordingController { bool IsSupported { get; } void StartLocationService(int boardId); void StopLocationService(); }` — Android implementation lands in Task 10; `UnsupportedRideRecordingController` (IsSupported=false, no-op methods) is registered for every other platform.
  - `RideViewModel` — properties `Boards` (ObservableCollection<Board>), `SelectedBoard` (Board?), display strings `SpeedDisplay`/`DistanceDisplay`/`DurationDisplay`/`MaxDisplay`, `bool IsGpsLost`, state flags `IsIdle`/`IsRecording`/`IsPaused`/`IsSupported`, `string PauseResumeText`; commands `StartCommand`, `PauseResumeCommand`, `StopCommand`; method `Task OnAppearingAsync()`. Task 11's map code subscribes to the recorder directly — the VM handles numbers only.
- The last-used board id is kept in `Preferences` under key `"LastBoardId"`.

- [ ] **Step 1: Write the controller abstraction**

`src/Esk8_Tracker/Services/IRideRecordingController.cs`:

```csharp
namespace Esk8_Tracker.Services;

/// <summary>
/// Platform hook that keeps GPS fixes flowing to the RideRecorder while a ride
/// is active (a foreground service on Android). The RideRecorder itself is
/// started/stopped by the ViewModel in shared code.
/// </summary>
public interface IRideRecordingController
{
    bool IsSupported { get; }

    void StartLocationService(int boardId);

    void StopLocationService();
}

public class UnsupportedRideRecordingController : IRideRecordingController
{
    public bool IsSupported => false;

    public void StartLocationService(int boardId) { }

    public void StopLocationService() { }
}
```

- [ ] **Step 2: Write the ViewModel**

`src/Esk8_Tracker/ViewModels/RideViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;
using Esk8_Tracker.Core.Models;
using Esk8_Tracker.Services;

namespace Esk8_Tracker.ViewModels;

public partial class RideViewModel : ObservableObject
{
    private const string LastBoardKey = "LastBoardId";

    private readonly Esk8Database _db;
    private readonly RideRecorder _recorder;
    private readonly IRideRecordingController _controller;
    private IDispatcherTimer? _timer;

    public RideViewModel(Esk8Database db, RideRecorder recorder, IRideRecordingController controller)
    {
        _db = db;
        _recorder = recorder;
        _controller = controller;
        _recorder.StatsUpdated += OnStatsUpdated;
        _recorder.StateChanged += OnStateChanged;
    }

    public ObservableCollection<Board> Boards { get; } = new();

    [ObservableProperty]
    private Board? _selectedBoard;

    [ObservableProperty]
    private string _speedDisplay = "0.0";

    [ObservableProperty]
    private string _distanceDisplay = "0.00";

    [ObservableProperty]
    private string _durationDisplay = "0:00:00";

    [ObservableProperty]
    private string _maxDisplay = "0.0";

    [ObservableProperty]
    private bool _isGpsLost;

    public bool IsSupported => _controller.IsSupported;
    public bool IsIdle => _recorder.State == RecorderState.Idle;
    public bool IsRecording => _recorder.State == RecorderState.Recording;
    public bool IsPaused => _recorder.State == RecorderState.Paused;
    public bool IsActive => !IsIdle;
    public string PauseResumeText => IsPaused ? "Resume" : "Pause";

    public async Task OnAppearingAsync()
    {
        var boards = await _db.GetActiveBoardsAsync();
        var lastBoardId = Preferences.Get(LastBoardKey, 0);
        Boards.Clear();
        foreach (var board in boards)
            Boards.Add(board);
        SelectedBoard = Boards.FirstOrDefault(b => b.Id == lastBoardId) ?? Boards.FirstOrDefault();

        RefreshStateProperties();
        if (!IsIdle)
        {
            OnStatsUpdated(_recorder.CurrentStats);
            StartTimer();
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!IsSupported)
        {
            await Shell.Current.DisplayAlert("Not supported",
                "Ride recording only works in the Android app.", "OK");
            return;
        }
        if (SelectedBoard is null)
        {
            await Shell.Current.DisplayAlert("No board",
                "Add a board on the Boards tab before recording a ride.", "OK");
            return;
        }

        var location = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (location != PermissionStatus.Granted)
        {
            var openSettings = await Shell.Current.DisplayAlert("Location needed",
                "Esk8 Tracker needs location access to record your ride. " +
                "Grant it in system settings.", "Open settings", "Cancel");
            if (openSettings) AppInfo.ShowSettingsUI();
            return;
        }
        // Optional on Android 13+: only affects whether the recording
        // notification is visible in the drawer. Must NOT block recording.
        await Permissions.RequestAsync<Permissions.PostNotifications>();

        Preferences.Set(LastBoardKey, SelectedBoard.Id);
        await _recorder.StartAsync(SelectedBoard.Id, DateTime.UtcNow);
        _controller.StartLocationService(SelectedBoard.Id);
        StartTimer();
    }

    [RelayCommand]
    private void PauseResume()
    {
        if (IsPaused) _recorder.Resume();
        else if (IsRecording) _recorder.Pause();
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        var confirmed = await Shell.Current.DisplayAlert("Stop ride",
            "Finish and save this ride?", "Stop", "Keep riding");
        if (!confirmed) return;

        _controller.StopLocationService();
        await _recorder.StopAsync(DateTime.UtcNow);
        StopTimer();
        SpeedDisplay = "0.0";
        IsGpsLost = false;
    }

    private void OnStatsUpdated(RideLiveStats stats) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SpeedDisplay = Format.SpeedKmh(stats.CurrentSpeedMps);
            DistanceDisplay = Format.DistanceKm(stats.DistanceMeters);
            DurationDisplay = Format.Duration(stats.MovingSeconds);
            MaxDisplay = Format.SpeedKmh(stats.MaxSpeedMps);
        });

    private void OnStateChanged() =>
        MainThread.BeginInvokeOnMainThread(RefreshStateProperties);

    private void RefreshStateProperties()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(PauseResumeText));
    }

    private void StartTimer()
    {
        if (_timer is not null) return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            DurationDisplay = Format.Duration(_recorder.CurrentStats.MovingSeconds);
            IsGpsLost = _recorder.IsSignalLost(DateTime.UtcNow);
        };
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }
}
```

- [ ] **Step 3: Write the page**

`src/Esk8_Tracker/Views/RidePage.xaml` (replace the placeholder):

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Esk8_Tracker.ViewModels"
             x:Class="Esk8_Tracker.Views.RidePage"
             x:DataType="vm:RideViewModel"
             Title="Ride">
    <Grid RowDefinitions="*,Auto" >

        <Grid Grid.Row="0">
            <ContentView x:Name="MapHolder" />
            <Label Text="GPS signal lost"
                   IsVisible="{Binding IsGpsLost}"
                   BackgroundColor="#CCB00020" TextColor="White"
                   Padding="12,6" HorizontalOptions="Center" VerticalOptions="Start"
                   Margin="0,10,0,0" />
        </Grid>

        <VerticalStackLayout Grid.Row="1" Padding="15,10" Spacing="10">

            <Label Text="Recording is not supported on this platform — use the Android app."
                   IsVisible="{Binding IsSupported, Converter={StaticResource InvertedBoolConverter}}"
                   TextColor="OrangeRed" HorizontalOptions="Center" />

            <HorizontalStackLayout HorizontalOptions="Center" Spacing="5">
                <Label Text="{Binding SpeedDisplay}" FontSize="64" FontAttributes="Bold" />
                <Label Text="km/h" FontSize="20" VerticalOptions="End" Margin="0,0,0,12" />
            </HorizontalStackLayout>

            <Grid ColumnDefinitions="*,*,*" >
                <VerticalStackLayout HorizontalOptions="Center">
                    <Label Text="{Binding DistanceDisplay}" FontSize="22" HorizontalOptions="Center" />
                    <Label Text="km" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="1" HorizontalOptions="Center">
                    <Label Text="{Binding DurationDisplay}" FontSize="22" HorizontalOptions="Center" />
                    <Label Text="moving" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="2" HorizontalOptions="Center">
                    <Label Text="{Binding MaxDisplay}" FontSize="22" HorizontalOptions="Center" />
                    <Label Text="max km/h" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Grid>

            <Picker Title="Board"
                    ItemsSource="{Binding Boards}" ItemDisplayBinding="{Binding Name}"
                    SelectedItem="{Binding SelectedBoard}"
                    IsEnabled="{Binding IsIdle}" />

            <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
                <Button Text="Start ride" Command="{Binding StartCommand}"
                        IsVisible="{Binding IsIdle}" Grid.ColumnSpan="2" />
                <Button Text="{Binding PauseResumeText}" Command="{Binding PauseResumeCommand}"
                        IsVisible="{Binding IsActive}" />
                <Button Grid.Column="1" Text="Stop" Command="{Binding StopCommand}"
                        IsVisible="{Binding IsActive}" BackgroundColor="#B00020" TextColor="White" />
            </Grid>
        </VerticalStackLayout>
    </Grid>
</ContentPage>
```

The `InvertedBoolConverter` doesn't exist yet — add it once, app-wide, in `src/Esk8_Tracker/App.xaml` resources:

```xml
<Application ...>
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Resources/Styles/Colors.xaml" />
                <ResourceDictionary Source="Resources/Styles/Styles.xaml" />
            </ResourceDictionary.MergedDictionaries>
            <local:InvertedBoolConverter x:Key="InvertedBoolConverter" />
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

with `src/Esk8_Tracker/InvertedBoolConverter.cs`:

```csharp
using System.Globalization;

namespace Esk8_Tracker;

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;
}
```

`src/Esk8_Tracker/Views/RidePage.xaml.cs`:

```csharp
using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class RidePage : ContentPage
{
    private readonly RideViewModel _viewModel;

    public RidePage(RideViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
}
```

- [ ] **Step 4: Register in DI**

In `MauiProgram.CreateMauiApp` add (controller first — Android's own implementation replaces the `#else` branch in Task 10):

```csharp
            builder.Services.AddSingleton<Services.IRideRecordingController,
                Services.UnsupportedRideRecordingController>();
            builder.Services.AddSingleton<ViewModels.RideViewModel>();
            builder.Services.AddTransient<Views.RidePage>();
```

(`RideViewModel` is a singleton on purpose: it subscribes to the singleton recorder and must survive tab switches during a ride.)

- [ ] **Step 5: Verify on Windows**

```powershell
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 -t:Run --nologo
```

Manual check: Ride tab shows the "not supported" banner, board picker filled, Start shows the not-supported alert. Also: `dotnet build src/Esk8_Tracker -f net10.0-android --nologo` still passes.

- [ ] **Step 6: Commit**

```powershell
git add -A; git commit -m "feat: ride recording UI with live stats (no map yet)"
```

---

### Task 10: Android foreground service — pocket-proof GPS recording

**Files:**
- Modify: `src/Esk8_Tracker/Platforms/Android/AndroidManifest.xml`
- Create: `src/Esk8_Tracker/Platforms/Android/RideRecordingService.cs`
- Create: `src/Esk8_Tracker/Platforms/Android/AndroidRideRecordingController.cs`
- Modify: `src/Esk8_Tracker/MauiProgram.cs`

**Interfaces:**
- Consumes: `RideRecorder.OnFixAsync(GpsFix)` + `RecorderState` (Task 4), `IRideRecordingController` (Task 9).
- Produces: on Android, `IRideRecordingController` resolves to `AndroidRideRecordingController`, which starts/stops `RideRecordingService` — a `location`-type foreground service that requests ~1 Hz GPS updates and pumps them into the shared recorder. Permission ordering is already handled by Task 9's `StartAsync` (location permission granted *before* the service starts; otherwise Android 14+ throws `SecurityException` at `StartForeground`).

- [ ] **Step 1: Add the manifest permissions**

In `src/Esk8_Tracker/Platforms/Android/AndroidManifest.xml`, inside `<manifest>` alongside the existing `uses-permission` entries, add:

```xml
    <uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
    <uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
    <uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
    <uses-permission android:name="android.permission.FOREGROUND_SERVICE_LOCATION" />
    <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
```

(The `<service>` element is generated from the C# `[Service]` attribute — do not add it manually.)

- [ ] **Step 2: Write the foreground service**

`src/Esk8_Tracker/Platforms/Android/RideRecordingService.cs`:

```csharp
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using AndroidX.Core.App;
using Esk8_Tracker.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Esk8_Tracker;

[Service(Exported = false, Enabled = true, ForegroundServiceType = ForegroundService.TypeLocation)]
public class RideRecordingService : Service, ILocationListener
{
    private const int NotificationId = 1001; // must not be 0
    private const string ChannelId = "ride_recording";

    private LocationManager? _locationManager;
    private RideRecorder? _recorder;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        _recorder = IPlatformApplication.Current!.Services.GetRequiredService<RideRecorder>();

        CreateNotificationChannel();
        // Android 14+: throws unless FOREGROUND_SERVICE_LOCATION is declared and
        // fine/coarse location was granted BEFORE this call (Task 9 guarantees it).
        StartForeground(NotificationId, BuildNotification(), ForegroundService.TypeLocation);
        StartLocationUpdates();

        // NotSticky: if the process dies mid-ride, recorder state is gone anyway;
        // startup crash recovery (App.OnStart) finalizes the ride from saved points.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        _locationManager?.RemoveUpdates(this);
        _locationManager = null;
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        var channel = new NotificationChannel(ChannelId, "Ride recording",
            NotificationImportance.Low)
        {
            Description = "Shown while a ride is being recorded",
        };
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var openApp = new Intent(this, typeof(MainActivity));
        var pending = PendingIntent.GetActivity(this, 0, openApp,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Recording ride")
            .SetContentText("Esk8 Tracker is recording your ride")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetContentIntent(pending)
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .Build();
    }

    private void StartLocationUpdates()
    {
        _locationManager = (LocationManager)GetSystemService(LocationService)!;

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            var request = new LocationRequest.Builder(1000)
                .SetMinUpdateIntervalMillis(1000)
                .SetMinUpdateDistanceMeters(0f)
                .Build();
            _locationManager.RequestLocationUpdates(LocationManager.GpsProvider!, request, MainExecutor!, this);
        }
        else
        {
            _locationManager.RequestLocationUpdates(LocationManager.GpsProvider!, 1000, 0f, this, Looper.MainLooper);
        }
    }

    public void OnLocationChanged(Location location)
    {
        var recorder = _recorder;
        if (recorder is null) return;

        var fix = new GpsFix(
            DateTimeOffset.FromUnixTimeMilliseconds(location.Time).UtcDateTime,
            location.Latitude,
            location.Longitude,
            location.HasSpeed ? location.Speed : null,
            location.HasAccuracy ? location.Accuracy : double.MaxValue,
            location.HasAltitude ? location.Altitude : null);

        _ = recorder.OnFixAsync(fix); // fire-and-forget; recorder is main-thread bound like this callback
    }

    public void OnProviderDisabled(string provider) { }

    public void OnProviderEnabled(string provider) { }

    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
}
```

- [ ] **Step 3: Write the Android controller**

`src/Esk8_Tracker/Platforms/Android/AndroidRideRecordingController.cs`:

```csharp
using Android.Content;
using Esk8_Tracker.Services;

namespace Esk8_Tracker;

public class AndroidRideRecordingController : IRideRecordingController
{
    public bool IsSupported => true;

    public void StartLocationService(int boardId)
    {
        var context = Android.App.Application.Context;
        context.StartForegroundService(new Intent(context, typeof(RideRecordingService)));
    }

    public void StopLocationService()
    {
        var context = Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(RideRecordingService)));
    }
}
```

- [ ] **Step 4: Register per platform**

In `MauiProgram.CreateMauiApp`, replace the controller registration from Task 9 with:

```csharp
#if ANDROID
            builder.Services.AddSingleton<Services.IRideRecordingController, AndroidRideRecordingController>();
#else
            builder.Services.AddSingleton<Services.IRideRecordingController,
                Services.UnsupportedRideRecordingController>();
#endif
```

- [ ] **Step 5: Build both targets**

```powershell
dotnet build src/Esk8_Tracker -f net10.0-android --nologo
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 --nologo
```

Expected: both pass.

- [ ] **Step 6: Verify on the phone (requires the user's Android phone with USB debugging, or an emulator with simulated GPS)**

```powershell
dotnet build src/Esk8_Tracker -f net10.0-android -t:Run --nologo
```

Checklist (walk outside for real GPS):
1. Add a board, go to Ride, tap Start → location permission prompt → grant "While using the app" → notification permission → "Recording ride" notification appears.
2. Walk: speed/distance/duration tick up about once per second.
3. Screen off for ~2 minutes while walking, screen on: distance clearly grew during the dark period (this is the pocket-proof gate — if it did NOT grow, report; the documented follow-up is adding a partial wake lock, not silently accepting gaps).
4. Pause → numbers freeze; Resume → they continue; Stop → confirm → notification disappears.
5. Deny-permission path: revoke location in system settings, Start again → the explanatory alert with "Open settings" appears.

- [ ] **Step 7: Commit**

```powershell
git add -A; git commit -m "feat: Android foreground service for pocket-proof GPS recording"
```

---

### Task 11: Live map on the Ride tab (Mapsui)

**Files:**
- Create: `src/Esk8_Tracker/Maps/RideMap.cs` (ALL Mapsui usage stays in this one class)
- Modify: `src/Esk8_Tracker/Views/RidePage.xaml.cs`

**Interfaces:**
- Consumes: `RideRecorder` events `FixAccepted`/`StateChanged` and `RoutePoints` (Task 4).
- Produces: `class RideMap` — property `Mapsui.Map Map`; methods `void UpdatePosition(double lat, double lon)`, `void AppendRoutePoint(double lat, double lon)`, `void SetRoute(IEnumerable<(double Lat, double Lon)> points)` (replaces the route), `void ClearRoute()`, `void CenterOn(double lat, double lon)`, `void ZoomToRoute()`. Task 13 reuses this class for ride playback.
- Verified API notes (Mapsui 5.1): `.UseSkiaSharp()` was added in Task 7; OSM layer via `Mapsui.Tiling.OpenStreetMap.CreateTileLayer(userAgent)` with static `OpenStreetMap.DefaultCache` (`IPersistentCache<byte[]>`) for persistent tiles; position marker is `Mapsui.Layers.MyLocationLayer` (ctor takes the `Map`, `UpdateMyLocation(MPoint, animated)`); routes are `MemoryLayer` + `Mapsui.Nts.GeometryFeature` wrapping an NTS `LineString` in SphericalMercator coordinates.

- [ ] **Step 1: Write RideMap**

`src/Esk8_Tracker/Maps/RideMap.cs`:

```csharp
using BruTile.Cache;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;

namespace Esk8_Tracker.Maps;

/// <summary>
/// Wraps all Mapsui specifics: OSM base layer (with persistent tile cache),
/// a red route polyline, and the my-location marker. Callers speak lat/lon;
/// conversion to spherical-mercator map coordinates happens here.
/// </summary>
public class RideMap
{
    private readonly MemoryLayer _routeLayer;
    private readonly MyLocationLayer _locationLayer;
    private readonly List<Coordinate> _routeCoords = new();

    public Map Map { get; }

    public RideMap()
    {
        Map = new Map();

        // OSM tile-usage policy wants an identifying user agent; FileCache keeps
        // tiles on disk so frequently ridden areas work with patchy connectivity.
        OpenStreetMap.DefaultCache ??= new FileCache(
            Path.Combine(FileSystem.CacheDirectory, "osm-tiles"), "png");
        Map.Layers.Add(OpenStreetMap.CreateTileLayer("Esk8_Tracker/1.0 (personal ride tracker)"));

        _routeLayer = new MemoryLayer
        {
            Name = "Route",
            Features = Array.Empty<IFeature>(),
            Style = new VectorStyle { Line = new Pen { Color = Color.Red, Width = 4 } },
        };
        Map.Layers.Add(_routeLayer);

        _locationLayer = new MyLocationLayer(Map) { IsCentered = true };
        Map.Layers.Add(_locationLayer);
    }

    public void UpdatePosition(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        _locationLayer.UpdateMyLocation(new MPoint(x, y), animated: true);
    }

    public void AppendRoutePoint(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        _routeCoords.Add(new Coordinate(x, y));
        RebuildRouteLayer();
    }

    public void SetRoute(IEnumerable<(double Lat, double Lon)> points)
    {
        _routeCoords.Clear();
        foreach (var (lat, lon) in points)
        {
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            _routeCoords.Add(new Coordinate(x, y));
        }
        RebuildRouteLayer();
    }

    public void ClearRoute()
    {
        _routeCoords.Clear();
        RebuildRouteLayer();
    }

    public void CenterOn(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        // Resolutions[] runs from world (0) to street level; 17 is a riding zoom.
        var resolutions = Map.Navigator.Resolutions;
        var resolution = resolutions.Count > 17 ? resolutions[17] : resolutions[^1];
        Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), resolution);
    }

    public void ZoomToRoute()
    {
        if (_routeCoords.Count < 2) return;
        var rect = new MRect(
            _routeCoords.Min(c => c.X), _routeCoords.Min(c => c.Y),
            _routeCoords.Max(c => c.X), _routeCoords.Max(c => c.Y));
        Map.Navigator.ZoomToBox(rect.Grow(Math.Max(rect.Width, rect.Height) * 0.1 + 50));
    }

    private void RebuildRouteLayer()
    {
        // NTS LineString needs >= 2 coordinates.
        _routeLayer.Features = _routeCoords.Count < 2
            ? Array.Empty<IFeature>()
            : new[] { new GeometryFeature { Geometry = new LineString(_routeCoords.ToArray()) } };
        _routeLayer.DataHasChanged();
    }
}
```

- [ ] **Step 2: Host the map in RidePage**

Replace `src/Esk8_Tracker/Views/RidePage.xaml.cs` with:

```csharp
using Esk8_Tracker.Core;
using Esk8_Tracker.Maps;
using Esk8_Tracker.ViewModels;
using Mapsui.UI.Maui;

namespace Esk8_Tracker.Views;

public partial class RidePage : ContentPage
{
    private readonly RideViewModel _viewModel;
    private readonly RideRecorder _recorder;
    private readonly RideMap _rideMap = new();
    private bool _mapCentered;

    public RidePage(RideViewModel viewModel, RideRecorder recorder)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _recorder = recorder;

        MapHolder.Content = new MapControl { Map = _rideMap.Map };
        _recorder.FixAccepted += OnFixAccepted;
        _recorder.StateChanged += OnRecorderStateChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();

        // Returning to the tab mid-ride: rebuild the drawn route from the recorder.
        if (_recorder.State != RecorderState.Idle && _recorder.RoutePoints.Count > 0)
        {
            _rideMap.SetRoute(_recorder.RoutePoints);
            var (lat, lon) = _recorder.RoutePoints[^1];
            _rideMap.UpdatePosition(lat, lon);
            _mapCentered = true;
        }
        else if (!_mapCentered)
        {
            await CenterOnLastKnownPositionAsync();
        }
    }

    private async Task CenterOnLastKnownPositionAsync()
    {
        try
        {
            var last = await Geolocation.GetLastKnownLocationAsync();
            if (last is not null)
            {
                _rideMap.CenterOn(last.Latitude, last.Longitude);
                _mapCentered = true;
            }
        }
        catch (Exception)
        {
            // No permission yet or no cached position — the world view is fine.
        }
    }

    private void OnFixAccepted(GpsFix fix) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_mapCentered)
            {
                _rideMap.CenterOn(fix.Latitude, fix.Longitude);
                _mapCentered = true;
            }
            _rideMap.UpdatePosition(fix.Latitude, fix.Longitude);
            _rideMap.AppendRoutePoint(fix.Latitude, fix.Longitude);
        });

    private void OnRecorderStateChanged() =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // A fresh ride starts with a clean polyline.
            if (_recorder.State == RecorderState.Recording && _recorder.RoutePoints.Count == 0)
                _rideMap.ClearRoute();
        });
}
```

Also register the page's new constructor dependency — no DI change needed (`RideRecorder` is already a singleton registration).

- [ ] **Step 3: Verify on Windows, then on the phone**

```powershell
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 -t:Run --nologo
```

Expected on Windows: the Ride tab shows an OpenStreetMap world map (needs internet); pan/zoom works.

```powershell
dotnet build src/Esk8_Tracker -f net10.0-android -t:Run --nologo
```

Expected on the phone (outside): map centers on your position shortly after opening; during a ride the marker follows you, the map stays centered, and a red line grows behind you; leaving the tab and returning mid-ride redraws the full line.

- [ ] **Step 4: Commit**

```powershell
git add -A; git commit -m "feat: live OpenStreetMap view with position marker and route line"
```

---

### Task 12: History list

**Files:**
- Create: `src/Esk8_Tracker/ViewModels/HistoryViewModel.cs`
- Modify: `src/Esk8_Tracker/Views/HistoryPage.xaml(.cs)`, `src/Esk8_Tracker/MauiProgram.cs`

**Interfaces:**
- Consumes: `Esk8Database.GetCompletedRidesAsync` / `GetBoardNamesAsync` (Task 5), `Format` (Task 6).
- Produces: `record RideListItem(int RideId, string Title, string BoardName, string Distance, string Duration, string AvgSpeed, string MaxSpeed, bool WasRecovered)`; `HistoryViewModel` with `ObservableCollection<RideListItem> Rides`, `LoadCommand`, `OpenRideCommand(RideListItem)` navigating to `RideDetailPage?rideId=<id>` (Task 13 consumes that query parameter).

- [ ] **Step 1: Write the ViewModel**

`src/Esk8_Tracker/ViewModels/HistoryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.ViewModels;

public record RideListItem(
    int RideId,
    string Title,
    string BoardName,
    string Distance,
    string Duration,
    string AvgSpeed,
    string MaxSpeed,
    bool WasRecovered);

public partial class HistoryViewModel(Esk8Database db) : ObservableObject
{
    public ObservableCollection<RideListItem> Rides { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var rides = await db.GetCompletedRidesAsync();
        var names = await db.GetBoardNamesAsync();

        Rides.Clear();
        foreach (var ride in rides)
        {
            Rides.Add(new RideListItem(
                ride.Id,
                ride.StartedAt.ToLocalTime().ToString("ddd d MMM yyyy HH:mm"),
                names.TryGetValue(ride.BoardId, out var name) ? name : "(unknown board)",
                Format.DistanceKm(ride.DistanceMeters),
                Format.Duration(ride.MovingSeconds),
                Format.SpeedKmh(ride.AvgSpeedMps),
                Format.SpeedKmh(ride.MaxSpeedMps),
                ride.WasRecovered));
        }
    }

    [RelayCommand]
    private async Task OpenRideAsync(RideListItem item) =>
        await Shell.Current.GoToAsync($"{nameof(Views.RideDetailPage)}?rideId={item.RideId}");
}
```

- [ ] **Step 2: Write the page**

`src/Esk8_Tracker/Views/HistoryPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Esk8_Tracker.ViewModels"
             x:Class="Esk8_Tracker.Views.HistoryPage"
             x:DataType="vm:HistoryViewModel"
             Title="History">
    <CollectionView ItemsSource="{Binding Rides}">
        <CollectionView.EmptyView>
            <Label Text="No rides yet — record your first ride!"
                   HorizontalOptions="Center" VerticalOptions="Center" />
        </CollectionView.EmptyView>
        <CollectionView.ItemTemplate>
            <DataTemplate x:DataType="vm:RideListItem">
                <Border Padding="15,10" Margin="10,5" StrokeThickness="0">
                    <Border.GestureRecognizers>
                        <TapGestureRecognizer
                            Command="{Binding Source={RelativeSource AncestorType={x:Type vm:HistoryViewModel}}, Path=OpenRideCommand}"
                            CommandParameter="{Binding .}" />
                    </Border.GestureRecognizers>
                    <VerticalStackLayout Spacing="4">
                        <HorizontalStackLayout Spacing="8">
                            <Label Text="{Binding Title}" FontAttributes="Bold" FontSize="16" />
                            <Label Text="recovered" IsVisible="{Binding WasRecovered}"
                                   TextColor="OrangeRed" FontSize="12" VerticalOptions="Center" />
                        </HorizontalStackLayout>
                        <Label Text="{Binding BoardName}" FontSize="13" TextColor="Gray" />
                        <HorizontalStackLayout Spacing="15">
                            <Label FontSize="14">
                                <Label.FormattedText>
                                    <FormattedString>
                                        <Span Text="{Binding Distance}" FontAttributes="Bold" />
                                        <Span Text=" km" />
                                    </FormattedString>
                                </Label.FormattedText>
                            </Label>
                            <Label Text="{Binding Duration}" FontSize="14" />
                            <Label FontSize="14">
                                <Label.FormattedText>
                                    <FormattedString>
                                        <Span Text="max " />
                                        <Span Text="{Binding MaxSpeed}" FontAttributes="Bold" />
                                        <Span Text=" km/h" />
                                    </FormattedString>
                                </Label.FormattedText>
                            </Label>
                        </HorizontalStackLayout>
                    </VerticalStackLayout>
                </Border>
            </DataTemplate>
        </CollectionView.ItemTemplate>
    </CollectionView>
</ContentPage>
```

`src/Esk8_Tracker/Views/HistoryPage.xaml.cs`:

```csharp
using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
```

- [ ] **Step 3: Register in DI**

In `MauiProgram.CreateMauiApp` add:

```csharp
            builder.Services.AddTransient<ViewModels.HistoryViewModel>();
            builder.Services.AddTransient<Views.HistoryPage>();
```

- [ ] **Step 4: Verify on Windows**

Run the Windows app. There is no completed ride on Windows, so verify the empty view; if a phone ride from Task 10 exists, verify on the phone instead: rides listed newest-first with correct numbers, tap navigates (detail page is still the placeholder). Both builds must pass.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: ride history list"
```

---

### Task 13: Ride detail with route playback

**Files:**
- Create: `src/Esk8_Tracker/ViewModels/RideDetailViewModel.cs`
- Modify: `src/Esk8_Tracker/Views/RideDetailPage.xaml(.cs)`, `src/Esk8_Tracker/MauiProgram.cs`

**Interfaces:**
- Consumes: `Esk8Database.GetRideAsync`/`GetTrackPointsAsync`/`GetBoardNamesAsync` (Task 5), `Format` (Task 6), `RideMap` (Task 11), route registration `RideDetailPage` + query `rideId` (Tasks 7/12).
- Produces: `RideDetailViewModel` with `[QueryProperty] int RideId`, display properties, `List<(double Lat, double Lon)> RoutePoints`, and `Task LoadAsync()`.

- [ ] **Step 1: Write the ViewModel**

`src/Esk8_Tracker/ViewModels/RideDetailViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.ViewModels;

[QueryProperty(nameof(RideId), "rideId")]
public partial class RideDetailViewModel(Esk8Database db) : ObservableObject
{
    public int RideId { get; set; }

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _boardName = "";

    [ObservableProperty]
    private string _distance = "";

    [ObservableProperty]
    private string _duration = "";

    [ObservableProperty]
    private string _avgSpeed = "";

    [ObservableProperty]
    private string _maxSpeed = "";

    [ObservableProperty]
    private bool _wasRecovered;

    public List<(double Lat, double Lon)> RoutePoints { get; } = new();

    public async Task LoadAsync()
    {
        var ride = await db.GetRideAsync(RideId);
        if (ride is null) return;

        var names = await db.GetBoardNamesAsync();
        Title = ride.StartedAt.ToLocalTime().ToString("ddd d MMM yyyy HH:mm");
        BoardName = names.TryGetValue(ride.BoardId, out var name) ? name : "(unknown board)";
        Distance = Format.DistanceKm(ride.DistanceMeters);
        Duration = Format.Duration(ride.MovingSeconds);
        AvgSpeed = Format.SpeedKmh(ride.AvgSpeedMps);
        MaxSpeed = Format.SpeedKmh(ride.MaxSpeedMps);
        WasRecovered = ride.WasRecovered;

        var points = await db.GetTrackPointsAsync(RideId);
        RoutePoints.Clear();
        RoutePoints.AddRange(points.Select(p => (p.Latitude, p.Longitude)));
    }
}
```

- [ ] **Step 2: Write the page**

`src/Esk8_Tracker/Views/RideDetailPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Esk8_Tracker.ViewModels"
             x:Class="Esk8_Tracker.Views.RideDetailPage"
             x:DataType="vm:RideDetailViewModel"
             Title="Ride">
    <Grid RowDefinitions="*,Auto">
        <ContentView x:Name="MapHolder" />
        <VerticalStackLayout Grid.Row="1" Padding="15,10" Spacing="6">
            <HorizontalStackLayout Spacing="8">
                <Label Text="{Binding Title}" FontAttributes="Bold" FontSize="18" />
                <Label Text="recovered" IsVisible="{Binding WasRecovered}"
                       TextColor="OrangeRed" FontSize="12" VerticalOptions="Center" />
            </HorizontalStackLayout>
            <Label Text="{Binding BoardName}" TextColor="Gray" />
            <Grid ColumnDefinitions="*,*,*,*">
                <VerticalStackLayout HorizontalOptions="Center">
                    <Label Text="{Binding Distance}" FontSize="20" HorizontalOptions="Center" />
                    <Label Text="km" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="1" HorizontalOptions="Center">
                    <Label Text="{Binding Duration}" FontSize="20" HorizontalOptions="Center" />
                    <Label Text="moving" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="2" HorizontalOptions="Center">
                    <Label Text="{Binding AvgSpeed}" FontSize="20" HorizontalOptions="Center" />
                    <Label Text="avg km/h" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="3" HorizontalOptions="Center">
                    <Label Text="{Binding MaxSpeed}" FontSize="20" HorizontalOptions="Center" />
                    <Label Text="max km/h" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Grid>
        </VerticalStackLayout>
    </Grid>
</ContentPage>
```

`src/Esk8_Tracker/Views/RideDetailPage.xaml.cs`:

```csharp
using Esk8_Tracker.Maps;
using Esk8_Tracker.ViewModels;
using Mapsui.UI.Maui;

namespace Esk8_Tracker.Views;

public partial class RideDetailPage : ContentPage
{
    private readonly RideDetailViewModel _viewModel;
    private readonly RideMap _rideMap = new();

    public RideDetailPage(RideDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        MapHolder.Content = new MapControl { Map = _rideMap.Map };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        _rideMap.SetRoute(_viewModel.RoutePoints);
        _rideMap.ZoomToRoute();
    }
}
```

- [ ] **Step 3: Register in DI**

```csharp
            builder.Services.AddTransient<ViewModels.RideDetailViewModel>();
            builder.Services.AddTransient<Views.RideDetailPage>();
```

- [ ] **Step 4: Verify**

Both builds pass. On the phone: History → tap the test ride → detail shows the full route line, zoomed to fit, stats matching the list row.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: ride detail page with route playback"
```

---

### Task 14: Stats dashboard

**Files:**
- Create: `src/Esk8_Tracker/ViewModels/StatsViewModel.cs`
- Modify: `src/Esk8_Tracker/Views/StatsPage.xaml(.cs)`, `src/Esk8_Tracker/MauiProgram.cs`

**Interfaces:**
- Consumes: `Esk8Database.GetDashboardStatsAsync` → `DashboardStats`/`MonthlyStat`/`BoardStat` (Task 6), `Format` (Task 6).
- Produces: `record MonthlyDisplay(string Month, string Distance, int Rides)`, `record BoardDisplay(string Board, string Distance, int Rides)`, `StatsViewModel` with totals/records strings, the two collections, and `LoadCommand`.

- [ ] **Step 1: Write the ViewModel**

`src/Esk8_Tracker/ViewModels/StatsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Esk8_Tracker.Core;
using Esk8_Tracker.Core.Data;

namespace Esk8_Tracker.ViewModels;

public record MonthlyDisplay(string Month, string Distance, int Rides);

public record BoardDisplay(string Board, string Distance, int Rides);

public partial class StatsViewModel(Esk8Database db) : ObservableObject
{
    [ObservableProperty]
    private string _totalDistance = "0.00";

    [ObservableProperty]
    private string _totalRides = "0";

    [ObservableProperty]
    private string _totalTime = "0:00:00";

    [ObservableProperty]
    private string _topSpeed = "0.0";

    [ObservableProperty]
    private string _longestRide = "0.00";

    public ObservableCollection<MonthlyDisplay> Monthly { get; } = new();
    public ObservableCollection<BoardDisplay> PerBoard { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var stats = await db.GetDashboardStatsAsync();

        TotalDistance = Format.DistanceKm(stats.TotalDistanceMeters);
        TotalRides = stats.RideCount.ToString();
        TotalTime = Format.Duration(stats.TotalMovingSeconds);
        TopSpeed = Format.SpeedKmh(stats.TopSpeedMps);
        LongestRide = Format.DistanceKm(stats.LongestRideMeters);

        Monthly.Clear();
        foreach (var m in stats.Monthly)
            Monthly.Add(new MonthlyDisplay(
                new DateTime(m.Year, m.Month, 1).ToString("MMMM yyyy"),
                Format.DistanceKm(m.DistanceMeters), m.RideCount));

        PerBoard.Clear();
        foreach (var b in stats.PerBoard)
            PerBoard.Add(new BoardDisplay(b.BoardName,
                Format.DistanceKm(b.DistanceMeters), b.RideCount));
    }
}
```

- [ ] **Step 2: Write the page**

`src/Esk8_Tracker/Views/StatsPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:Esk8_Tracker.ViewModels"
             x:Class="Esk8_Tracker.Views.StatsPage"
             x:DataType="vm:StatsViewModel"
             Title="Stats">
    <ScrollView>
        <VerticalStackLayout Padding="15" Spacing="20">

            <Label Text="All time" FontAttributes="Bold" FontSize="18" />
            <Grid ColumnDefinitions="*,*,*" >
                <VerticalStackLayout HorizontalOptions="Center">
                    <Label Text="{Binding TotalDistance}" FontSize="24" FontAttributes="Bold" HorizontalOptions="Center" />
                    <Label Text="km" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="1" HorizontalOptions="Center">
                    <Label Text="{Binding TotalRides}" FontSize="24" FontAttributes="Bold" HorizontalOptions="Center" />
                    <Label Text="rides" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="2" HorizontalOptions="Center">
                    <Label Text="{Binding TotalTime}" FontSize="24" FontAttributes="Bold" HorizontalOptions="Center" />
                    <Label Text="riding time" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Grid>

            <Label Text="Records" FontAttributes="Bold" FontSize="18" />
            <Grid ColumnDefinitions="*,*" >
                <VerticalStackLayout HorizontalOptions="Center">
                    <Label Text="{Binding TopSpeed}" FontSize="24" FontAttributes="Bold" HorizontalOptions="Center" />
                    <Label Text="top km/h" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
                <VerticalStackLayout Grid.Column="1" HorizontalOptions="Center">
                    <Label Text="{Binding LongestRide}" FontSize="24" FontAttributes="Bold" HorizontalOptions="Center" />
                    <Label Text="longest km" FontSize="12" HorizontalOptions="Center" />
                </VerticalStackLayout>
            </Grid>

            <Label Text="By month" FontAttributes="Bold" FontSize="18" />
            <VerticalStackLayout BindableLayout.ItemsSource="{Binding Monthly}" Spacing="6">
                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="vm:MonthlyDisplay">
                        <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="12">
                            <Label Text="{Binding Month}" />
                            <Label Grid.Column="1" Text="{Binding Distance}" FontAttributes="Bold" />
                            <Label Grid.Column="2" Text="{Binding Rides, StringFormat='{0} rides'}" TextColor="Gray" />
                        </Grid>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>
            </VerticalStackLayout>

            <Label Text="By board" FontAttributes="Bold" FontSize="18" />
            <VerticalStackLayout BindableLayout.ItemsSource="{Binding PerBoard}" Spacing="6">
                <BindableLayout.ItemTemplate>
                    <DataTemplate x:DataType="vm:BoardDisplay">
                        <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="12">
                            <Label Text="{Binding Board}" />
                            <Label Grid.Column="1" Text="{Binding Distance}" FontAttributes="Bold" />
                            <Label Grid.Column="2" Text="{Binding Rides, StringFormat='{0} rides'}" TextColor="Gray" />
                        </Grid>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>
            </VerticalStackLayout>

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

`src/Esk8_Tracker/Views/StatsPage.xaml.cs`:

```csharp
using Esk8_Tracker.ViewModels;

namespace Esk8_Tracker.Views;

public partial class StatsPage : ContentPage
{
    private readonly StatsViewModel _viewModel;

    public StatsPage(StatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
```

- [ ] **Step 3: Register in DI**

```csharp
            builder.Services.AddTransient<ViewModels.StatsViewModel>();
            builder.Services.AddTransient<Views.StatsPage>();
```

- [ ] **Step 4: Verify**

Both builds pass; on the phone the Stats tab reflects the recorded test rides (totals, records, one monthly row, per-board rows).

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: stats dashboard"
```

---

### Task 15: Final verification — full suite + on-device end-to-end

**Files:** none (verification only; fix-forward anything found and commit fixes individually).

- [ ] **Step 1: Full automated check**

```powershell
dotnet test tests/Esk8_Tracker.Core.Tests --nologo
dotnet build src/Esk8_Tracker -f net10.0-android --nologo
dotnet build src/Esk8_Tracker -f net10.0-windows10.0.19041.0 --nologo
```

Expected: 50 tests pass, both builds succeed with no new warnings.

- [ ] **Step 2: On-device end-to-end ride (the user rides or walks; agent provides this checklist)**

1. Fresh ride with screen off in pocket for several minutes → distance/route complete afterwards.
2. Pause at a stop, resume, finish → moving time excludes the pause.
3. Ride appears in History with correct board; detail map matches the actual route.
4. Stats tab totals/records update accordingly.
5. Crash recovery: start a ride, force-stop the app (swipe away from recents while recording), reopen → the partial ride appears in History flagged "recovered", with plausible stats. (Note: swiping away also kills the service — that's exactly the scenario recovery exists for.)
6. Battery sanity: a ~30 min recording should not visibly drain the phone (a few percent is normal for 1 Hz GPS).

- [ ] **Step 3: Close out**

Report results to the user, including anything that failed and was fixed. Any behavior deviating from the spec (`docs/superpowers/specs/2026-07-02-esk8-tracker-design.md`) that cannot be fixed on the spot gets reported, not hidden.

---

## Plan Self-Review Notes

- Spec coverage: live map+stats while riding (Tasks 9–11), pocket-proof FGS (Task 10), history+detail playback (Tasks 12–13), dashboard (Tasks 6/14), boards+archival (Tasks 5/8), metric units + moving-time duration (Global Constraints, Format in Task 6), crash recovery (Tasks 5/7, verified in 15), GPS-loss indicator (Tasks 4/9), fix filtering thresholds (Task 3), Windows dev preview with recording disabled (Tasks 9/10), permissions flow (Tasks 9/10).
- Deliberately deferred to execution: exact template `App.xaml.cs` shape (noted inline in Task 7), possible `DisplayAlert` obsolete-warning rename (noted in Task 8).
- Known risk gates: Mapsui-on-net10 restore/build (Task 7 Step 6 stops if it fails), screen-off GPS delivery (Task 10 Step 6 point 3).
