using System.ComponentModel;
using eShopSupport.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace eShopSupport.AgentService.Tools;

/// <summary>
/// Tools for agents to research ticket context and gather information
/// </summary>
public class TicketTools
{
    private readonly AppDbContext _dbContext;

    public TicketTools(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Description("Gets comprehensive customer information including account details, order history, and past ticket interactions")]
    public async Task<string> GetCustomerContext(
        [Description("The customer ID to look up")] int customerId)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
        {
            return "Customer not found";
        }

        // Get past tickets for this customer
        var pastTickets = await _dbContext.Tickets
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new
            {
                t.TicketId,
                t.CreatedAt,
                t.TicketStatus,
                t.ShortSummary,
                MessageCount = t.Messages.Count
            })
            .ToListAsync();

        var context = $"""
            **Customer**: {customer.FullName}
            **Customer ID**: {customerId}

            **Past Tickets** ({pastTickets.Count} recent):
            """;

        foreach (var ticket in pastTickets)
        {
            context += $"\n- Ticket #{ticket.TicketId} ({ticket.CreatedAt:yyyy-MM-dd}): {ticket.TicketStatus} - {ticket.ShortSummary ?? "No summary"} ({ticket.MessageCount} messages)";
        }

        return context;
    }

    [Description("Searches for similar resolved tickets to find solutions that worked before")]
    public async Task<string> SearchTicketHistory(
        [Description("Search keywords to find similar tickets")] string keywords)
    {
        // Simple keyword search in ticket summaries and first messages
        var tickets = await _dbContext.Tickets
            .Where(t => t.TicketStatus == eShopSupport.ServiceDefaults.Clients.Backend.TicketStatus.Closed &&
                       (t.ShortSummary!.Contains(keywords) || t.LongSummary!.Contains(keywords)))
            .OrderByDescending(t => t.CreatedAt)
            .Take(3)
            .Include(t => t.Messages)
            .ToListAsync();

        if (!tickets.Any())
        {
            return $"No similar resolved tickets found for keywords: {keywords}";
        }

        var result = $"**Similar Resolved Tickets** ({tickets.Count} found):\n\n";

        foreach (var ticket in tickets)
        {
            result += $"**Ticket #{ticket.TicketId}** (Resolved {ticket.CreatedAt:yyyy-MM-dd})\n";
            result += $"Summary: {ticket.ShortSummary ?? "N/A"}\n";

            // Get the resolution (usually the last staff message)
            var resolutionMessage = ticket.Messages
                .Where(m => m.MessageType == MessageType.Staff)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            if (resolutionMessage != null)
            {
                result += $"Resolution: {resolutionMessage.Text.Substring(0, Math.Min(200, resolutionMessage.Text.Length))}...\n\n";
            }
        }

        return result;
    }

    [Description("Gets detailed information about a specific product including brand, model, and description")]
    public async Task<string> GetProductInfo(
        [Description("The product ID to look up")] int productId)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
        {
            return "Product not found";
        }

        return $"""
            **Product**: {product.Brand} {product.Model}
            **Product ID**: {productId}
            **Description**: {product.Description}
            **Price**: ${product.Price}
            """;
    }

    [Description("Retrieves current ticket details including all messages and metadata")]
    public async Task<string> GetTicketDetails(
        [Description("The ticket ID to retrieve")] int ticketId)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .Include(t => t.Product)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
        {
            return "Ticket not found";
        }

        var details = $"""
            **Ticket #{ticketId}**
            **Status**: {ticket.TicketStatus}
            **Type**: {ticket.TicketType}
            **Created**: {ticket.CreatedAt:yyyy-MM-dd HH:mm}
            **Customer**: {ticket.Customer.FullName} (ID: {ticket.CustomerId})
            **Product**: {(ticket.Product != null ? $"{ticket.Product.Brand} {ticket.Product.Model}" : "N/A")}

            **Messages** ({ticket.Messages.Count}):
            """;

        foreach (var message in ticket.Messages.OrderBy(m => m.CreatedAt))
        {
            var sender = message.MessageType switch
            {
                MessageType.Customer => ticket.Customer.FullName,
                MessageType.Staff => "Support Staff",
                MessageType.AgentResearch => "AI Agent (Internal)",
                _ => "Unknown"
            };

            details += $"\n\n[{message.CreatedAt:HH:mm}] **{sender}**: {message.Text}";
        }

        return details;
    }
}
