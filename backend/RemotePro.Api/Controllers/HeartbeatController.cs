using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemotePro.Api.Data;

namespace RemotePro.Api.Controllers;

[ApiController]
[Route("api/heartbeat")]
public sealed class HeartbeatController : ControllerBase
{
    private readonly RemoteProDbContext _db;

    public HeartbeatController(RemoteProDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] HeartbeatRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Token))
        {
            return Unauthorized(new
            {
                message = "A valid device token is required."
            });
        }

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(request.Token)));

        var credential = await _db.DeviceCredentials
            .Include(item => item.Device)
            .FirstOrDefaultAsync(
                item => item.TokenHash == tokenHash &&
                        item.RevokedAtUtc == null,
                cancellationToken);

        if (credential is null)
        {
            return Unauthorized(new
            {
                message = "Invalid or revoked device token."
            });
        }

        var now = DateTime.UtcNow;

        credential.Device.LastSeenAtUtc = now;
        credential.Device.Status = "Online";

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            status = "Online",
            deviceId = credential.DeviceId,
            lastSeenAtUtc = now
        });
    }
}

public sealed record HeartbeatRequest(string? Token);