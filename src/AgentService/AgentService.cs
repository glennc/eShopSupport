using eShopSupport.AgentService.Agents;
using eShopSupport.AgentService.Api;
using eShopSupport.Backend.Data;
using eShopSupport.ServiceDefaults;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

builder.AddKeyedAzureOpenAIClient("eShopSupportModel")
        .AddKeyedChatClient("eShopSupportModel")
        .UseFunctionInvocation()
        .UseCachingForTest()
        .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true);

builder.AddKeyedAzureOpenAIClient("eShopSupportMini")
    .AddKeyedChatClient("eShopSupportMini","eShopSupportMini")
    .UseFunctionInvocation()
    .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true);
    
// Add database
builder.AddNpgsqlDbContext<AppDbContext>("backenddb");

// Register services
builder.Services.AddScoped<eShopSupport.AgentService.Services.DraftConfidenceCalculator>();

// Register background services
builder.Services.AddHostedService<eShopSupport.AgentService.Services.AutomaticTriageService>();

// Register agents
builder.Services.AddScoped<TriageAgent>();
builder.Services.AddScoped<ResearchAgent>();
builder.Services.AddScoped<ResponseDraftAgent>();

// Add services to the container.
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.MapDefaultEndpoints();

// Map API endpoints
app.MapResearchApiEndpoints();

app.Run();
