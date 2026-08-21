using System.ComponentModel.DataAnnotations;

namespace UpgradePlanner.Api.Validation;

/// <summary>
/// Rejects enum values outside the declared set.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core's JSON binder happily converts any integer to an enum-typed
/// property: <c>{"category": 99}</c> bound to <see cref="Models.UpgradeCategory"/>
/// produced <c>201 Created</c> before this attribute existed, and the client then
/// rendered <c>CATEGORY_LABELS[99]</c> as <c>undefined</c>. The enum's declared
/// members are a trust boundary like any other, so they get checked like one.
/// </para>
/// <para>
/// <see cref="EnumDataTypeAttribute"/> is not used because it accepts any value
/// for a <c>[Flags]</c> enum and reports failures with a message that names the
/// CLR type rather than the field.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class EnumDefinedAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Absent is a separate concern; [Required] owns it.
        if (value is null) return ValidationResult.Success;

        var type = value.GetType();
        if (!type.IsEnum || !Enum.IsDefined(type, value))
        {
            var name = validationContext.DisplayName;
            var allowed = type.IsEnum ? string.Join(", ", Enum.GetValues(type).Cast<object>().Select(v => (int)v)) : "";
            return new ValidationResult(
                $"The field {name} must be one of: {allowed}.",
                validationContext.MemberName is null ? null : [validationContext.MemberName]);
        }

        return ValidationResult.Success;
    }
}
