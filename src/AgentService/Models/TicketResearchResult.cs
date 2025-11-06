namespace eShopSupport.AgentService.Models;

/// <summary>
/// Structured result from the ResearchAgent containing all research sections
/// </summary>
public class TicketResearchResult
{
    /// <summary>
    /// Customer context summary (account info, order history, past tickets, sentiment)
    /// </summary>
    public required string CustomerContext { get; set; }

    /// <summary>
    /// Relevant knowledge from manuals, FAQs, and similar tickets
    /// </summary>
    public required string RelevantKnowledge { get; set; }

    /// <summary>
    /// Suggested actions for the support staff
    /// </summary>
    public required List<string> SuggestedActions { get; set; }

    /// <summary>
    /// AI-generated draft response that staff can edit and send
    /// </summary>
    public required string DraftResponse { get; set; }

    /// <summary>
    /// List of tool calls made during research (for transparency)
    /// </summary>
    public List<string> ToolCallsMade { get; set; } = new();

    /// <summary>
    /// Converts the research into a formatted markdown string for display
    /// </summary>
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

            ## ✍️ Draft Response
            {DraftResponse}
            """;
    }
}
