namespace MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;

/// <summary>Hashes and verifies passwords. Backed by BCrypt.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
