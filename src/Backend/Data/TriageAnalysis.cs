using System.ComponentModel.DataAnnotations;

namespace eShopSupport.Backend.Data;

/// <summary>
/// Represents an AI-generated triage analysis for a ticket
/// </summary>
public class TriageAnalysis
{
    [Key]
    public int TriageAnalysisId { get; set; }

    /// <summary>
    /// The ticket this triage analysis is for
    /// </summary>
    public required int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// Link to the agent execution that generated this triage
    /// </summary>
    public int? AgentExecutionId { get; set; }

    /// <summary>
    /// Ticket type classification
    /// </summary>
    public required string TicketType { get; set; }

    /// <summary>
    /// Priority score (0-10)
    /// </summary>
    public required int PriorityScore { get; set; }

    /// <summary>
    /// Urgency level (Critical, High, Medium, Low)
    /// </summary>
    public required string UrgencyLevel { get; set; }

    /// <summary>
    /// Recommended agent to handle this ticket
    /// </summary>
    public string? RecommendedAgent { get; set; }

    /// <summary>
    /// Whether this requires immediate escalation
    /// </summary>
    public bool RequiresEscalation { get; set; }

    /// <summary>
    /// When the triage was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
