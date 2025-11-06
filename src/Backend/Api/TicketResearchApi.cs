using eShopSupport.Backend.Clients;
using eShopSupport.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace eShopSupport.Backend.Api;

public static class TicketResearchApi
{
    public static void MapTicketResearchApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ticket/{ticketId}/research", TriggerResearchAsync)
            .WithName("TriggerTicketResearch");
    }

    private static async Task<IResult> TriggerResearchAsync(
        int ticketId,
        AppDbContext dbContext,
        AgentServiceClient agentServiceClient,
        ILogger<AgentServiceClient> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify ticket exists
            var ticket = await dbContext.Tickets
                .Include(t => t.Messages)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId, cancellationToken);

            if (ticket == null)
            {
                return Results.NotFound($"Ticket {ticketId} not found");
            }

            // Check if research already exists for this ticket
            var existingResearch = ticket.Messages
                .FirstOrDefault(m => m.MessageType == MessageType.AgentResearch);

            if (existingResearch != null)
            {
                logger.LogInformation("Research already exists for ticket {TicketId}, skipping", ticketId);
                return Results.Ok(new { alreadyExists = true, messageId = existingResearch.MessageId });
            }

            logger.LogInformation("Triggering research for ticket {TicketId}", ticketId);

            // Call AgentService to perform research
            var researchResponse = await agentServiceClient.RequestTicketResearchAsync(ticketId, cancellationToken);

            if (researchResponse == null || !researchResponse.Success || researchResponse.Result == null)
            {
                logger.LogError("Failed to get research result for ticket {TicketId}", ticketId);
                return Results.Problem("Failed to complete research", statusCode: 500);
            }

            // Save research result as an AgentResearch message
            var researchMessage = new Message
            {
                TicketId = ticketId,
                MessageType = MessageType.AgentResearch,
                CreatedAt = DateTime.UtcNow,
                Text = researchResponse.Result.ToMarkdown(),
                AgentExecutionId = researchResponse.ExecutionId
            };

            dbContext.Messages.Add(researchMessage);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Research completed and saved for ticket {TicketId} as message {MessageId}",
                ticketId, researchMessage.MessageId);

            return Results.Ok(new
            {
                success = true,
                messageId = researchMessage.MessageId,
                executionId = researchResponse.ExecutionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error triggering research for ticket {TicketId}", ticketId);
            return Results.Problem(
                title: "Research Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }
}
