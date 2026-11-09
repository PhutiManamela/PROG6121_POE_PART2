namespace RaceDay.Api.Services;

public interface IPasswordHasher
{
    /// <summary>Produces a salted hash of the given plain-text password. Never store the plain text itself.</summary>
    string Hash(string password);

    /// <summary>Checks a plain-text password against a previously stored hash.</summary>
    bool Verify(string password, string hash);
}
