namespace eShopSupport.AgentService.Models;

/// <summary>
/// Structured result from the TriageAgent containing classification, priority, and routing information
/// </summary>
public class TicketTriageResult
{
    /// <summary>
    /// The classified ticket type (e.g., Question, Complaint, Return Request, Technical Issue, Other)
    /// </summary>
    public required string TicketType { get; set; }

    /// <summary>
    /// Priority score from 1-10, where 10 is most urgent
    /// </summary>
    public required int PriorityScore { get; set; }

    /// <summary>
    /// Urgency level classification (Low, Medium, High, Critical)
    /// </summary>
    public required string UrgencyLevel { get; set; }

    /// <summary>
    /// Reasoning behind the classification and priority score
    /// </summary>
    public required string ClassificationRationale { get; set; }

    /// <summary>
    /// List of similar resolved tickets found (ticket IDs and brief descriptions)
    /// </summary>
    public List<SimilarTicket> SimilarTickets { get; set; } = new();

    /// <summary>
    /// Recommended specialist agent to handle this ticket (Research, Order, Product, Shipping)
    /// </summary>
    public string? RecommendedAgent { get; set; }

    /// <summary>
    /// Immediate next steps recommended based on triage
    /// </summary>
    public List<string> RecommendedNextSteps { get; set; } = new();

    /// <summary>
    /// Whether this ticket needs escalation to a human immediately
    /// </summary>
    public bool RequiresImmediateEscalation { get; set; }

    /// <summary>
    /// Converts the triage result into a formatted markdown string for display
    /// </summary>
    public string ToMarkdown()
    {
        var urgencyEmoji = UrgencyLevel switch
        {
            "Critical" => "🔴",
            "High" => "🟠",
            "Medium" => "🟡",
            "Low" => "🟢",
            _ => "⚪"
        };

        var nextStepsText = RecommendedNextSteps.Count > 0
            ? string.Join("\n", RecommendedNextSteps.Select(s => $"- {s}"))
            : "- Proceed with standard research and response";

        var similarTicketsText = SimilarTickets.Count > 0
            ? string.Join("\n", SimilarTickets.Select(t => $"- **Ticket #{t.TicketId}**: {t.Summary} (Resolved: {t.Resolution})"))
            : "No similar tickets found.";

        var escalationNote = RequiresImmediateEscalation
            ? "\n\n⚠️ **ESCALATION REQUIRED**: This ticket needs immediate human attention."
            : "";

        return $"""
            ## 🎯 Triage Analysis

            **Type**: {TicketType}
            **Priority**: {urgencyEmoji} {UrgencyLevel} (Score: {PriorityScore}/10)
            **Recommended Agent**: {RecommendedAgent ?? "Research (default)"}

            ### Classification Rationale
            {ClassificationRationale}

            ### Similar Resolved Tickets
            {similarTicketsText}

            ### Recommended Next Steps
            {nextStepsText}{escalationNote}
            """;
    }
}

/// <summary>
/// Information about a similar ticket found during triage
/// </summary>
public class SimilarTicket
{
    public required int TicketId { get; set; }
    public required string Summary { get; set; }
    public string? Resolution { get; set; }
}
