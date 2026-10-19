using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace FindIt.Api.Services;

public sealed class PickupCodeService(IDataProtectionProvider protectionProvider)
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly IDataProtector _protector = protectionProvider.CreateProtector("FindIt.PickupCode.v1");

    public string Generate() => new(Enumerable.Range(0, 4)
        .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());

    public string Protect(string code) => _protector.Protect(code);

    public string Unprotect(string encrypted) => _protector.Unprotect(encrypted);

    public bool Verify(string encrypted, string candidate)
    {
        var expected = Encoding.ASCII.GetBytes(Unprotect(encrypted));
        var supplied = Encoding.ASCII.GetBytes(candidate.Trim().ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
