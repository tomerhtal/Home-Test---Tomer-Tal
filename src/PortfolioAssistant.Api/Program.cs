using System.Text.Json.Serialization;
using PortfolioAssistant.Api;
using PortfolioAssistant.Core;
using PortfolioAssistant.Core.Audit;
using PortfolioAssistant.Core.Llm;
#if BEDROCK
using Amazon;
using Amazon.BedrockRuntime;
using PortfolioAssistant.Bedrock;
#endif

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// בחירת המודל מההגדרות: Mock (ברירת מחדל, בלי AWS) או Bedrock.
var provider = config["Llm:Provider"] ?? "Mock";
ILlmClient primary;
ILlmClient? fallback;

if (provider.Equals("Bedrock", StringComparison.OrdinalIgnoreCase))
{
#if BEDROCK
    var bedrock = new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(config["Llm:Region"] ?? "us-east-1"));
    primary = new BedrockLlmClient(bedrock, config["Llm:PrimaryModelId"] ?? throw new InvalidOperationException("Llm:PrimaryModelId is missing"));
    var fallbackId = config["Llm:FallbackModelId"];
    fallback = string.IsNullOrWhiteSpace(fallbackId) || fallbackId.StartsWith('<') ? null : new BedrockLlmClient(bedrock, fallbackId);
#else
    throw new InvalidOperationException("This build excludes Bedrock. Rebuild without -p:IncludeBedrock=false.");
#endif
}
else
{
    primary = new MockLlmClient();
    fallback = null;
}

var maxPriceAge = config.GetValue<double?>("Prices:MaxAgeMinutes") is { } minutes ? TimeSpan.FromMinutes(minutes) : (TimeSpan?)null;
var assistant = AssistantBuilder.Build(new AssistantOptions(
    AssistantBuilder.FindDataFolder(),
    primary,
    fallback,
    new FileAuditLogger(Path.Combine(builder.Environment.ContentRootPath, "logs")),
    maxPriceAge));

builder.Services.AddSingleton(assistant);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapAskEndpoint();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", model = primary.Name }));

app.Logger.LogInformation("Portfolio assistant started with model provider: {Provider}", provider);
app.Run();
