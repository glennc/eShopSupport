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

        app.MapPost("/api/ticket/{ticketId}/triage", TriggerTriageAsync)
            .WithName("TriggerTicketTriage");

        app.MapPost("/api/draft/{draftId}/reject", RejectDraftAsync)
            .WithName("RejectDraft");

        app.MapPost("/api/ticket/{ticketId}/draft/regenerate", RegenerateDraftAsync)
            .WithName("RegenerateDraft");
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

    private static async Task<IResult> TriggerTriageAsync(
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

            // Check if triage already exists for this ticket (check for both research and draft)
            var existingResearch = ticket.Messages
                .FirstOrDefault(m => m.MessageType == MessageType.AgentResearch);
            var existingDraft = await dbContext.DraftResponses
                .FirstOrDefaultAsync(d => d.TicketId == ticketId && d.Status == "pending", cancellationToken);

            if (existingResearch != null && existingDraft != null)
            {
                logger.LogInformation("Triage already exists for ticket {TicketId}, skipping", ticketId);
                return Results.Ok(new
                {
                    alreadyExists = true,
                    researchMessageId = existingResearch.MessageId,
                    draftId = existingDraft.DraftResponseId
                });
            }

            logger.LogInformation("Triggering triage workflow for ticket {TicketId}", ticketId);

            // Call AgentService to perform triage (research + draft)
            var triageResponse = await agentServiceClient.RequestTicketTriageAsync(ticketId, cancellationToken);

            if (triageResponse == null || !triageResponse.Success)
            {
                logger.LogError("Failed to get triage result for ticket {TicketId}: {Error}",
                    ticketId, triageResponse?.ErrorMessage ?? "Unknown error");
                return Results.Problem("Failed to complete triage", statusCode: 500);
            }

            // Save research result as an AgentResearch message
            Message? researchMessage = null;
            if (triageResponse.Research != null && existingResearch == null)
            {
                researchMessage = new Message
                {
                    TicketId = ticketId,
                    MessageType = MessageType.AgentResearch,
                    CreatedAt = DateTime.UtcNow,
                    Text = triageResponse.Research.ToMarkdown(),
                    AgentExecutionId = triageResponse.ResearchExecutionId
                };

                dbContext.Messages.Add(researchMessage);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Research completed and saved for ticket {TicketId} as message {MessageId}",
                    ticketId, researchMessage.MessageId);
            }

            logger.LogInformation("Triage completed for ticket {TicketId}. Research: {ResearchId}, Draft: {DraftId}",
                ticketId, researchMessage?.MessageId, triageResponse.DraftId);

            return Results.Ok(new
            {
                success = true,
                researchMessageId = researchMessage?.MessageId ?? existingResearch?.MessageId,
                draftId = triageResponse.DraftId,
                draft = triageResponse.Draft,
                researchExecutionId = triageResponse.ResearchExecutionId,
                draftExecutionId = triageResponse.DraftExecutionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error triggering triage for ticket {TicketId}", ticketId);
            return Results.Problem(
                title: "Triage Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> RejectDraftAsync(
        int draftId,
        AppDbContext dbContext,
        ILogger<AgentServiceClient> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the optional rejection reason from request body
            string? rejectionReason = null;
            if (httpContext.Request.ContentLength > 0)
            {
                var body = await httpContext.Request.ReadFromJsonAsync<RejectDraftRequest>(cancellationToken);
                rejectionReason = body?.Reason;
            }

            var draft = await dbContext.DraftResponses
                .FirstOrDefaultAsync(d => d.DraftResponseId == draftId, cancellationToken);

            if (draft == null)
            {
                return Results.NotFound($"Draft {draftId} not found");
            }

            draft.Status = "rejected";
            draft.ReviewedAt = DateTime.UtcNow;
            draft.RejectionReason = rejectionReason;

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Draft {DraftId} rejected. Reason: {Reason}", draftId, rejectionReason ?? "No reason provided");

            return Results.Ok(new { success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rejecting draft {DraftId}", draftId);
            return Results.Problem(
                title: "Reject Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> RegenerateDraftAsync(
        int ticketId,
        AppDbContext dbContext,
        AgentServiceClient agentServiceClient,
        ILogger<AgentServiceClient> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Regenerating draft for ticket {TicketId}", ticketId);

            // Get the most recent research for this ticket
            var researchMessage = await dbContext.Messages
                .Where(m => m.TicketId == ticketId && m.MessageType == MessageType.AgentResearch)
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (researchMessage == null)
            {
                return Results.BadRequest(new { error = "No research found for this ticket. Please run triage first." });
            }

            // For now, we need to re-run triage to get a new draft
            // In a full implementation, we'd deserialize the research from the message and just call the draft agent
            var triageResponse = await agentServiceClient.RequestTicketTriageAsync(ticketId, cancellationToken);

            if (triageResponse == null || !triageResponse.Success || triageResponse.Draft == null)
            {
                return Results.Problem("Failed to regenerate draft", statusCode: 500);
            }

            logger.LogInformation("Draft regenerated for ticket {TicketId}. New draft ID: {DraftId}",
                ticketId, triageResponse.DraftId);

            return Results.Ok(new
            {
                success = true,
                draftId = triageResponse.DraftId,
                draft = triageResponse.Draft
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error regenerating draft for ticket {TicketId}", ticketId);
            return Results.Problem(
                title: "Regenerate Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }
}

public class RejectDraftRequest
{
    public string? Reason { get; set; }
}
