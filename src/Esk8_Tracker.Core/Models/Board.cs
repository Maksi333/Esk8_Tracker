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
