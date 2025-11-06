using eShopSupport.AgentService.Agents;
using eShopSupport.AgentService.Models;
using eShopSupport.Backend.Data;
using Microsoft.AspNetCore.Mvc;

namespace eShopSupport.AgentService.Api;

public static class ResearchApi
{
    public static void MapResearchApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/agent/ticket-research", ResearchTicketAsync)
            .WithName("ResearchTicket");
    }

    private static async Task<IResult> ResearchTicketAsync(
        [FromBody] TicketResearchRequest request,
        ResearchAgent researchAgent,
        AppDbContext dbContext,
        ILogger<ResearchAgent> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Received research request for ticket {TicketId}", request.TicketId);

            // Create agent execution record
            var execution = new AgentExecution
            {
                AgentName = "ResearchAgent",
                TicketId = request.TicketId,
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };

            dbContext.AgentExecutions.Add(execution);
            await dbContext.SaveChangesAsync(cancellationToken);

            var executionId = execution.AgentExecutionId;
            logger.LogInformation("Created agent execution {ExecutionId}", executionId);

            try
            {
                // Execute research
                var result = await researchAgent.ResearchTicketAsync(request.TicketId, cancellationToken);

                // Update execution status
                execution.Status = "Completed";
                execution.CompletedAt = DateTime.UtcNow;

                // Add trace for completion
                execution.Traces.Add(new AgentTrace
                {
                    Timestamp = DateTime.UtcNow,
                    TraceType = "Completion",
                    Content = $"Research completed successfully. Tools used: {string.Join(", ", result.ToolCallsMade)}",
                    SequenceNumber = 1
                });

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Research completed successfully for ticket {TicketId}", request.TicketId);

                // Return the result with execution ID
                return Results.Ok(new TicketResearchResponse
                {
                    Success = true,
                    ExecutionId = executionId,
                    Result = result
                });
            }
            catch (Exception ex)
            {
                // Update execution status to failed
                execution.Status = "Failed";
                execution.CompletedAt = DateTime.UtcNow;
                execution.ErrorMessage = ex.Message;

                execution.Traces.Add(new AgentTrace
                {
                    Timestamp = DateTime.UtcNow,
                    TraceType = "Error",
                    Content = $"Error: {ex.Message}",
                    SequenceNumber = 1
                });

                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogError(ex, "Research failed for ticket {TicketId}", request.TicketId);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process research request for ticket {TicketId}", request.TicketId);
            return Results.Problem(
                title: "Research Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }
}

public class TicketResearchRequest
{
    public int TicketId { get; set; }
}

public class TicketResearchResponse
{
    public bool Success { get; set; }
    public int ExecutionId { get; set; }
    public TicketResearchResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
}
