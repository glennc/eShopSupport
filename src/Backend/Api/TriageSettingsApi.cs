using eShopSupport.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace eShopSupport.Backend.Api;

public static class TriageSettingsApi
{
    public static void MapTriageSettingsApiEndpoints(this WebApplication app)
    {
        app.MapGet("/api/settings/triage", GetTriageSettingsAsync)
            .WithName("GetTriageSettings");

        app.MapPost("/api/settings/triage", UpdateTriageSettingsAsync)
            .WithName("UpdateTriageSettings");
    }

    private static async Task<IResult> GetTriageSettingsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get or create settings (there should only ever be one row)
            var settings = await dbContext.TriageSettings.FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
            {
                settings = new TriageSettings
                {
                    AutomaticTriageEnabled = false,
                    LastModified = DateTime.UtcNow
                };
                dbContext.TriageSettings.Add(settings);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to get triage settings",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> UpdateTriageSettingsAsync(
        UpdateTriageSettingsRequest request,
        AppDbContext dbContext,
        ILogger<UpdateTriageSettingsRequest> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get or create settings (there should only ever be one row)
            var settings = await dbContext.TriageSettings.FirstOrDefaultAsync(cancellationToken);

            if (settings == null)
            {
                settings = new TriageSettings
                {
                    AutomaticTriageEnabled = request.AutomaticTriageEnabled,
                    LastModified = DateTime.UtcNow
                };
                dbContext.TriageSettings.Add(settings);
            }
            else
            {
                settings.AutomaticTriageEnabled = request.AutomaticTriageEnabled;
                settings.LastModified = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Automatic triage setting updated to: {Enabled}", request.AutomaticTriageEnabled);

            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating triage settings");
            return Results.Problem(
                title: "Failed to update triage settings",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }
}

public class UpdateTriageSettingsRequest
{
    public bool AutomaticTriageEnabled { get; set; }
}
