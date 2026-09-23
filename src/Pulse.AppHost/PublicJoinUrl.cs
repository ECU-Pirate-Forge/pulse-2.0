namespace Pulse.AppHost;

public static class PublicJoinUrl
{
    public static string Normalize(string value)
    {
        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "The public join base URL must be absolute HTTPS without credentials, a query, or a fragment.",
                nameof(value));
        }

        return normalized.TrimEnd('/');
    }
}
