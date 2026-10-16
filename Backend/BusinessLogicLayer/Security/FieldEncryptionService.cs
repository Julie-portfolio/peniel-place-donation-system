using System.Globalization;
using Microsoft.AspNetCore.DataProtection;

namespace PenielPlaceMinistriesCSharp.Security;

public interface IFieldEncryptionService
{
    string? Protect(string? plaintext);
    string? Unprotect(string? protectedValue);
    string? ProtectAmount(decimal? amount);
    decimal? UnprotectAmount(string? protectedValue);
}

public sealed class FieldEncryptionService : IFieldEncryptionService
{
    private readonly IDataProtector _protector;
    public FieldEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("PenielPlace.SensitiveFields.v1");
    }

    public string? Protect(string? plaintext) =>
        plaintext is null ? null : _protector.Protect(plaintext);

    public string? Unprotect(string? protectedValue) =>
        protectedValue is null ? null : _protector.Unprotect(protectedValue);

    public string? ProtectAmount(decimal? amount) =>
        amount is null
            ? null
            : _protector.Protect(amount.Value.ToString("0.00", CultureInfo.InvariantCulture));

    public decimal? UnprotectAmount(string? protectedValue)
    {
        if (protectedValue is null) return null;
        var raw = _protector.Unprotect(protectedValue);
        return decimal.Parse(raw, NumberStyles.Number, CultureInfo.InvariantCulture);
    }
}
