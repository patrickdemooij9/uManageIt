namespace uManageIt.Website.Domain;

public sealed class ManagedWebsite
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public ICollection<WebsiteApiKey> ApiKeys { get; set; } = new List<WebsiteApiKey>();
}
