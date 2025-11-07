using System.Net.Http.Json;

namespace eShopSupport.Backend.Clients;

/// <summary>
/// HTTP client for calling the AgentService
/// </summary>
public class AgentServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentServiceClient> _logger;

    public AgentServiceClient(HttpClient httpClient, ILogger<AgentServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Requests research for a ticket from the AgentService
    /// </summary>
    public async Task<TicketResearchResponse?> RequestTicketResearchAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Requesting research for ticket {TicketId} from AgentService", ticketId);

            var request = new { TicketId = ticketId };
            var response = await _httpClient.PostAsJsonAsync("/api/agent/ticket-research", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AgentService returned error {StatusCode} for ticket {TicketId}", response.StatusCode, ticketId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TicketResearchResponse>(cancellationToken: cancellationToken);
            _logger.LogInformation("Received research result for ticket {TicketId}", ticketId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request research for ticket {TicketId}", ticketId);
            return null;
        }
    }

    /// <summary>
    /// Requests full triage (research + draft) for a ticket from the AgentService
    /// </summary>
    public async Task<TicketTriageResponse?> RequestTicketTriageAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Requesting triage for ticket {TicketId} from AgentService", ticketId);

            var request = new { TicketId = ticketId };
            var response = await _httpClient.PostAsJsonAsync("/api/agent/ticket-triage", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AgentService returned error {StatusCode} for ticket {TicketId}", response.StatusCode, ticketId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TicketTriageResponse>(cancellationToken: cancellationToken);
            _logger.LogInformation("Received triage result for ticket {TicketId}", ticketId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request triage for ticket {TicketId}", ticketId);
            return null;
        }
    }
}

public class TicketResearchResponse
{
    public bool Success { get; set; }
    public int ExecutionId { get; set; }
    public TicketResearchResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TicketResearchResult
{
    public required string CustomerContext { get; set; }
    public required string RelevantKnowledge { get; set; }
    public required List<string> SuggestedActions { get; set; }
    public List<string> ToolCallsMade { get; set; } = new();

    public string ToMarkdown()
    {
        var suggestedActionsText = string.Join("\n", SuggestedActions);

        return $"""
            ## 📊 Customer Context
            {CustomerContext}

            ## 📚 Relevant Knowledge
            {RelevantKnowledge}

            ## ✅ Suggested Actions
            {suggestedActionsText}
            """;
    }
}

public class TicketTriageResponse
{
    public bool Success { get; set; }
    public int TriageExecutionId { get; set; }
    public int ResearchExecutionId { get; set; }
    public int DraftExecutionId { get; set; }
    public int DraftId { get; set; }
    public TicketTriageResult? Triage { get; set; }
    public TicketResearchResult? Research { get; set; }
    public DraftResponseResult? Draft { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TicketTriageResult
{
    public required string TicketType { get; set; }
    public required int PriorityScore { get; set; }
    public required string UrgencyLevel { get; set; }
    public required string ClassificationRationale { get; set; }
    public List<SimilarTicketInfo> SimilarTickets { get; set; } = new();
    public string? RecommendedAgent { get; set; }
    public List<string> RecommendedNextSteps { get; set; } = new();
    public bool RequiresImmediateEscalation { get; set; }

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

public class SimilarTicketInfo
{
    public required int TicketId { get; set; }
    public required string Summary { get; set; }
    public string? Resolution { get; set; }
}

public class DraftResponseResult
{
    public required string DraftContent { get; set; }
    public double Confidence { get; set; }
    public string? Rationale { get; set; }
}
