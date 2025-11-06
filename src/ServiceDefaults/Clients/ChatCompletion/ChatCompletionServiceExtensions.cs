using Microsoft.Extensions.AI;

namespace Microsoft.Extensions.Hosting;

public static class ChatCompletionServiceExtensions
{
    public static void AddChatCompletionService(this IHostApplicationBuilder builder, string serviceName)
    {
        ChatClientBuilder chatClientBuilder = builder
            .AddAzureOpenAIClient(serviceName)
            .AddChatClient(serviceName);

        chatClientBuilder
            .UseFunctionInvocation()
            .UseCachingForTest()
            .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true);
    }
}
