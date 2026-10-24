namespace RemotePro.Api.Models;

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DeviceName { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string Status { get; set; } = "Offline";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastSeenAtUtc { get; set; }
}
