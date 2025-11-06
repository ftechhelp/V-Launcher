using System.ComponentModel.DataAnnotations;
using System.Security;

namespace V_Launcher.Validation;

/// <summary>
/// Helper class for validation operations
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates that a string is not null or whitespace
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public static ValidationResult? ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ValidationResult($"{fieldName} is required.");
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a SecureString is not null or empty
    /// </summary>
    /// <param name="value">The SecureString to validate</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public static ValidationResult? ValidateSecureStringRequired(SecureString? value, string fieldName)
    {
        if (value == null || value.Length == 0)
        {
            return new ValidationResult($"{fieldName} is required.");
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a string meets minimum length requirements
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="minLength">The minimum required length</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public static ValidationResult? ValidateMinLength(string? value, int minLength, string fieldName)
    {
        if (!string.IsNullOrEmpty(value) && value.Length < minLength)
        {
            return new ValidationResult($"{fieldName} must be at least {minLength} characters long.");
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a SecureString meets minimum length requirements
    /// </summary>
    /// <param name="value">The SecureString to validate</param>
    /// <param name="minLength">The minimum required length</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public static ValidationResult? ValidateSecureStringMinLength(SecureString? value, int minLength, string fieldName)
    {
        if (value != null && value.Length > 0 && value.Length < minLength)
        {
            return new ValidationResult($"{fieldName} must be at least {minLength} characters long.");
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a string contains only valid domain characters
    /// </summary>
    /// <param name="value">The domain value to validate</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public static ValidationResult? ValidateDomain(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success; // Let required validation handle empty values
        }

        // Basic domain validation - alphanumeric, dots, and hyphens
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z0-9.-]+$"))
        {
            return new ValidationResult($"{fieldName} contains invalid characters. Only letters, numbers, dots, and hyphens are allowed.");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a string contains only valid username characters
    /// </summary>
    /// <param name="value">The username value to validate</param>
    /// <param name="fieldName">The name of the field being validated</param>
    /// <returns>Validation result</returns>
    public static ValidationResult? ValidateUsername(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success; // Let required validation handle empty values
        }

        // Basic username validation - alphanumeric, dots, underscores, and hyphens
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-zA-Z0-9._-]+$"))
        {
            return new ValidationResult($"{fieldName} contains invalid characters. Only letters, numbers, dots, underscores, and hyphens are allowed.");
        }

        return ValidationResult.Success;
    }
}