using System;

namespace Leayal.SnowBreakLauncher.LeaHelpers
{
    internal static class UrlHelper
    {
        public static string MakeAbsoluteUrl(ReadOnlySpan<char> baseUrl, ReadOnlySpan<char> relativeOrAbsoluteUrl, bool forceRelativeUrl = false)
        {
            var sanitized_relativeOrAbsoluteUrl = forceRelativeUrl ? relativeOrAbsoluteUrl.TrimStart('/') : relativeOrAbsoluteUrl;
            var sanitized_baseUrl = baseUrl.TrimEnd('/');

            return $"{sanitized_baseUrl}/{sanitized_relativeOrAbsoluteUrl}";
        }

        public static Uri? MakeAbsoluteUri(Uri? baseUrl, string relativeOrAbsoluteUrl, bool forceRelativeUrl = false)
        {
            var sanitized_relativeOrAbsoluteUrl = forceRelativeUrl ? relativeOrAbsoluteUrl.TrimStart('/') : relativeOrAbsoluteUrl;
            
            if (Uri.TryCreate(baseUrl, relativeOrAbsoluteUrl, out var result))
            {
                return result;
            }
            return null;
        }
    }
}
