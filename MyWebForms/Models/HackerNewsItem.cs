using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyWebForms.Models
{
    /// <summary>
    /// Represents any HN item: story, comment, job, poll, or pollopt.
    /// Property names match the HN Firebase API JSON keys via JsonProperty.
    /// </summary>
    public class HackerNewsItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("deleted")]
        public bool Deleted { get; set; }

        /// <summary>
        /// One of: "job", "story", "comment", "poll", "pollopt"
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("by")]
        public string By { get; set; }

        /// <summary>Unix timestamp.</summary>
        [JsonProperty("time")]
        public long Time { get; set; }

        /// <summary>HTML text for comments, polls, Ask HNs.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("dead")]
        public bool Dead { get; set; }

        [JsonProperty("parent")]
        public int? Parent { get; set; }

        [JsonProperty("poll")]
        public int? Poll { get; set; }

        /// <summary>Child comment IDs in ranked display order.</summary>
        [JsonProperty("kids")]
        public List<int> Kids { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>Poll option IDs in display order.</summary>
        [JsonProperty("parts")]
        public List<int> Parts { get; set; }

        /// <summary>Total comment count (stories and polls only).</summary>
        [JsonProperty("descendants")]
        public int Descendants { get; set; }

        // ── Derived helpers (not from API) ───────────────────────────────────

        /// <summary>
        /// Returns the display URL — the story URL for link posts,
        /// or the HN item page for text posts (Ask, etc.).
        /// </summary>
        public string DisplayUrl
        {
            get
            {
                return !string.IsNullOrEmpty(Url)
                    ? Url
                    : string.Format("https://news.ycombinator.com/item?id={0}", Id);
            }
        }

        /// <summary>
        /// Extracts the bare hostname from the URL for the "(domain.com)" display,
        /// stripping www. prefix.
        /// </summary>
        public string Domain
        {
            get
            {
                if (string.IsNullOrEmpty(Url)) return string.Empty;
                try
                {
                    var uri = new System.Uri(Url);
                    var host = uri.Host;
                    if (host.StartsWith("www.", System.StringComparison.OrdinalIgnoreCase))
                        host = host.Substring(4);
                    return host;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// Converts the Unix timestamp to a human-readable relative time string,
        /// e.g. "3 hours ago", "2 days ago".
        /// </summary>
        public string TimeAgo
        {
            get
            {
                if (Time == 0) return string.Empty;
                var dt = System.DateTimeOffset.FromUnixTimeSeconds(Time);
                var diff = System.DateTimeOffset.UtcNow - dt;

                if (diff.TotalMinutes < 1) return "just now";
                if (diff.TotalMinutes < 60) return (int)diff.TotalMinutes + " minutes ago";
                if (diff.TotalHours < 24) return (int)diff.TotalHours + " hours ago";
                if (diff.TotalDays < 30) return (int)diff.TotalDays + " days ago";
                if (diff.TotalDays < 365) return (int)(diff.TotalDays / 30) + " months ago";
                return (int)(diff.TotalDays / 365) + " years ago";
            }
        }

        public bool IsStory { get { return Type == "story"; } }
        public bool IsComment { get { return Type == "comment"; } }
        public bool IsJob { get { return Type == "job"; } }
        public bool IsPoll { get { return Type == "poll"; } }
    }
}
