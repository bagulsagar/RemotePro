using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RemotePro.Api.Data;
using RemotePro.Api.Models;
using RemotePro.Api.Security;

namespace RemotePro.Api.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize(AuthenticationSchemes = AdminApiKeyDefaults.AuthenticationScheme)]
public sealed class DevicesController : ControllerBase
{
    private readonly RemoteProDbContext _db;

    private static readonly string[] AllowedStatuses =
    [
        "Online",
        "Offline"
    ];

    public DevicesController(RemoteProDbContext db)
    {
        _db = db;
    }

    // GET /api/devices
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Device>>> GetAll(
        CancellationToken cancellationToken)
    {
        var devices = await _db.Devices
            .AsNoTracking()
            .OrderByDescending(device => device.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(devices);
    }

    // GET /api/devices/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Device>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var device = await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                device => device.Id == id,
                cancellationToken);

        if (device is null)
        {
            return NotFound(new
            {
                message = "Device not found."
            });
        }

        return Ok(device);
    }

    // POST /api/devices
    [HttpPost]
    public async Task<ActionResult<Device>> Create(
        [FromBody] CreateDeviceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new
            {
                message = "A device request body is required."
            });
        }

        var deviceName = Normalize(request.DeviceName);
        var hostname = Normalize(request.Hostname);
        var operatingSystem = Normalize(request.OperatingSystem);
        var ipAddress = Normalize(request.IpAddress);

        var validationError = ValidateDeviceFields(
            deviceName,
            hostname,
            operatingSystem,
            ipAddress);

        if (validationError is not null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        // Case-insensitive hostname check.
        var normalizedHostname = NormalizeHostname(hostname);

        var hostnameExists = await _db.Devices.AnyAsync(
            device => device.Hostname.Trim().ToLower() == normalizedHostname,
            cancellationToken);

        if (hostnameExists)
        {
            return Conflict(new
            {
                message = "A device with this hostname already exists.",
                hostname
            });
        }

        var device = new Device
        {
            DeviceName = deviceName,
            Hostname = hostname,
            OperatingSystem = operatingSystem,
            IpAddress = ipAddress,
            Status = "Offline",
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Devices.Add(device);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsHostnameUniqueViolation(exception))
        {
            return Conflict(new
            {
                message = "A device with this hostname already exists.",
                hostname
            });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = device.Id },
            device);
    }

    // PUT /api/devices/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Device>> Update(
        Guid id,
        [FromBody] UpdateDeviceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new
            {
                message = "A device request body is required."
            });
        }

        var device = await _db.Devices
            .FirstOrDefaultAsync(
                device => device.Id == id,
                cancellationToken);

        if (device is null)
        {
            return NotFound(new
            {
                message = "Device not found."
            });
        }

        var deviceName = Normalize(request.DeviceName);
        var hostname = Normalize(request.Hostname);
        var operatingSystem = Normalize(request.OperatingSystem);
        var ipAddress = Normalize(request.IpAddress);
        var status = Normalize(request.Status);

        var validationError = ValidateDeviceFields(
            deviceName,
            hostname,
            operatingSystem,
            ipAddress);

        if (validationError is not null)
        {
            return BadRequest(new
            {
                message = validationError
            });
        }

        if (status.Length == 0)
        {
            return BadRequest(new
            {
                message = "Status is required."
            });
        }

        if (status.Length > 30)
        {
            return BadRequest(new
            {
                message = "Status cannot exceed 30 characters."
            });
        }

        var allowedStatus = AllowedStatuses.FirstOrDefault(
            value => string.Equals(
                value,
                status,
                StringComparison.OrdinalIgnoreCase));

        if (allowedStatus is null)
        {
            return BadRequest(new
            {
                message = "Invalid status. Allowed values: Online, Offline."
            });
        }

        // Do not treat the current device's hostname as a duplicate.
        var normalizedHostname = NormalizeHostname(hostname);

        var hostnameExists = await _db.Devices.AnyAsync(
            existingDevice =>
                existingDevice.Id != id &&
                existingDevice.Hostname.Trim().ToLower() == normalizedHostname,
            cancellationToken);

        if (hostnameExists)
        {
            return Conflict(new
            {
                message = "Another device with this hostname already exists.",
                hostname
            });
        }

        device.DeviceName = deviceName;
        device.Hostname = hostname;
        device.OperatingSystem = operatingSystem;
        device.IpAddress = ipAddress;
        device.Status = allowedStatus;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsHostnameUniqueViolation(exception))
        {
            return Conflict(new
            {
                message = "A device with this hostname already exists.",
                hostname
            });
        }

        return Ok(device);
    }

    // DELETE /api/devices/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var device = await _db.Devices
            .FirstOrDefaultAsync(
                device => device.Id == id,
                cancellationToken);

        if (device is null)
        {
            return NotFound(new
            {
                message = "Device not found."
            });
        }

        _db.Devices.Remove(device);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeHostname(string hostname)
    {
        return hostname.Trim().ToLowerInvariant();
    }

    private static string? ValidateDeviceFields(
        string deviceName,
        string hostname,
        string operatingSystem,
        string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(deviceName) ||
            string.IsNullOrWhiteSpace(hostname) ||
            string.IsNullOrWhiteSpace(operatingSystem) ||
            string.IsNullOrWhiteSpace(ipAddress))
        {
            return "DeviceName, Hostname, OperatingSystem and IpAddress are required.";
        }

        if (deviceName.Length > 150)
        {
            return "DeviceName cannot exceed 150 characters.";
        }

        if (hostname.Length > 255)
        {
            return "Hostname cannot exceed 255 characters.";
        }

        if (operatingSystem.Length > 150)
        {
            return "OperatingSystem cannot exceed 150 characters.";
        }

        if (ipAddress.Length > 45)
        {
            return "IpAddress cannot exceed 45 characters.";
        }

        if (!IPAddress.TryParse(ipAddress, out _))
        {
            return "IpAddress must be a valid IPv4 or IPv6 address.";
        }

        return null;
    }

    private static bool IsHostnameUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == "IX_devices_Hostname_CaseInsensitive";
    }
}

public sealed record CreateDeviceRequest(
    string? DeviceName,
    string? Hostname,
    string? OperatingSystem,
    string? IpAddress);

public sealed record UpdateDeviceRequest(
    string? DeviceName,
    string? Hostname,
    string? OperatingSystem,
    string? IpAddress,
    string? Status);
