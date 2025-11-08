using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.Sources.Add(new JsonConfigurationSource { Path = "appsettings.Local.json", Optional = true });

var isE2ETest = builder.Configuration["E2E_TEST"] == "true";

var dbPassword = builder.AddParameter("PostgresPassword", secret: true);

var postgresServer = builder
    .AddPostgres("eshopsupport-postgres", password: dbPassword);
var backendDb = postgresServer
    .AddDatabase("backenddb");

var vectorDb = builder
    .AddQdrant("vector-db");

var identityServer = builder.AddProject<IdentityServer>("identity-server")
    .WithExternalHttpEndpoints();

var identityEndpoint = identityServer
    .GetEndpoint("https");

// Azure AI Foundry setup
var foundryName = builder.AddParameter("foundryName");
var resourceGroup = builder.AddParameter("resourceGroup");

var foundry = builder.AddAzureAIFoundry("foundry")
    .AsExisting(foundryName, resourceGroup);

var eShopSupportModel = foundry.AddDeployment("eShopSupportModel", AIFoundryModel.OpenAI.Gpt41);
var eShopSupportMini = foundry.AddDeployment("eShopSupportMini", AIFoundryModel.Microsoft.Phi4MiniInstruct);

var storage = builder.AddAzureStorage("eshopsupport-storage");
if (builder.Environment.IsDevelopment())
{
    storage.RunAsEmulator(r =>
    {
        if (!isE2ETest)
        {
            r.WithDataVolume();
        }
    });
}

var blobStorage = storage.AddBlobs("eshopsupport-blobs");

var pythonInference = builder.AddPythonUvicornApp("python-inference",
    Path.Combine("..", "PythonInference"), port: 62394);

var redis = builder.AddRedis("redis");

var agentService = builder.AddProject<AgentService>("agentservice")
    .WithReference(backendDb)
    .WithReference(eShopSupportModel)
    .WithReference(vectorDb)
    .WithReference(eShopSupportMini)
    .WithReference(redis);

var backend = builder.AddProject<Backend>("backend")
    .WithReference(backendDb)
    .WithReference(eShopSupportModel)
    .WithReference(blobStorage)
    .WithReference(vectorDb)
    .WithReference(pythonInference)
    .WithReference(agentService)
    .WithReference(redis)
    .WithEnvironment("IdentityUrl", identityEndpoint)
    .WithEnvironment("ImportInitialDataDir", Path.Combine(builder.AppHostDirectory, "..", "..", "seeddata", isE2ETest ? "test" : "dev"));

var staffWebUi = builder.AddProject<StaffWebUI>("staffwebui")
    .WithExternalHttpEndpoints()
    .WithReference(backend)
    .WithReference(redis)
    .WithEnvironment("IdentityUrl", identityEndpoint);

var customerWebUi = builder.AddProject<CustomerWebUI>("customerwebui")
    .WithReference(backend)
    .WithEnvironment("IdentityUrl", identityEndpoint);

// Circular references: IdentityServer needs to know the endpoints of the web UIs
identityServer
    .WithEnvironment("CustomerWebUIEndpoint", customerWebUi.GetEndpoint("https"))
    .WithEnvironment("StaffWebUIEndpoint", staffWebUi.GetEndpoint("https"));

if (!isE2ETest)
{
    postgresServer.WithDataVolume();
    vectorDb.WithVolume("eshopsupport-vector-db-storage", "/qdrant/storage");
}

builder.Build().Run();
