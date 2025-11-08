using eShopSupport.AgentService.Models;
using eShopSupport.AgentService.Tools;
using eShopSupport.Backend.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace eShopSupport.AgentService.Agents;

/// <summary>
/// Agent that performs comprehensive research on a ticket to assist support staff
/// </summary>
public class ResearchAgent : DelegatingAIAgent
{
    private readonly ILogger<ResearchAgent> _logger;

    public ResearchAgent(IChatClient chatClient, IServiceProvider services, ILogger<ResearchAgent> logger, AppDbContext dbContext)
        : base(CreateConfiguredAgent(chatClient, dbContext))
    {
        _logger = logger;
    }

    private static ChatClientAgent CreateConfiguredAgent(IChatClient chatClient, AppDbContext dbContext)
    {
        // Prepare tools for the agent
        var tools = new TicketTools(dbContext);
        var availableTools = new[]
        {
            AIFunctionFactory.Create(tools.GetCustomerContext),
            AIFunctionFactory.Create(tools.SearchTicketHistory),
            AIFunctionFactory.Create(tools.GetProductInfo),
            AIFunctionFactory.Create(tools.GetTicketDetails)
        };

        // Create the agent with instructions and tools - configured once at startup
        return chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Name = "research_agent",
            Instructions = """
                You are a highly skilled AI research assistant helping support staff at AdventureWorks.

                Your task is to research a support ticket thoroughly and provide comprehensive context to help the support agent respond effectively.

                **Your Research Goals:**
                1. Use available tools to gather customer history, similar past tickets, and product information
                2. Synthesize findings into clear, actionable insights
                3. Suggest specific next steps for the support agent
                4. Draft a professional, empathetic response the agent can use

                **Available Tools:**
                - GetCustomerContext: Get customer account details and past ticket history
                - SearchTicketHistory: Find similar resolved tickets for reference
                - GetProductInfo: Get detailed product information
                - GetTicketDetails: Get full ticket conversation

                **Instructions:**
                First, think about what information would be most helpful. Then use the tools to gather that information.
                After gathering information, provide your findings in this exact JSON structure:

                {
                  "customerContext": "Brief summary of customer account, history, and sentiment",
                  "relevantKnowledge": "Key findings from similar tickets, product info, and knowledge base",
                  "suggestedActions": ["Action 1", "Action 2", "Action 3"],
                  "toolCallsMade": ["List of tools you called"]
                }

                Be thorough in your research but concise in your findings. Focus on actionable insights.
                Note: Do NOT generate a draft response - that will be handled by a specialized agent.
                """,
            ChatOptions = new ChatOptions
            {
                Tools = availableTools.Cast<AITool>().ToList(),
                Temperature = 0.3f,
                ResponseFormat = ChatResponseFormat.Json,
                AdditionalProperties = new() { ["seed"] = 0 }
            }
        });
    }

    /// <summary>
    /// Conducts research on a ticket and returns structured findings
    /// </summary>
    public async Task<TicketResearchResult> ResearchTicketAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting research for ticket {TicketId}", ticketId);

        // Create a new thread for this research session
        var thread = GetNewThread();

        // Execute the agent
        _logger.LogInformation("Executing agent for ticket {TicketId}", ticketId);

        var response = await RunAsync(
            $"Please research ticket {ticketId} thoroughly and provide your findings in the required JSON format.",
            thread);

        var responseText = response.ToString();

        _logger.LogInformation("Received response from agent: {Length} characters", responseText.Length);

        // Parse the structured response
        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<TicketResearchResult>(
                responseText,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize research result");
            }

            _logger.LogInformation("Research completed successfully for ticket {TicketId}", ticketId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse research result. Raw response: {Response}", responseText);

            // Fallback: return a basic result
            return new TicketResearchResult
            {
                CustomerContext = "Unable to parse research results",
                RelevantKnowledge = responseText,
                SuggestedActions = new List<string>
                {
                    "1. Review the raw research output",
                    "2. Manually investigate the issue"
                },
                ToolCallsMade = new List<string> { "Error occurred during research" }
            };
        }
    }
}
