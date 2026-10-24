using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "RemotePro Device Agent";
});

builder.Services.AddHostedService<HeartbeatWorker>();

await builder.Build().RunAsync();

public sealed class HeartbeatWorker : BackgroundService
{
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly string _apiUrl;
    private readonly string _token;

    public HeartbeatWorker(ILogger<HeartbeatWorker> logger)
    {
        _logger = logger;

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RemotePro",
            "agent.json"
        );

        AgentConfig? config = null;

        if (File.Exists(configPath))
        {
            try
            {
                config = JsonSerializer.Deserialize<AgentConfig>(
                    File.ReadAllText(configPath)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to read agent configuration.");
            }
        }

        _apiUrl = (
            Environment.GetEnvironmentVariable("REMOTEPRO_API_URL")
            ?? config?.ApiUrl
            ?? "http://localhost:5150"
        ).TrimEnd('/');

        _token = Environment.GetEnvironmentVariable("REMOTEPRO_DEVICE_TOKEN")
            ?? config?.DeviceToken
            ?? string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            _logger.LogError("Device token is missing from secure configuration.");
            return;
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(_apiUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        _logger.LogInformation("RemotePro Agent started. API: {ApiUrl}", _apiUrl);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        do
        {
            try
            {
                using var response = await client.PostAsJsonAsync(
                    "/api/heartbeat",
                    new { token = _token },
                    stoppingToken
                );

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
                        cancellationToken: stoppingToken
                    );

                    _logger.LogInformation(
                        "Heartbeat successful. Status: {Status}; Device: {DeviceId}",
                        result?.Status,
                        result?.DeviceId
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "Heartbeat failed. HTTP status: {StatusCode}",
                        (int)response.StatusCode
                    );
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Heartbeat request failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private sealed record AgentConfig(string? ApiUrl, string? DeviceToken);

    private sealed record HeartbeatResponse(
        string? Status,
        Guid DeviceId,
        DateTime LastSeenAtUtc
    );
}
