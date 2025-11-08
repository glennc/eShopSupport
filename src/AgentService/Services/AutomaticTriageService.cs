using eShopSupport.AgentService.Agents;
using eShopSupport.AgentService.Models;
using eShopSupport.Backend.Data;
using Microsoft.EntityFrameworkCore;
using TicketStatus = eShopSupport.ServiceDefaults.Clients.Backend.TicketStatus;
using MessageType = eShopSupport.Backend.Data.MessageType;

namespace eShopSupport.AgentService.Services;

public class AutomaticTriageService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutomaticTriageService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public AutomaticTriageService(
        IServiceProvider serviceProvider,
        ILogger<AutomaticTriageService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automatic Triage Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUntriagedTicketsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing untriaged tickets");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Automatic Triage Service stopped");
    }

    private async Task ProcessUntriagedTicketsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check if automatic triage is enabled
        var settings = await dbContext.TriageSettings.FirstOrDefaultAsync(cancellationToken);

        if (settings == null || !settings.AutomaticTriageEnabled)
        {
            // Auto-triage is disabled, skip processing
            return;
        }

        _logger.LogDebug("Checking for untriaged tickets");

        // Find tickets that don't have triage analysis yet and are still open
        var untriagedTickets = await dbContext.Tickets
            .Where(t => t.TicketStatus == TicketStatus.Open)
            .Where(t => !dbContext.TriageAnalyses.Any(ta => ta.TicketId == t.TicketId))
            .OrderByDescending(t => t.TicketId)
            .Take(5) // Process up to 5 tickets at a time
            .Select(t => t.TicketId)
            .ToListAsync(cancellationToken);

        if (untriagedTickets.Any())
        {
            _logger.LogInformation("Found {Count} untriaged tickets to process", untriagedTickets.Count);

            foreach (var ticketId in untriagedTickets)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await TriageTicketAsync(ticketId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to triage ticket {TicketId}", ticketId);
                }
            }
        }
    }

    private async Task TriageTicketAsync(int ticketId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting automatic triage workflow for ticket {TicketId}", ticketId);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var triageAgent = scope.ServiceProvider.GetRequiredService<TriageAgent>();
        var researchAgent = scope.ServiceProvider.GetRequiredService<ResearchAgent>();
        var draftAgent = scope.ServiceProvider.GetRequiredService<ResponseDraftAgent>();

        // Step 1: Perform initial triage analysis
        _logger.LogInformation("Step 1: Analyzing ticket {TicketId} with TriageAgent", ticketId);

        var triageExecution = new AgentExecution
        {
            AgentName = "TriageAgent",
            TicketId = ticketId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        dbContext.AgentExecutions.Add(triageExecution);
        await dbContext.SaveChangesAsync(cancellationToken);

        TicketTriageResult triageResult;
        try
        {
            triageResult = await triageAgent.TriageTicketAsync(ticketId, cancellationToken);

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
            _logger.LogInformation("Triage completed for ticket {TicketId}: {Type}, Priority {Priority}, Urgency {Urgency}",
                ticketId, triageResult.TicketType, triageResult.PriorityScore, triageResult.UrgencyLevel);
        }
        catch (Exception ex)
        {
            triageExecution.Status = "Failed";
            triageExecution.CompletedAt = DateTime.UtcNow;
            triageExecution.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Triage failed for ticket {TicketId}", ticketId);
            throw;
        }

        // Save triage analysis to database
        var triageAnalysis = new TriageAnalysis
        {
            TicketId = ticketId,
            TicketType = triageResult.TicketType,
            PriorityScore = triageResult.PriorityScore,
            UrgencyLevel = triageResult.UrgencyLevel,
            RecommendedAgent = triageResult.RecommendedAgent,
            RequiresEscalation = triageResult.RequiresImmediateEscalation,
            AgentExecutionId = triageExecution.AgentExecutionId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.TriageAnalyses.Add(triageAnalysis);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Triage analysis saved for ticket {TicketId}", ticketId);

        // Step 2: Route to appropriate specialist agent based on triage
        _logger.LogInformation("Step 2: Researching ticket {TicketId} (Recommended: {Agent})",
            ticketId, triageResult.RecommendedAgent);

        var researchExecution = new AgentExecution
        {
            AgentName = "ResearchAgent",
            TicketId = ticketId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        dbContext.AgentExecutions.Add(researchExecution);
        await dbContext.SaveChangesAsync(cancellationToken);

        TicketResearchResult researchResult;
        try
        {
            researchResult = await researchAgent.ResearchTicketAsync(ticketId, cancellationToken);

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
            _logger.LogInformation("Research completed for ticket {TicketId}", ticketId);
        }
        catch (Exception ex)
        {
            researchExecution.Status = "Failed";
            researchExecution.CompletedAt = DateTime.UtcNow;
            researchExecution.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Research failed during triage workflow for ticket {TicketId}", ticketId);
            throw;
        }

        // Save research result as an AgentResearch message
        var researchMessage = new Message
        {
            TicketId = ticketId,
            MessageType = MessageType.AgentResearch,
            CreatedAt = DateTime.UtcNow,
            Text = researchResult.ToMarkdown(),
            AgentExecutionId = researchExecution.AgentExecutionId
        };

        dbContext.Messages.Add(researchMessage);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Step 3: Generate draft response based on research
        _logger.LogInformation("Step 3: Generating draft for ticket {TicketId}", ticketId);

        var draftExecution = new AgentExecution
        {
            AgentName = "ResponseDraftAgent",
            TicketId = ticketId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        dbContext.AgentExecutions.Add(draftExecution);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var draftResult = await draftAgent.GenerateDraftAsync(researchResult, ticketId, cancellationToken);

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
                TicketId = ticketId,
                AgentExecutionId = draftExecution.AgentExecutionId,
                Content = draftResult.DraftContent,
                Confidence = draftResult.Confidence,
                ConfidenceFactors = System.Text.Json.JsonSerializer.Serialize(draftResult.ConfidenceFactors),
                Rationale = draftResult.Rationale,
                Status = "pending"
            };

            dbContext.DraftResponses.Add(draftResponse);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Draft generated for ticket {TicketId} with ID {DraftId}", ticketId, draftResponse.DraftResponseId);
        }
        catch (Exception ex)
        {
            draftExecution.Status = "Failed";
            draftExecution.CompletedAt = DateTime.UtcNow;
            draftExecution.ErrorMessage = ex.Message;
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Draft generation failed during triage workflow for ticket {TicketId}", ticketId);
            // Don't throw - triage and research succeeded, just draft failed
        }

        _logger.LogInformation("Automatic triage workflow completed successfully for ticket {TicketId}", ticketId);
    }
}
