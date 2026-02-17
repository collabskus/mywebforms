using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using MyWebForms.Models;
using Newtonsoft.Json;

namespace MyWebForms.Services
{
    /// <summary>
    /// Fetches data from the public Hacker News Firebase REST API v0.
    /// All public methods are virtual so they can be overridden in test fakes.
    ///
    /// Threading note: HttpClient is reused as a static instance — this is the
    /// correct pattern for .NET Framework to avoid socket exhaustion.
    ///
    /// Caching note: results are stored in HttpRuntime.Cache (the built-in
    /// ASP.NET object cache) to avoid hammering the API on every postback.
    /// Cache durations are kept short (60 s for lists, 5 min for items)
    /// so the "live" feel is preserved.
    /// </summary>
    public class HackerNewsService
    {
        private const string BaseUrl = "https://hacker-news.firebaseio.com/v0/";

        // A single shared HttpClient for the lifetime of the AppDomain.
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add(
                "User-Agent", "MyWebForms-HN-Demo/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }

        // ── Story list endpoints ─────────────────────────────────────────────

        public virtual Task<List<int>> GetTopStoryIdsAsync()
            => GetIdListAsync("topstories.json", "hn_top");

        public virtual Task<List<int>> GetNewStoryIdsAsync()
            => GetIdListAsync("newstories.json", "hn_new");

        public virtual Task<List<int>> GetBestStoryIdsAsync()
            => GetIdListAsync("beststories.json", "hn_best");

        public virtual Task<List<int>> GetAskStoryIdsAsync()
            => GetIdListAsync("askstories.json", "hn_ask");

        public virtual Task<List<int>> GetShowStoryIdsAsync()
            => GetIdListAsync("showstories.json", "hn_show");

        public virtual Task<List<int>> GetJobStoryIdsAsync()
            => GetIdListAsync("jobstories.json", "hn_jobs");

        // ── Item and user fetching ───────────────────────────────────────────

        /// <summary>
        /// Fetches a single item by ID. Results are cached for 5 minutes.
        /// Returns null if the item cannot be fetched (deleted, network error, etc.).
        /// </summary>
        public virtual async Task<HackerNewsItem> GetItemAsync(int id)
        {
            var cacheKey = "hn_item_" + id;
            var cached = HttpRuntime.Cache[cacheKey] as HackerNewsItem;
            if (cached != null) return cached;

            try
            {
                var url = BaseUrl + "item/" + id + ".json";
                var json = await Http.GetStringAsync(url).ConfigureAwait(false);
                var item = JsonConvert.DeserializeObject<HackerNewsItem>(json);
                if (item != null)
                {
                    HttpRuntime.Cache.Insert(cacheKey, item, null,
                        DateTime.UtcNow.AddMinutes(5), Cache.NoSlidingExpiration);
                }
                return item;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches a user profile by username.
        /// Returns null if the user is not found or there is a network error.
        /// </summary>
        public virtual async Task<HackerNewsUser> GetUserAsync(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;

            var cacheKey = "hn_user_" + username;
            var cached = HttpRuntime.Cache[cacheKey] as HackerNewsUser;
            if (cached != null) return cached;

            try
            {
                var url = BaseUrl + "user/" + HttpUtility.UrlEncode(username) + ".json";
                var json = await Http.GetStringAsync(url).ConfigureAwait(false);
                var user = JsonConvert.DeserializeObject<HackerNewsUser>(json);
                if (user != null)
                {
                    HttpRuntime.Cache.Insert(cacheKey, user, null,
                        DateTime.UtcNow.AddMinutes(10), Cache.NoSlidingExpiration);
                }
                return user;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches the current maximum item ID.
        /// </summary>
        public virtual async Task<int> GetMaxItemIdAsync()
        {
            const string cacheKey = "hn_maxitem";
            var cached = HttpRuntime.Cache[cacheKey];
            if (cached != null) return (int)cached;

            try
            {
                var json = await Http.GetStringAsync(BaseUrl + "maxitem.json")
                    .ConfigureAwait(false);
                var id = JsonConvert.DeserializeObject<int>(json);
                HttpRuntime.Cache.Insert(cacheKey, id, null,
                    DateTime.UtcNow.AddSeconds(30), Cache.NoSlidingExpiration);
                return id;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Fetches a page of story items from a list of IDs.
        /// Items that fail to load (deleted, network error) are silently skipped.
        /// </summary>
        /// <param name="ids">Full ordered list of story IDs.</param>
        /// <param name="page">1-based page number.</param>
        /// <param name="pageSize">Number of items per page.</param>
        public virtual async Task<List<HackerNewsItem>> GetItemPageAsync(
            List<int> ids, int page, int pageSize)
        {
            if (ids == null || ids.Count == 0) return new List<HackerNewsItem>();

            page = Math.Max(1, page);
            var slice = ids
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Fetch concurrently — Task.WhenAll is safe here because
            // individual failures are caught inside GetItemAsync.
            var tasks = slice.Select(id => GetItemAsync(id));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return results
                .Where(item => item != null && !item.Deleted && !item.Dead)
                .ToList();
        }

        /// <summary>
        /// Recursively fetches the comment tree for an item up to a given depth.
        /// Depth 0 = top-level comments only. Each comment's Kids are loaded
        /// one level deeper up to maxDepth.
        /// </summary>
        public virtual async Task<List<HackerNewsItem>> GetCommentTreeAsync(
            HackerNewsItem parent, int maxDepth = 3)
        {
            if (parent == null || parent.Kids == null || parent.Kids.Count == 0)
                return new List<HackerNewsItem>();

            return await FetchCommentsAsync(parent.Kids, 0, maxDepth)
                .ConfigureAwait(false);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private async Task<List<int>> GetIdListAsync(string endpoint, string cacheKey)
        {
            var cached = HttpRuntime.Cache[cacheKey] as List<int>;
            if (cached != null) return cached;

            try
            {
                var json = await Http.GetStringAsync(BaseUrl + endpoint)
                    .ConfigureAwait(false);
                var ids = JsonConvert.DeserializeObject<List<int>>(json)
                          ?? new List<int>();

                HttpRuntime.Cache.Insert(cacheKey, ids, null,
                    DateTime.UtcNow.AddSeconds(60), Cache.NoSlidingExpiration);
                return ids;
            }
            catch
            {
                return new List<int>();
            }
        }

        private async Task<List<HackerNewsItem>> FetchCommentsAsync(
            List<int> kidIds, int currentDepth, int maxDepth)
        {
            var comments = new List<HackerNewsItem>();

            var tasks = kidIds.Select(id => GetItemAsync(id));
            var items = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var comment in items)
            {
                if (comment == null || comment.Deleted || comment.Dead) continue;
                comments.Add(comment);

                if (currentDepth < maxDepth
                    && comment.Kids != null
                    && comment.Kids.Count > 0)
                {
                    comment.Kids = comment.Kids.Take(10).ToList(); // limit breadth
                    var children = await FetchCommentsAsync(
                        comment.Kids, currentDepth + 1, maxDepth)
                        .ConfigureAwait(false);
                    // Attach children as a separate flat list associated via Parent.
                    // The HnCommentTree control handles nesting visually.
                    comments.AddRange(children);
                }
            }

            return comments;
        }
    }
}
