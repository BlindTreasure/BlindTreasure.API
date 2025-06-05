namespace BlindTreasure.Infrastructure.Interfaces;

public interface IClaimsService
{
    public Guid CurrentUserId { get; }
    public string? IpAddress { get; }
}