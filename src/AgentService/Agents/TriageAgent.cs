using eShopSupport.AgentService.Models;
using eShopSupport.AgentService.Tools;
using eShopSupport.Backend.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace eShopSupport.AgentService.Agents;

/// <summary>
/// Agent that performs initial triage on incoming tickets to classify, prioritize, and route them appropriately
/// </summary>
public class TriageAgent : DelegatingAIAgent
{
    private readonly ILogger<TriageAgent> _logger;

    public TriageAgent([FromKeyedServices("eShopSupportModel")] IChatClient chatClient,
                                                                AppDbContext dbContext,
                                                                ILogger<TriageAgent> logger)
        : base(CreateConfiguredAgent(chatClient, dbContext))
    {
        _logger = logger;
    }

    private static ChatClientAgent CreateConfiguredAgent(IChatClient chatClient, AppDbContext dbContext)
    {
        // Prepare tools for the agent
        var triageTools = new TriageTools(dbContext);
        var ticketTools = new TicketTools(dbContext);

        var availableTools = new[]
        {
            AIFunctionFactory.Create(triageTools.ClassifyTicketType),
            AIFunctionFactory.Create(triageTools.CalculatePriorityScore),
            AIFunctionFactory.Create(triageTools.SearchSimilarTickets),
            AIFunctionFactory.Create(triageTools.CheckEscalationRequired),
            AIFunctionFactory.Create(ticketTools.GetTicketDetails)
        };

        // Create the agent with instructions and tools - configured once at startup
        return chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "triage_agent",
            Instructions = """
                You are an intelligent triage agent for AdventureWorks support system.

                Your role is to analyze incoming support tickets and provide structured triage information to help route and prioritize them effectively.

                **Your Triage Goals:**
                1. Classify the ticket type (Question, Complaint, Return Request, Technical Issue, Account Issue, Shipping Issue, or Other)
                2. Calculate a priority score and urgency level based on the content, customer history, and timing
                3. Search for similar resolved tickets that might provide useful context
                4. Determine if immediate human escalation is required
                5. Recommend which specialist agent should handle the ticket and what next steps to take

                **Available Tools:**
                - GetTicketDetails: Get the full ticket conversation and metadata
                - ClassifyTicketType: Analyze the ticket to determine its type
                - CalculatePriorityScore: Score the ticket's urgency from 1-10
                - SearchSimilarTickets: Find similar tickets for context
                - CheckEscalationRequired: Determine if immediate human intervention is needed

                **Instructions:**
                1. First, get the ticket details to understand what you're dealing with
                2. Use the classification and priority tools to analyze the ticket
                3. Search for similar tickets to see if there are patterns or precedents
                4. Check if escalation is required
                5. Based on all findings, recommend the appropriate specialist agent and next steps

                **Agent Routing Guidelines:**
                - "Research" (default): For general questions and issues needing investigation
                - "Order": For order status, cancellations, modifications
                - "Product": For product questions, specifications, compatibility
                - "Shipping": For delivery issues, tracking, logistics
                - "Account": For login, password, profile issues
                - "Escalation": For issues requiring immediate human attention

                **Response Format:**
                Provide your triage analysis in this exact JSON structure:

                {
                  "ticketType": "The classified ticket type",
                  "priorityScore": 5,
                  "urgencyLevel": "Low/Medium/High/Critical",
                  "classificationRationale": "Brief explanation of why you classified it this way",
                  "similarTickets": [
                    {
                      "ticketId": 123,
                      "summary": "Brief description",
                      "resolution": "How it was resolved"
                    }
                  ],
                  "recommendedAgent": "Which specialist agent should handle this",
                  "recommendedNextSteps": ["Step 1", "Step 2", "Step 3"],
                  "requiresImmediateEscalation": false
                }

                Be thorough but efficient in your analysis. Focus on actionable triage decisions.
                """,
            ChatOptions = new ChatOptions
            {
                Tools = availableTools.Cast<AITool>().ToList(),
                Temperature = 0.2f, // Lower temperature for more consistent triage decisions
                ResponseFormat = ChatResponseFormat.Json,
                AdditionalProperties = new() { ["seed"] = 0 }
            }
        });
    }

    /// <summary>
    /// Performs triage on a ticket and returns classification, priority, and routing recommendations
    /// </summary>
    public async Task<TicketTriageResult> TriageTicketAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting triage for ticket {TicketId}", ticketId);

        // Create a new thread for this triage session
        var thread = GetNewThread();

        // Execute the agent
        _logger.LogInformation("Executing triage agent for ticket {TicketId}", ticketId);

        var response = await RunAsync(
            $"Please perform triage on ticket {ticketId} and provide your analysis in the required JSON format.",
            thread);

        var responseText = response.ToString();

        _logger.LogInformation("Received triage response: {Length} characters", responseText.Length);

        // Parse the structured response
        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<TicketTriageResult>(
                responseText,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize triage result");
            }

            _logger.LogInformation(
                "Triage completed successfully for ticket {TicketId}: Type={Type}, Priority={Priority}, Agent={Agent}",
                ticketId, result.TicketType, result.PriorityScore, result.RecommendedAgent);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse triage result. Raw response: {Response}", responseText);

            // Fallback: return a basic triage result
            return new TicketTriageResult
            {
                TicketType = "Other",
                PriorityScore = 5,
                UrgencyLevel = "Medium",
                ClassificationRationale = $"Unable to parse triage results. Error: {ex.Message}",
                SimilarTickets = new List<SimilarTicket>(),
                RecommendedAgent = "Research",
                RecommendedNextSteps = new List<string>
                {
                    "Review the ticket manually",
                    "Perform standard research and investigation"
                },
                RequiresImmediateEscalation = false
            };
        }
    }
}
