using eShopSupport.AgentService.Agents;
using eShopSupport.AgentService.Models;
using eShopSupport.Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace eShopSupport.AgentService.Api;

public static class ResearchApi
{
    public static void MapResearchApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/agent/ticket-research", ResearchTicketAsync)
            .WithName("ResearchTicket");

        app.MapPost("/api/agent/ticket-draft", GenerateDraftAsync)
            .WithName("GenerateDraft");

        app.MapPost("/api/agent/ticket-triage", TriageTicketAsync)
            .WithName("TriageTicket");
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

    private static async Task<IResult> GenerateDraftAsync(
        [FromBody] DraftGenerationRequest request,
        ResponseDraftAgent draftAgent,
        AppDbContext dbContext,
        ILogger<ResponseDraftAgent> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Received draft generation request for ticket {TicketId}", request.TicketId);

            // Get the research result - either from the request or from the database
            TicketResearchResult? research = request.ResearchResult;

            if (research == null)
            {
                // If no research provided, get the most recent research execution for this ticket
                var latestExecution = await dbContext.AgentExecutions
                    .Where(e => e.TicketId == request.TicketId && e.AgentName == "ResearchAgent" && e.Status == "Completed")
                    .OrderByDescending(e => e.CompletedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestExecution == null)
                {
                    return Results.BadRequest(new { Error = "No research found for this ticket. Please run research first." });
                }

                // For now, we'll return an error if research isn't provided
                // In a full implementation, we'd deserialize it from the execution record
                return Results.BadRequest(new { Error = "Research result must be provided in the request." });
            }

            // Create agent execution record
            var execution = new AgentExecution
            {
                AgentName = "ResponseDraftAgent",
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
                // Generate draft
                var result = await draftAgent.GenerateDraftAsync(research, request.TicketId, cancellationToken);

                // Update execution status
                execution.Status = "Completed";
                execution.CompletedAt = DateTime.UtcNow;

                execution.Traces.Add(new AgentTrace
                {
                    Timestamp = DateTime.UtcNow,
                    TraceType = "Completion",
                    Content = $"Draft generated successfully with confidence {result.Confidence:F2}",
                    SequenceNumber = 1
                });

                // Create draft response record
                var draftResponse = new DraftResponse
                {
                    TicketId = request.TicketId,
                    AgentExecutionId = executionId,
                    Content = result.DraftContent,
                    Confidence = result.Confidence,
                    Rationale = result.Rationale,
                    Status = "pending"
                };

                dbContext.DraftResponses.Add(draftResponse);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Draft generated successfully for ticket {TicketId}", request.TicketId);

                return Results.Ok(new DraftGenerationResponse
                {
                    Success = true,
                    ExecutionId = executionId,
                    DraftId = draftResponse.DraftResponseId,
                    Draft = result
                });
            }
            catch (Exception ex)
            {
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

                logger.LogError(ex, "Draft generation failed for ticket {TicketId}", request.TicketId);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process draft generation request for ticket {TicketId}", request.TicketId);
            return Results.Problem(
                title: "Draft Generation Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> TriageTicketAsync(
        [FromBody] TicketTriageRequest request,
        TriageAgent triageAgent,
        ResearchAgent researchAgent,
        ResponseDraftAgent draftAgent,
        AppDbContext dbContext,
        ILogger<TriageAgent> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting intelligent triage workflow for ticket {TicketId}", request.TicketId);

            // Step 1: Perform initial triage analysis
            logger.LogInformation("Step 1: Analyzing ticket {TicketId} with TriageAgent", request.TicketId);

            var triageExecution = new AgentExecution
            {
                AgentName = "TriageAgent",
                TicketId = request.TicketId,
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };

            dbContext.AgentExecutions.Add(triageExecution);
            await dbContext.SaveChangesAsync(cancellationToken);

            TicketTriageResult triageResult;
            try
            {
                triageResult = await triageAgent.TriageTicketAsync(request.TicketId, cancellationToken);

                triageExecution.Status = "Completed";
                triageExecution.CompletedAt = DateTime.UtcNow;
                triageExecution.Traces.Add(new AgentTrace
                {
                    Timestamp = DateTime.UtcNow,
                    TraceType = "Completion",
                    Content = $"Triage completed: Type={triageResult.TicketType}, Priority={triageResult.PriorityScore}, Urgency={triageResult.UrgencyLevel}, Agent={triageResult.RecommendedAgent}",
                    SequenceNumber = 1
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Triage completed for ticket {TicketId}: {Type}, Priority {Priority}, Urgency {Urgency}",
                    request.TicketId, triageResult.TicketType, triageResult.PriorityScore, triageResult.UrgencyLevel);
            }
            catch (Exception ex)
            {
                triageExecution.Status = "Failed";
                triageExecution.CompletedAt = DateTime.UtcNow;
                triageExecution.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogError(ex, "Triage failed for ticket {TicketId}", request.TicketId);
                throw;
            }

            // Step 2: Route to appropriate specialist agent based on triage
            // For now, we only have ResearchAgent, so all tickets go there
            // In the future, we'll route based on triageResult.RecommendedAgent
            logger.LogInformation("Step 2: Researching ticket {TicketId} (Recommended: {Agent})",
                request.TicketId, triageResult.RecommendedAgent);

            var researchExecution = new AgentExecution
            {
                AgentName = "ResearchAgent",
                TicketId = request.TicketId,
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };

            dbContext.AgentExecutions.Add(researchExecution);
            await dbContext.SaveChangesAsync(cancellationToken);

            TicketResearchResult researchResult;
            try
            {
                researchResult = await researchAgent.ResearchTicketAsync(request.TicketId, cancellationToken);

                researchExecution.Status = "Completed";
                researchExecution.CompletedAt = DateTime.UtcNow;
                researchExecution.Traces.Add(new AgentTrace
                {
                    Timestamp = DateTime.UtcNow,
                    TraceType = "Completion",
                    Content = $"Research completed. Tools used: {string.Join(", ", researchResult.ToolCallsMade)}",
                    SequenceNumber = 1
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Research completed for ticket {TicketId}", request.TicketId);
            }
            catch (Exception ex)
            {
                researchExecution.Status = "Failed";
                researchExecution.CompletedAt = DateTime.UtcNow;
                researchExecution.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogError(ex, "Research failed during triage workflow for ticket {TicketId}", request.TicketId);
                throw;
            }

            // Step 3: Generate draft response based on research
            logger.LogInformation("Step 3: Generating draft for ticket {TicketId}", request.TicketId);

            var draftExecution = new AgentExecution
            {
                AgentName = "ResponseDraftAgent",
                TicketId = request.TicketId,
                StartedAt = DateTime.UtcNow,
                Status = "Running"
            };

            dbContext.AgentExecutions.Add(draftExecution);
            await dbContext.SaveChangesAsync(cancellationToken);

            Agents.DraftResponseResult draftResult;
            int draftId;
            try
            {
                draftResult = await draftAgent.GenerateDraftAsync(researchResult, request.TicketId, cancellationToken);

                draftExecution.Status = "Completed";
                draftExecution.CompletedAt = DateTime.UtcNow;
                draftExecution.Traces.Add(new AgentTrace
                {
                    Timestamp = DateTime.UtcNow,
                    TraceType = "Completion",
                    Content = $"Draft generated with confidence {draftResult.Confidence:F2}",
                    SequenceNumber = 1
                });

                // Save draft response
                var draftResponse = new DraftResponse
                {
                    TicketId = request.TicketId,
                    AgentExecutionId = draftExecution.AgentExecutionId,
                    Content = draftResult.DraftContent,
                    Confidence = draftResult.Confidence,
                    Rationale = draftResult.Rationale,
                    Status = "pending"
                };

                dbContext.DraftResponses.Add(draftResponse);
                await dbContext.SaveChangesAsync(cancellationToken);
                draftId = draftResponse.DraftResponseId;

                logger.LogInformation("Draft generated for ticket {TicketId}", request.TicketId);
            }
            catch (Exception ex)
            {
                draftExecution.Status = "Failed";
                draftExecution.CompletedAt = DateTime.UtcNow;
                draftExecution.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogError(ex, "Draft generation failed during triage workflow for ticket {TicketId}", request.TicketId);

                // Return partial success - triage and research worked, draft failed
                return Results.Ok(new TicketTriageResponse
                {
                    Success = false,
                    TriageExecutionId = triageExecution.AgentExecutionId,
                    ResearchExecutionId = researchExecution.AgentExecutionId,
                    DraftExecutionId = draftExecution.AgentExecutionId,
                    Triage = triageResult,
                    Research = researchResult,
                    Draft = null,
                    ErrorMessage = $"Draft generation failed: {ex.Message}"
                });
            }

            logger.LogInformation("Triage workflow completed successfully for ticket {TicketId}", request.TicketId);

            return Results.Ok(new TicketTriageResponse
            {
                Success = true,
                TriageExecutionId = triageExecution.AgentExecutionId,
                ResearchExecutionId = researchExecution.AgentExecutionId,
                DraftExecutionId = draftExecution.AgentExecutionId,
                DraftId = draftId,
                Triage = triageResult,
                Research = researchResult,
                Draft = draftResult
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Triage workflow failed for ticket {TicketId}", request.TicketId);
            return Results.Problem(
                title: "Triage Failed",
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

public class DraftGenerationRequest
{
    public int TicketId { get; set; }
    public TicketResearchResult? ResearchResult { get; set; }
}

public class DraftGenerationResponse
{
    public bool Success { get; set; }
    public int ExecutionId { get; set; }
    public int DraftId { get; set; }
    public Agents.DraftResponseResult? Draft { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TicketTriageRequest
{
    public int TicketId { get; set; }
}

public class TicketTriageResponse
{
    public bool Success { get; set; }
    public int TriageExecutionId { get; set; }
    public int ResearchExecutionId { get; set; }
    public int DraftExecutionId { get; set; }
    public int DraftId { get; set; }
    public TicketTriageResult? Triage { get; set; }
    public TicketResearchResult? Research { get; set; }
    public Agents.DraftResponseResult? Draft { get; set; }
    public string? ErrorMessage { get; set; }
}
