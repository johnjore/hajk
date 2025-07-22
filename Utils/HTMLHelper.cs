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
            if (html == null || html == string.Empty)
                return string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(html))
                    return string.Empty;

                var doc = new HtmlDocument { OptionAutoCloseOnEnd = true };
                doc.LoadHtml(html);

                // Extract text and decode HTML entities
                var plainText = doc.DocumentNode.InnerText;
                return WebUtility.HtmlDecode(plainText);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to parse HTML string");
            }

            return string.Empty;
        }
    }
}
