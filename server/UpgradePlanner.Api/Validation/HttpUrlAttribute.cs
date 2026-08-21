using System.ComponentModel.DataAnnotations;

namespace UpgradePlanner.Api.Validation;

/// <summary>
/// Requires an absolute <c>http</c> or <c>https</c> URL.
/// </summary>
/// <remarks>
/// <para>
/// This is <b>input validation, not XSS protection</b>, and the distinction
/// matters. The field it guards, <c>ProductLink</c>, is never rendered as an
/// anchor — the client shows it in an <c>&lt;input type="url"&gt;</c> — so
/// storing <c>javascript:alert(1)</c> was never an XSS vulnerability. It was
/// simply invalid data in a field whose whole meaning is "a link to a product
/// page", and a value the app would have to reject anyway the day someone makes
/// that field clickable.
/// </para>
/// <para>
/// Calling this an XSS defence in the documentation would be exactly the kind of
/// security theatre the audit was meant to remove, so it is not called one.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return ValidationResult.Success;
        if (value is not string raw || string.IsNullOrWhiteSpace(raw)) return ValidationResult.Success;

        var ok = Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        return ok
            ? ValidationResult.Success
            : new ValidationResult(
                $"The field {validationContext.DisplayName} must be an absolute http:// or https:// URL.",
                validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}
