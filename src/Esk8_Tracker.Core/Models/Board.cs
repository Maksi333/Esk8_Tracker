using SQLite;

namespace Esk8_Tracker.Core.Models;

public static class WheelTypes
{
    public const string Street = "Street urethane";
    public const string AllTerrain = "All-terrain pneumatic";
    public const string Custom = "Custom";

    public static readonly string[] All = { Street, AllTerrain, Custom };
}

public class Board
{
    /// <summary>Board accent palette from the design handoff (user picks per board).</summary>
    public static readonly string[] Colors =
        { "#4C8DFF", "#F5C51E", "#22C55E", "#F03E3E", "#B47CFF" };

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>Board accent color used on cards, chips and route start markers.</summary>
    public string ColorHex { get; set; } = "#4C8DFF";

    public string WheelType { get; set; } = WheelTypes.Street;

    /// <summary>Battery cell layout, e.g. "10S4P". Informational.</summary>
    public string BatteryCells { get; set; } = "";

    /// <summary>Battery capacity in watt-hours; powers the range estimate when set.</summary>
    public double? BatteryWh { get; set; }

    /// <summary>Top speed in km/h. Scales the velocity ramp and the speedometer.</summary>
    public double TopSpeedKmh { get; set; } = 40;

    public string Notes { get; set; } = "";

    [Ignore]
    public double TopSpeedMps => TopSpeedKmh / 3.6;
}
