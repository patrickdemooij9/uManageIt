using System.Security.Cryptography;
using System.Text;

namespace uManageIt.Website.Services;

public interface IApiKeyHasher
{
    string Hash(string value);
}

public sealed class ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
