namespace uManageIt.Core.Configuration;

public sealed class uManageItOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public Guid WebsiteId { get; set; }
    public TimeSpan RuntimeCollectionInterval { get; set; } = TimeSpan.FromSeconds(15);
    public int MaxQueueSize { get; set; } = 5000;
    public int BatchSize { get; set; } = 100;
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
}
