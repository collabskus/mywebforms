using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using MyWebForms.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    ///
    /// New methods
    /// -----------
    /// GetActiveItemIdsAsync()
    ///   Calls /v0/updates.json which returns { "items": [...], "profiles": [...] }.
    ///   The "items" array contains IDs recently updated on HN — this is what
    ///   https://news.ycombinator.com/active shows.
    ///   Cached for 30 s (short, because "active" is very time-sensitive).
    ///
    /// GetRisingStoryIdsAsync(minComments, minPoints, candidates)
    ///   Fetches the first `candidates` new story IDs, loads those items
    ///   concurrently, then returns only the IDs of stories satisfying:
    ///       (descendants >= minComments) OR (score >= minPoints)
    ///   Cached for 60 s.  The caller passes candidates so the page controls
    ///   the breadth of the search without touching the service.
    /// </summary>
    public class HackerNewsService
    {
        private const string BaseUrl = "https://hacker-news.firebaseio.com/v0/";

        // A single shared HttpClient for the lifetime of the AppDomain.
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MyWebForms-HN-Demo/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
            return client;
        }

        // ── Story list endpoints ──────────────────────────────────────────────

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

        /// <summary>
        /// Returns the list of recently-updated item IDs from /v0/updates.json.
        ///
        /// The HN API's updates endpoint returns a JSON object:
        ///   { "items": [id, id, ...], "profiles": ["user", ...] }
        ///
        /// We extract only the "items" array and cache it for 30 seconds
        /// because "active" is intended to show what's happening right now.
        ///
        /// Educational note — JObject vs a typed DTO:
        ///   We use Newtonsoft's JObject here to parse just the "items" field
        ///   without declaring a full DTO class.  This is fine for a single
        ///   field; if we needed the profiles array too we would add a DTO.
        /// </summary>
        public virtual async Task<List<int>> GetActiveItemIdsAsync()
        {
            const string cacheKey = "hn_active";
            var cached = HttpRuntime.Cache[cacheKey] as List<int>;
            if (cached != null) return cached;

            try
            {
                var json = await Http.GetStringAsync(BaseUrl + "updates.json")
                    .ConfigureAwait(false);

                var obj  = JObject.Parse(json);
                var ids  = obj["items"] != null
                    ? obj["items"].ToObject<List<int>>()
                    : new List<int>();

                HttpRuntime.Cache.Insert(cacheKey, ids, null,
                    DateTime.UtcNow.AddSeconds(30), Cache.NoSlidingExpiration);

                return ids;
            }
            catch
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// Returns IDs from the "new" list that pass a quality threshold.
        ///
        /// Algorithm:
        ///   1. Fetch up to <paramref name="candidates"/> new story IDs.
        ///   2. Fetch those items concurrently (each individually cached for 5 min).
        ///   3. Keep only stories where:
        ///          descendants >= minComments  OR  score >= minPoints
        ///      (either threshold being zero means that criterion is ignored)
        ///   4. Return the filtered IDs in their original order.
        ///
        /// The entire filtered ID list is cached for 60 s under a key that
        /// encodes the thresholds, so different threshold combinations are
        /// independently cached.
        ///
        /// Educational note — why not filter on the server API:
        ///   The HN Firebase API is a thin data-access layer with no server-side
        ///   filtering or sorting beyond the pre-built lists (top/new/best etc.).
        ///   Filtering with quality criteria must be done client-side (i.e., in
        ///   our service layer) by fetching items and inspecting their fields.
        /// </summary>
        public virtual async Task<List<int>> GetRisingStoryIdsAsync(
            int minComments, int minPoints, int candidates = 200)
        {
            // Build a cache key that encodes the thresholds so that changing
            // the thresholds always produces a fresh result.
            var cacheKey = string.Format("hn_rising_{0}_{1}_{2}",
                minComments, minPoints, candidates);

            var cached = HttpRuntime.Cache[cacheKey] as List<int>;
            if (cached != null) return cached;

            try
            {
                // Step 1: get the raw new-story ID list.
                var allNewIds = await GetNewStoryIdsAsync().ConfigureAwait(false);
                var slice     = allNewIds.Take(candidates).ToList();

                // Step 2: fetch items concurrently.
                var tasks   = slice.Select(id => GetItemAsync(id));
                var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                // Step 3: filter.
                var filtered = new List<int>();
                foreach (var item in results)
                {
                    if (item == null || item.Deleted || item.Dead) continue;

                    bool passComments = minComments > 0 && item.Descendants >= minComments;
                    bool passPoints   = minPoints   > 0 && item.Score       >= minPoints;

                    // If both thresholds are zero, admit everything (same as /new).
                    if (minComments == 0 && minPoints == 0)
                    {
                        filtered.Add(item.Id);
                    }
                    else if (passComments || passPoints)
                    {
                        filtered.Add(item.Id);
                    }
                }

                // Preserve original relative order (newest first, as returned
                // by /newstories.json).
                var idOrder = new Dictionary<int, int>();
                for (int i = 0; i < slice.Count; i++) idOrder[slice[i]] = i;
                filtered.Sort(delegate(int a, int b)
                {
                    int ia, ib;
                    idOrder.TryGetValue(a, out ia);
                    idOrder.TryGetValue(b, out ib);
                    return ia.CompareTo(ib);
                });

                HttpRuntime.Cache.Insert(cacheKey, filtered, null,
                    DateTime.UtcNow.AddSeconds(60), Cache.NoSlidingExpiration);

                return filtered;
            }
            catch
            {
                return new List<int>();
            }
        }

        // ── Item and user fetching ────────────────────────────────────────────

        /// <summary>
        /// Fetches a single item by ID. Results are cached for 5 minutes.
        /// Returns null if the item cannot be fetched (deleted, network error, etc.).
        /// </summary>
        public virtual async Task<HackerNewsItem> GetItemAsync(int id)
        {
            var cacheKey = "hn_item_" + id;
            var cached   = HttpRuntime.Cache[cacheKey] as HackerNewsItem;
            if (cached != null) return cached;

            try
            {
                var url  = BaseUrl + "item/" + id + ".json";
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
            var cached   = HttpRuntime.Cache[cacheKey] as HackerNewsUser;
            if (cached != null) return cached;

            try
            {
                var url  = BaseUrl + "user/" + HttpUtility.UrlEncode(username) + ".json";
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
        /// Cached for 30 seconds — used by the live-feed page and by background
        /// polling to detect new items.
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
        public virtual async Task<List<HackerNewsItem>> GetItemPageAsync(
            List<int> ids, int page, int pageSize)
        {
            if (ids == null || ids.Count == 0) return new List<HackerNewsItem>();

            page = Math.Max(1, page);
            var slice = ids
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var tasks   = slice.Select(id => GetItemAsync(id));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return results
                .Where(item => item != null && !item.Deleted && !item.Dead)
                .ToList();
        }

        /// <summary>
        /// Recursively fetches the comment tree for an item up to a given depth.
        /// Depth 0 = top-level comments only.  Each comment's Kids are loaded
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
                    comment.Kids = comment.Kids.Take(10).ToList();
                    var children = await FetchCommentsAsync(
                        comment.Kids, currentDepth + 1, maxDepth)
                        .ConfigureAwait(false);
                    comments.AddRange(children);
                }
            }

            return comments;
        }
    }
}
