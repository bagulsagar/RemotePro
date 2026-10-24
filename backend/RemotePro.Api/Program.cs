using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using RemotePro.Api.Data;
using RemotePro.Api.Services;
using RemotePro.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// SERVICES
// --------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, AdminApiKeyAuthenticationHandler>(
        AdminApiKeyDefaults.AuthenticationScheme, _ => { });
builder.Services.AddOpenApi();
builder.Services.AddHostedService<DeviceOfflineBackgroundService>();

// PostgreSQL database connection
builder.Services.AddDbContext<RemoteProDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("RemotePro");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Database connection string 'RemotePro' is not configured. " +
            "Check .NET User Secrets or appsettings configuration.");
    }

    options.UseNpgsql(connectionString);
});

// --------------------------------------------------
// BUILD APPLICATION
// --------------------------------------------------

var app = builder.Build();

// Log configuration status without exposing credentials
var connectionConfigured = !string.IsNullOrWhiteSpace(
    app.Configuration.GetConnectionString("RemotePro"));

app.Logger.LogInformation(
    "Remote Pro database connection string configured: {Configured}",
    connectionConfigured);

// --------------------------------------------------
// MIDDLEWARE
// --------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --------------------------------------------------
// API HEALTH CHECK
// --------------------------------------------------

app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        service = "Remote Pro API",
        status = "Healthy",
        version = "1.0.0",
        timestampUtc = DateTime.UtcNow
    });
});

// --------------------------------------------------
// POSTGRESQL DATABASE HEALTH CHECK
// --------------------------------------------------

app.MapGet("/api/health/database", async (
    RemoteProDbContext db,
    CancellationToken cancellationToken) =>
{
    try
    {
        // Execute a real SQL query against PostgreSQL.
        var result = await db.Database
            .SqlQueryRaw<int>("SELECT 1 AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (result != 1)
        {
            app.Logger.LogError(
                "PostgreSQL diagnostic query returned an unexpected result.");

            return Results.Json(
                new
                {
                    service = "Remote Pro API",
                    database = "PostgreSQL",
                    status = "UnexpectedResult"
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new
        {
            service = "Remote Pro API",
            database = "PostgreSQL",
            status = "Connected",
            queryTest = "Passed",
            timestampUtc = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        // Detailed diagnostic information stays in the API server logs.
        app.Logger.LogError(
            ex,
            "PostgreSQL diagnostic query failed. ExceptionType={ExceptionType}",
            ex.GetType().FullName);

        return Results.Json(
            new
            {
                service = "Remote Pro API",
                database = "PostgreSQL",
                status = "ConnectionFailed",
                message = "Check the API server terminal for diagnostic details."
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// --------------------------------------------------
// START APPLICATION
// --------------------------------------------------

app.Run();
