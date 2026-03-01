namespace uManageIt.Website.Domain;

public sealed class WebsiteApiKey
{
    public Guid Id { get; set; }
    public Guid WebsiteId { get; set; }
    public ManagedWebsite? Website { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
