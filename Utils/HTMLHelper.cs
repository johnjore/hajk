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


                // Strip unwanted tags like <br>, but keep URLs
                foreach (var br in doc.DocumentNode.SelectNodes("//br") ?? Enumerable.Empty<HtmlNode>())
                    br.Remove();

                foreach (var a in doc.DocumentNode.SelectNodes("//a") ?? Enumerable.Empty<HtmlNode>())
                {
                    string href = a.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(href))
                    {
                        string replacement = $"{a.InnerText} ({href})";
                        a.ParentNode.ReplaceChild(HtmlTextNode.CreateNode(replacement), a);
                    }
                }

                var cleanedText = doc.DocumentNode.InnerText.Trim();
                return WebUtility.HtmlDecode(cleanedText);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to parse HTML string");
            }

            return string.Empty;
        }
    }
}
