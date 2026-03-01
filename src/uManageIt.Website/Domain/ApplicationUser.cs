using Microsoft.AspNetCore.Identity;

namespace uManageIt.Website.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ICollection<ManagedWebsite> Websites { get; set; } = new List<ManagedWebsite>();
}
