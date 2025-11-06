using eShopSupport.AgentService.Agents;
using eShopSupport.AgentService.Api;
using eShopSupport.Backend.Data;
using eShopSupport.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add chat completion service (Azure OpenAI)
builder.AddChatCompletionService("eShopSupport");

// Add database
builder.AddNpgsqlDbContext<AppDbContext>("backenddb");

// Register agents
builder.Services.AddScoped<ResearchAgent>();

// Add services to the container.
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.MapDefaultEndpoints();

// Map API endpoints
app.MapResearchApiEndpoints();

app.Run();
