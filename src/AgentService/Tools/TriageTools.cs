using System.ComponentModel;
using eShopSupport.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace eShopSupport.AgentService.Tools;

/// <summary>
/// Tools for the TriageAgent to classify, prioritize, and route tickets
/// </summary>
public class TriageTools
{
    private readonly AppDbContext _dbContext;

    public TriageTools(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Description("Analyzes ticket content to classify the type of issue (Question, Complaint, Return Request, Technical Issue, Account Issue, Shipping Issue, or Other)")]
    public async Task<string> ClassifyTicketType(
        [Description("The ticket ID to classify")] int ticketId)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
        {
            return "Ticket not found";
        }

        // Get the initial customer message
        var initialMessage = ticket.Messages
            .Where(m => m.MessageType == MessageType.Customer)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefault();

        var messageText = initialMessage?.Text?.ToLower() ?? "";
        var summary = (ticket.ShortSummary ?? ticket.LongSummary ?? "").ToLower();
        var combinedText = $"{summary} {messageText}";

        // Simple keyword-based classification (in a real system, this might use ML)
        var classification = new Dictionary<string, string[]>
        {
            ["Return Request"] = ["return", "refund", "send back", "don't want", "changed my mind"],
            ["Complaint"] = ["disappointed", "terrible", "awful", "angry", "unacceptable", "poor quality", "complaint"],
            ["Technical Issue"] = ["not working", "broken", "error", "won't turn on", "malfunction", "defective", "bug"],
            ["Shipping Issue"] = ["delivery", "shipped", "tracking", "arrived", "lost package", "delayed"],
            ["Account Issue"] = ["login", "password", "account", "can't access", "forgot password", "locked out"],
            ["Question"] = ["how do i", "how to", "what is", "can you", "could you", "question about", "wondering"]
        };

        foreach (var (type, keywords) in classification)
        {
            if (keywords.Any(keyword => combinedText.Contains(keyword)))
            {
                return $"""
                    **Classified Type**: {type}
                    **Ticket ID**: {ticketId}
                    **Initial Message**: {(initialMessage?.Text != null ? initialMessage.Text.Substring(0, Math.Min(150, initialMessage.Text.Length)) : "N/A")}...
                    **Confidence**: High (keyword match)
                    """;
            }
        }

        return $"""
            **Classified Type**: Other
            **Ticket ID**: {ticketId}
            **Initial Message**: {(initialMessage?.Text != null ? initialMessage.Text.Substring(0, Math.Min(150, initialMessage.Text.Length)) : "N/A")}...
            **Confidence**: Low (no clear keyword match)
            """;
    }

    [Description("Calculates a priority score (1-10) based on urgency signals in the ticket content, customer history, and ticket age")]
    public async Task<string> CalculatePriorityScore(
        [Description("The ticket ID to score")] int ticketId)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
        {
            return "Ticket not found";
        }

        int score = 5; // Base priority
        var reasons = new List<string>();

        // Get the initial customer message
        var initialMessage = ticket.Messages
            .Where(m => m.MessageType == MessageType.Customer)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefault();

        var messageText = initialMessage?.Text?.ToLower() ?? "";

        // Check for urgency keywords
        var urgentKeywords = new[] { "urgent", "asap", "immediately", "emergency", "critical", "broken", "not working" };
        if (urgentKeywords.Any(keyword => messageText.Contains(keyword)))
        {
            score += 2;
            reasons.Add("Contains urgency keywords");
        }

        // Check for frustration/complaint keywords
        var frustrationKeywords = new[] { "disappointed", "angry", "frustrated", "terrible", "awful", "unacceptable" };
        if (frustrationKeywords.Any(keyword => messageText.Contains(keyword)))
        {
            score += 1;
            reasons.Add("Customer expresses frustration");
        }

        // Check ticket age (older tickets get higher priority)
        var age = DateTime.UtcNow - ticket.CreatedAt;
        if (age.TotalHours > 48)
        {
            score += 2;
            reasons.Add($"Ticket is {age.TotalHours:F1} hours old");
        }
        else if (age.TotalHours > 24)
        {
            score += 1;
            reasons.Add($"Ticket is {age.TotalHours:F1} hours old");
        }

        // Check customer history - repeat customers with multiple tickets
        var customerTicketCount = await _dbContext.Tickets
            .CountAsync(t => t.CustomerId == ticket.CustomerId);

        if (customerTicketCount > 5)
        {
            score += 1;
            reasons.Add($"Customer has {customerTicketCount} total tickets");
        }

        // Cap score at 10
        score = Math.Min(10, score);

        // Determine urgency level
        var urgencyLevel = score switch
        {
            >= 9 => "Critical",
            >= 7 => "High",
            >= 4 => "Medium",
            _ => "Low"
        };

        return $"""
            **Priority Score**: {score}/10
            **Urgency Level**: {urgencyLevel}
            **Ticket ID**: {ticketId}
            **Reasoning**: {string.Join("; ", reasons)}
            **Customer**: {ticket.Customer.FullName}
            **Created**: {ticket.CreatedAt:yyyy-MM-dd HH:mm}
            """;
    }

    [Description("Searches for similar tickets (both open and resolved) to identify patterns and potential solutions")]
    public async Task<string> SearchSimilarTickets(
        [Description("The ticket ID to find similar tickets for")] int ticketId,
        [Description("Optional search keywords to narrow the search")] string? keywords = null)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
        {
            return "Ticket not found";
        }

        // Use provided keywords or extract from ticket
        string searchText = keywords ?? ticket.ShortSummary ?? ticket.LongSummary ?? "";

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return "No search text available";
        }

        // Search for similar tickets (excluding the current one)
        var similarTickets = await _dbContext.Tickets
            .Where(t => t.TicketId != ticketId &&
                       (t.ShortSummary!.Contains(searchText) ||
                        t.LongSummary!.Contains(searchText) ||
                        t.TicketType == ticket.TicketType))
            .OrderByDescending(t => t.TicketStatus == eShopSupport.ServiceDefaults.Clients.Backend.TicketStatus.Closed) // Prioritize resolved tickets
            .ThenByDescending(t => t.CreatedAt)
            .Take(5)
            .Include(t => t.Messages)
            .ToListAsync();

        if (!similarTickets.Any())
        {
            return $"No similar tickets found for: {searchText}";
        }

        var result = $"**Similar Tickets Found** ({similarTickets.Count}):\n\n";

        foreach (var similar in similarTickets)
        {
            result += $"**Ticket #{similar.TicketId}** - {similar.TicketStatus}\n";
            result += $"Type: {similar.TicketType}\n";
            result += $"Summary: {similar.ShortSummary ?? "N/A"}\n";

            if (similar.TicketStatus == eShopSupport.ServiceDefaults.Clients.Backend.TicketStatus.Closed)
            {
                // Get the resolution (last staff message)
                var resolutionMessage = similar.Messages
                    .Where(m => m.MessageType == MessageType.Staff)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                if (resolutionMessage != null)
                {
                    var preview = resolutionMessage.Text.Length > 150
                        ? resolutionMessage.Text.Substring(0, 150) + "..."
                        : resolutionMessage.Text;
                    result += $"Resolution: {preview}\n";
                }
            }

            result += "\n";
        }

        return result;
    }

    [Description("Determines if a ticket requires immediate escalation to a human based on severity indicators")]
    public async Task<string> CheckEscalationRequired(
        [Description("The ticket ID to check")] int ticketId)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Messages)
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.TicketId == ticketId);

        if (ticket == null)
        {
            return "Ticket not found";
        }

        var escalationReasons = new List<string>();
        var initialMessage = ticket.Messages
            .Where(m => m.MessageType == MessageType.Customer)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefault();

        var messageText = initialMessage?.Text?.ToLower() ?? "";

        // Check for severe complaint keywords
        var severeKeywords = new[] { "lawyer", "legal action", "sue", "lawsuit", "consumer protection", "better business bureau", "bbb" };
        if (severeKeywords.Any(keyword => messageText.Contains(keyword)))
        {
            escalationReasons.Add("Legal threat or severe complaint");
        }

        // Check for safety issues
        var safetyKeywords = new[] { "injured", "hurt", "dangerous", "safety", "fire", "explosion", "burn" };
        if (safetyKeywords.Any(keyword => messageText.Contains(keyword)))
        {
            escalationReasons.Add("Safety concern reported");
        }

        // Check for refund requests over certain threshold (if we had order data)
        // This is a placeholder - in real system would check order amount

        // Check ticket age
        var age = DateTime.UtcNow - ticket.CreatedAt;
        if (age.TotalHours > 72)
        {
            escalationReasons.Add($"Ticket is {age.TotalDays:F1} days old without resolution");
        }

        var requiresEscalation = escalationReasons.Any();

        return $"""
            **Escalation Required**: {(requiresEscalation ? "YES" : "NO")}
            **Ticket ID**: {ticketId}
            **Reasons**: {(requiresEscalation ? string.Join("; ", escalationReasons) : "None")}
            **Customer**: {ticket.Customer.FullName}
            """;
    }
}
