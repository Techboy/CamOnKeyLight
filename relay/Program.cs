using System.Text;
using CamOnKeyLight.Core;
using CamOnKeyLight.Relay;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 4096;
});

LightCatalog catalog = LightCatalog.Parse(builder.Configuration["LIGHTS"]);
bool discover = string.Equals(builder.Configuration["DISCOVER"], "true", StringComparison.OrdinalIgnoreCase);
builder.Services.AddSingleton(catalog);
builder.Services.AddSingleton(new RelayAuth(builder.Configuration["RELAY_SHARED_SECRET"]));
builder.Services.AddHttpClient<LightForwarder>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    ConnectTimeout = TimeSpan.FromSeconds(2)
});
if (discover)
    builder.Services.AddHostedService<DiscoveryHostedService>();

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapGet("/v1/ping", (RelayAuth auth, HttpRequest request, LightCatalog lights) =>
{
    if (!auth.Allows(request))
        return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

    return Results.Json(new { ok = true, count = lights.Count });
});

app.MapGet("/v1/lights", async (string address, RelayAuth auth, HttpRequest request, LightForwarder forwarder, CancellationToken cancellationToken) =>
{
    if (!auth.Allows(request))
        return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

    ForwardResult result = await forwarder.GetAsync(address, cancellationToken);
    return ToResult(result);
});

app.MapPut("/v1/lights", async (string address, RelayAuth auth, HttpRequest request, LightForwarder forwarder, CancellationToken cancellationToken) =>
{
    if (!auth.Allows(request))
        return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

    string? body = await ReadBody(request, cancellationToken);
    if (body == null)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    ForwardResult result = await forwarder.PutAsync(address, body, cancellationToken);
    return ToResult(result);
});

app.MapPost("/v1/lights/on", (string address, RelayAuth auth, HttpRequest request, LightForwarder forwarder, CancellationToken cancellationToken) =>
    Switch(address, true, auth, request, forwarder, cancellationToken));

app.MapPost("/v1/lights/off", (string address, RelayAuth auth, HttpRequest request, LightForwarder forwarder, CancellationToken cancellationToken) =>
    Switch(address, false, auth, request, forwarder, cancellationToken));

app.Run();

static async Task<IResult> Switch(string address, bool turnOn, RelayAuth auth, HttpRequest request, LightForwarder forwarder, CancellationToken cancellationToken)
{
    if (!auth.Allows(request))
        return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

    ForwardResult result = await forwarder.PutAsync(address, LightEndpoint.PutBody(turnOn), cancellationToken);
    return ToResult(result);
}

static async Task<string?> ReadBody(HttpRequest request, CancellationToken cancellationToken)
{
    if (request.ContentLength is > 4096)
        return null;

    using StreamReader reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
    char[] buffer = new char[4097];
    int read = await reader.ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
    if (read > 4096)
        return null;

    return new string(buffer, 0, read);
}

static IResult ToResult(ForwardResult result)
{
    if (result.StatusCode == 0)
        return Results.Json(new { error = "light unreachable" }, statusCode: StatusCodes.Status502BadGateway);

    return Results.Text(result.Body, "application/json", statusCode: result.StatusCode);
}

public partial class Program
{
}
