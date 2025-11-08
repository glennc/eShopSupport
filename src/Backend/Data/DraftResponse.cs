using System.ComponentModel.DataAnnotations;

namespace eShopSupport.Backend.Data;

/// <summary>
/// Represents an AI-generated draft response awaiting staff approval
/// </summary>
public class DraftResponse
{
    [Key]
    public int DraftResponseId { get; set; }

    /// <summary>
    /// The ticket this draft response is for
    /// </summary>
    public required int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// Link to the agent execution that generated this draft
    /// </summary>
    public int? AgentExecutionId { get; set; }

    /// <summary>
    /// The draft response content
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Agent's confidence in this response (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Factors that contributed to the confidence calculation
    /// </summary>
    public string? ConfidenceFactors { get; set; }

    /// <summary>
    /// Why the agent generated this response
    /// </summary>
    public string? Rationale { get; set; }

    /// <summary>
    /// Status of the draft (pending, approved, rejected, edited)
    /// </summary>
    public required string Status { get; set; } = "pending";

    /// <summary>
    /// When the draft was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When staff reviewed the draft
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Who reviewed the draft
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Final content after staff edits (if edited)
    /// </summary>
    public string? FinalContent { get; set; }

    /// <summary>
    /// Reason for rejection (if rejected)
    /// </summary>
    public string? RejectionReason { get; set; }
}
