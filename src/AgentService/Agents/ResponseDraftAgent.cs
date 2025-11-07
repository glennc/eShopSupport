using eShopSupport.AgentService.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace eShopSupport.AgentService.Agents;

/// <summary>
/// Agent that generates draft responses for staff to review and approve
/// </summary>
public class ResponseDraftAgent
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ResponseDraftAgent> _logger;

    public ResponseDraftAgent(IChatClient chatClient, ILogger<ResponseDraftAgent> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
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

        // Create the agent with instructions for drafting responses
        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
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

                Return your draft in this exact JSON structure:
                {
                  "draftContent": "The complete draft response to send to the customer",
                  "confidence": 0.85,
                  "rationale": "Brief explanation of why you wrote the response this way and what key points you addressed"
                }

                The confidence should be between 0.0 and 1.0, representing how confident you are that this response fully addresses the customer's needs.
                """,
            ChatOptions = new ChatOptions
            {
                Temperature = 0.7f, // Slightly higher for more natural language
                ResponseFormat = ChatResponseFormat.Json
            }
        });

        _logger.LogInformation("Created draft agent");

        // Create a new thread for this drafting session
        var thread = agent.GetNewThread();

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

        var response = await agent.RunAsync(researchContext, thread);

        var responseText = response.ToString();

        _logger.LogInformation("Received draft from agent: {Length} characters", responseText.Length);

        // Parse the structured response
        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<DraftResponseResult>(
                responseText,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize draft result");
            }

            _logger.LogInformation("Draft generated successfully for ticket {TicketId} with confidence {Confidence:F2}",
                ticketId, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse draft result. Raw response: {Response}", responseText);

            // Fallback: return a basic draft with low confidence
            return new DraftResponseResult
            {
                DraftContent = "Thank you for contacting AdventureWorks support. We have reviewed your issue and a support specialist will respond shortly with detailed assistance.",
                Confidence = 0.3,
                Rationale = "Unable to parse agent response. Generic fallback draft generated - manual review strongly recommended."
            };
        }
    }
}

/// <summary>
/// Result from the ResponseDraftAgent
/// </summary>
public class DraftResponseResult
{
    public required string DraftContent { get; set; }
    public double Confidence { get; set; }
    public string? Rationale { get; set; }
}
