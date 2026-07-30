using System.Globalization;
using System.Security.Cryptography;

namespace PhilSLA.ExamPlatform.Candidate.Authentication;

public sealed class PasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int IterationCount = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            '$',
            Algorithm,
            IterationCount.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encodedHash))
        {
            return false;
        }

        try
        {
            var parts = encodedHash.Split('$');
            if (parts.Length != 4 ||
                parts[0] != Algorithm ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var iterations) ||
                iterations <= 0)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
