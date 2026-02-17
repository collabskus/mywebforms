using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyWebForms.Models
{
    /// <summary>
    /// Represents a Hacker News user profile as returned by
    /// /v0/user/{id}.json
    /// </summary>
    public class HackerNewsUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>Account creation date as Unix timestamp.</summary>
        [JsonProperty("created")]
        public long Created { get; set; }

        [JsonProperty("karma")]
        public int Karma { get; set; }

        /// <summary>Optional self-description. May contain HTML.</summary>
        [JsonProperty("about")]
        public string About { get; set; }

        /// <summary>IDs of all stories, polls and comments submitted.</summary>
        [JsonProperty("submitted")]
        public List<int> Submitted { get; set; }

        // ── Derived helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Returns the account creation date as a formatted date string.
        /// </summary>
        public string MemberSince
        {
            get
            {
                if (Created == 0) return string.Empty;
                return System.DateTimeOffset.FromUnixTimeSeconds(Created)
                    .ToString("MMMM yyyy");
            }
        }
    }
}
