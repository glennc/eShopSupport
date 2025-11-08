namespace eShopSupport.Backend.Data;

public class TriageSettings
{
    public int Id { get; set; }

    public bool AutomaticTriageEnabled { get; set; }

    public DateTime LastModified { get; set; }

    // Status tracking fields
    public string CurrentStatus { get; set; } = "Idle"; // "Idle", "Running", "Processing", "Error"

    public DateTime? LastRunTime { get; set; }

    public int TicketsProcessedLastRun { get; set; }

    public int TotalTicketsProcessed { get; set; }

    public string? CurrentActivity { get; set; } // e.g., "Processing ticket #123"

    public DateTime? CurrentActivityStarted { get; set; }
}
