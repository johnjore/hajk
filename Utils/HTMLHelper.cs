using HtmlAgilityPack;
using System.Net;

namespace hajk.Utilities
{
    public static class HtmlUtils
    {
        /// <summary>
        /// Strips HTML tags and decodes common HTML entities (e.g., &deg; → °).
        /// </summary>
        public static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var doc = new HtmlDocument { OptionAutoCloseOnEnd = true };
            doc.LoadHtml(html);

            // Extract text and decode HTML entities
            var plainText = doc.DocumentNode.InnerText;
            return WebUtility.HtmlDecode(plainText);
        }
    }
}
