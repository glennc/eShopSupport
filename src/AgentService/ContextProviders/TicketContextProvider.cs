using eShopSupport.Backend.Data;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;

namespace eShopSupport.AgentService.ContextProviders;

/// <summary>
/// Provides ticket-specific context to agents, including customer info, conversation history, and product details
/// </summary>
public class TicketContextProvider : AIContextProvider
{
    private readonly AppDbContext _dbContext;
    private readonly int _ticketId;

    public TicketContextProvider(AppDbContext dbContext, int ticketId)
    {
        _dbContext = dbContext;
        _ticketId = ticketId;
    }

    public override async ValueTask<AIContext> InvokingAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        // Load ticket with all related data
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .Include(t => t.Customer)
            .Include(t => t.Product)
            .FirstOrDefaultAsync(t => t.TicketId == _ticketId, cancellationToken);

        if (ticket == null)
        {
            return new AIContext
            {
                Instructions = $"Ticket {_ticketId} not found."
            };
        }

        // Build conversation history from customer messages
        var customerMessages = ticket.Messages
            .Where(m => m.MessageType == MessageType.Customer)
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Text)
            .ToList();

        var latestCustomerMessage = customerMessages.LastOrDefault() ?? "No customer message";
        var conversationHistory = string.Join("\n", customerMessages.Select((m, i) => $"Message {i + 1}: {m}"));

        // Inject ticket context into agent instructions
        var instructions = $$"""
            **Ticket Information:**
            - Ticket ID: {{_ticketId}}
            - Customer: {{ticket.Customer.FullName}} (ID: {{ticket.CustomerId}})
            - Product: {{(ticket.Product != null ? $"{ticket.Product.Brand} {ticket.Product.Model}" : "Not specified")}} {{(ticket.ProductId.HasValue ? $"(ID: {ticket.ProductId})" : "")}}
            - Status: {{ticket.TicketStatus}}
            - Created: {{ticket.CreatedAt:yyyy-MM-dd HH:mm}}

            **Customer's Issue:**
            {{latestCustomerMessage}}

            **Full Conversation History:**
            {{conversationHistory}}
            """;

        return new AIContext
        {
            Instructions = instructions
        };
    }
}
