using System.Text;
using eShopSupport.AgentService.Models;
using eShopSupport.AgentService.Tools;
using eShopSupport.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace eShopSupport.AgentService.Agents;

/// <summary>
/// Agent that performs comprehensive research on a ticket to assist support staff
/// </summary>
public class ResearchAgent
{
    private readonly IChatClient _chatClient;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ResearchAgent> _logger;

    public ResearchAgent(IChatClient chatClient, AppDbContext dbContext, ILogger<ResearchAgent> logger)
    {
        _chatClient = chatClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Conducts research on a ticket and returns structured findings
    /// </summary>
    public async Task<TicketResearchResult> ResearchTicketAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting research for ticket {TicketId}", ticketId);

        // Get ticket details
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .Include(t => t.Customer)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, cancellationToken);

        if (ticket == null)
        {
            throw new ArgumentException($"Ticket {ticketId} not found", nameof(ticketId));
        }

        // Prepare tools for the agent
        var tools = new TicketTools(_dbContext);
        var availableTools = new[]
        {
            AIFunctionFactory.Create(tools.GetCustomerContext),
            AIFunctionFactory.Create(tools.SearchTicketHistory),
            AIFunctionFactory.Create(tools.GetProductInfo),
            AIFunctionFactory.Create(tools.GetTicketDetails)
        };

        // Build conversation context
        var customerMessages = ticket.Messages
            .Where(m => m.MessageType == MessageType.Customer)
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Text)
            .ToList();

        var latestCustomerMessage = customerMessages.LastOrDefault() ?? "No customer message";
        var conversationHistory = string.Join("\n", customerMessages.Select((m, i) => $"Message {i + 1}: {m}"));

        // Create system prompt for research
        var systemPrompt = $$"""
            You are a highly skilled AI research assistant helping support staff at AdventureWorks.

            Your task is to research a support ticket thoroughly and provide comprehensive context to help the support agent respond effectively.

            **Ticket Information:**
            - Ticket ID: {{ticketId}}
            - Customer: {{ticket.Customer.FullName}} (ID: {{ticket.CustomerId}})
            - Product: {{(ticket.Product != null ? $"{ticket.Product.Brand} {ticket.Product.Model}" : "Not specified")}} {{(ticket.ProductId.HasValue ? $"(ID: {ticket.ProductId})" : "")}}
            - Status: {{ticket.TicketStatus}}
            - Created: {{ticket.CreatedAt:yyyy-MM-dd HH:mm}}

            **Customer's Issue:**
            {{latestCustomerMessage}}

            **Full Conversation History:**
            {{conversationHistory}}

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
              "draftResponse": "A professional, empathetic draft response addressing the customer's issue",
              "toolCallsMade": ["List of tools you called"]
            }

            Be thorough in your research but concise in your findings. Focus on actionable insights.
            """;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, "Please research this ticket thoroughly and provide your findings in the required JSON format.")
        };

        // Execute research with tools
        var chatOptions = new ChatOptions
        {
            Temperature = 0.3f,
            Tools = availableTools.Cast<AITool>().ToList(),
            ResponseFormat = ChatResponseFormat.Json,
            AdditionalProperties = new() { ["seed"] = 0 }
        };

        _logger.LogInformation("Calling LLM for research with {ToolCount} tools available", availableTools.Length);

        var response = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        var responseText = response.ToString();

        _logger.LogInformation("Received response from LLM: {Length} characters", responseText.Length);

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
                DraftResponse = "Thank you for contacting us. We're looking into your issue and will get back to you shortly.",
                ToolCallsMade = new List<string> { "Error occurred during research" }
            };
        }
    }
}
