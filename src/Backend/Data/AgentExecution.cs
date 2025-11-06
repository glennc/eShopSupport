namespace eShopSupport.Backend.Data;

/// <summary>
/// Tracks each execution of an agent workflow
/// </summary>
public class AgentExecution
{
    public int AgentExecutionId { get; set; }

    /// <summary>
    /// Name of the agent that executed (e.g., "ResearchAgent", "TriageAgent")
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// ID of the ticket this execution relates to
    /// </summary>
    public int TicketId { get; set; }

    /// <summary>
    /// When the agent execution started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the agent execution completed (null if still running or failed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Success, Failed, Timeout, etc.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Error message if the execution failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Total token count used during this execution
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// ID of the message created by this agent (if any)
    /// </summary>
    public int? ResultMessageId { get; set; }

    /// <summary>
    /// Collection of trace steps for this execution
    /// </summary>
    public List<AgentTrace> Traces { get; set; } = new();
}
