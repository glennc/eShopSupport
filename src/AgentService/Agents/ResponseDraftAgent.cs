using eShopSupport.AgentService.Models;
using eShopSupport.AgentService.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace eShopSupport.AgentService.Agents;

/// <summary>
/// Agent that generates draft responses for staff to review and approve
/// </summary>
public class ResponseDraftAgent : DelegatingAIAgent
{
    private readonly ILogger<ResponseDraftAgent> _logger;
    private readonly DraftConfidenceCalculator _confidenceCalculator;

    public ResponseDraftAgent(
        [FromKeyedServices("eShopSupportModel")]IChatClient chatClient,
        DraftConfidenceCalculator confidenceCalculator,
        ILogger<ResponseDraftAgent> logger)
        : base(CreateConfiguredAgent(chatClient))
    {
        _logger = logger;
        _confidenceCalculator = confidenceCalculator;
    }

    private static ChatClientAgent CreateConfiguredAgent(IChatClient chatClient)
    {
        // Create the agent with instructions for drafting responses - configured once at startup
        return chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "response_draft_agent",
            Instructions = """
                You are a professional customer support response writer at AdventureWorks.

                Your task is to create a polished, empathetic draft response that a support agent can review and send to the customer.

                **Writing Guidelines:**
                1. Be professional, warm, and empathetic
                2. Address the customer's concerns directly
                3. Provide clear, actionable information
                4. Use the research findings to ensure accuracy
                5. Keep the tone conversational but professional
                6. End with an invitation for follow-up if needed

                **Important:**
                - This draft will be reviewed by a human before sending
                - Include all necessary information from the research
                - Be specific with product details, policies, or steps
                - If uncertain about something, flag it for staff review
                - DO NOT include placeholder text like "[Your Name]", "[Agent Name]", or similar
                - Either omit the signature entirely or sign as "The AdventureWorks Support Team"
                - The response must be ready to send without requiring manual name insertion

                You will receive research findings about the ticket. Use this information to craft a helpful response.

                IMPORTANT: You must return ONLY valid JSON in this EXACT structure with these EXACT field names:
                {
                  "draftContent": "The complete draft response to send to the customer",
                  "rationale": "Brief explanation of why you wrote the response this way and what key points you addressed"
                }

                Do NOT use any other field names like "response", "content", "message", etc.
                Use ONLY "draftContent" and "rationale" as shown above.

                Note: The confidence score will be calculated automatically based on objective criteria.
                """,
            ChatOptions = new ChatOptions
            {
                Temperature = 0.7f, // Slightly higher for more natural language
                ResponseFormat = ChatResponseFormat.Json
            }
        });
    }

    /// <summary>
    /// Generates a draft response based on research findings
    /// </summary>
    public async Task<DraftResponseResult> GenerateDraftAsync(
        TicketResearchResult research,
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating draft response for ticket {TicketId}", ticketId);

        // Create a new thread for this drafting session
        var thread = GetNewThread();

        // Build the context from research
        var researchContext = $"""
            **Research Findings:**

            **Customer Context:**
            {research.CustomerContext}

            **Relevant Knowledge:**
            {research.RelevantKnowledge}

            **Suggested Actions:**
            {string.Join("\n", research.SuggestedActions.Select((a, i) => $"{i + 1}. {a}"))}

            Please generate a polished, professional draft response based on this research that addresses the customer's issue comprehensively.
            """;

        _logger.LogInformation("Executing draft agent for ticket {TicketId}", ticketId);

        var response = await RunAsync(researchContext, thread);

        var responseText = response.ToString();

        _logger.LogInformation("Received draft from agent: {Length} characters", responseText.Length);

        // Parse the structured response
        try
        {
            // Parse the LLM response (without confidence field)
            var parsedResponse = System.Text.Json.JsonSerializer.Deserialize<DraftResponseParsed>(
                responseText,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsedResponse == null || string.IsNullOrWhiteSpace(parsedResponse.DraftContent))
            {
                throw new InvalidOperationException("Failed to deserialize draft result or draft content is empty");
            }

            // Calculate objective confidence score using Phi-4-mini-instruct
            var confidenceResult = await _confidenceCalculator.CalculateConfidenceAsync(
                research,
                parsedResponse.DraftContent,
                ticketId,
                cancellationToken);

            _logger.LogInformation(
                "Draft generated successfully for ticket {TicketId} with calculated confidence {Confidence:P0}",
                ticketId, confidenceResult.Score);

            return new DraftResponseResult
            {
                DraftContent = parsedResponse.DraftContent,
                Confidence = confidenceResult.Score,
                ConfidenceFactors = confidenceResult.Factors,
                Rationale = parsedResponse.Rationale
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse draft result. Raw response: {Response}", responseText);

            // Fallback: return a basic draft with low confidence
            var fallbackDraft = "Thank you for contacting AdventureWorks support. We have reviewed your issue and a support specialist will respond shortly with detailed assistance.";
            var fallbackConfidenceResult = await _confidenceCalculator.CalculateConfidenceAsync(
                research,
                fallbackDraft,
                ticketId,
                cancellationToken);

            return new DraftResponseResult
            {
                DraftContent = fallbackDraft,
                Confidence = Math.Min(fallbackConfidenceResult.Score, 0.3), // Cap at 30% for fallback
                ConfidenceFactors = fallbackConfidenceResult.Factors,
                Rationale = "Unable to parse agent response. Generic fallback draft generated - manual review strongly recommended."
            };
        }
    }
}

/// <summary>
/// Internal model for parsing LLM response (without confidence)
/// </summary>
internal class DraftResponseParsed
{
    public required string DraftContent { get; set; }
    public string? Rationale { get; set; }
}

/// <summary>
/// Result from the ResponseDraftAgent with calculated confidence
/// </summary>
public class DraftResponseResult
{
    public required string DraftContent { get; set; }
    public double Confidence { get; set; }
    public List<string> ConfidenceFactors { get; set; } = new();
    public string? Rationale { get; set; }
}
