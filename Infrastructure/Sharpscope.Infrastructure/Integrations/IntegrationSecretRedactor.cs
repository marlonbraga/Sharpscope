using System.Text.RegularExpressions;

namespace Sharpscope.Infrastructure.Integrations;

public static class IntegrationSecretRedactor
{
    private static readonly Regex UriUserPassRegex = new(
        "(://[^/\\s:]+:)([^@/\\s]+)@",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex QuerySecretRegex = new(
        "([?&])(password|pwd|sharedaccesskey|accountkey|sharedaccesssignature|sig|sastoken|token|apikey|key)=([^&]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex KeyValueSecretRegex = new(
        "(^|[;,&?])\\s*(password|pwd|sharedaccesskey|accountkey|sharedaccesssignature|sig|sastoken|token|apikey|key)\\s*=\\s*([^;,&]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var redacted = value;

        // Redact user:pass@ in URIs
        redacted = UriUserPassRegex.Replace(redacted, "$1***@");

        // Redact query parameters
        redacted = QuerySecretRegex.Replace(redacted, "$1$2=***");

        // Redact key/value pairs in connection strings
        redacted = KeyValueSecretRegex.Replace(redacted, "$1$2=***");

        return redacted;
    }
}
