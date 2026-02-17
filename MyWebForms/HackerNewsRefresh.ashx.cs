using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using MyWebForms.Services;
using Newtonsoft.Json;

namespace MyWebForms
{
    /// <summary>
    /// HTTP handler that returns a lightweight JSON snapshot of the current
    /// story list for a given HN tab.  The client polls this endpoint on a
    /// timer and compares scores against what is already rendered — updating
    /// scores in-place and only triggering a full postback when the set of
    /// story IDs has actually changed.
    ///
    /// Educational notes — HttpHandler pattern
    /// ----------------------------------------
    /// IHttpAsyncHandler lets us do async I/O (HN API calls) without blocking
    /// a thread-pool thread for the duration of the network wait.  We implement
    /// it via a Task-based adapter: BeginProcessRequest starts the Task and
    /// returns an IAsyncResult; EndProcessRequest is called by ASP.NET when
    /// the Task completes.
    ///
    /// IsReusable = true is safe here because we have no per-request instance
    /// state (everything is in local variables of ProcessRequestAsync).
    ///
    /// Route: registered via RouteConfig so the URL is /hn-refresh
    /// Query params:
    ///   tab  — one of top|new|best|ask|show|jobs  (default: top)
    ///   ids  — comma-separated list of item IDs currently shown on the page
    ///           (used by the handler to decide whether to include scores)
    /// </summary>
    public class HackerNewsRefresh : HttpTaskAsyncHandler
    {
        // Only fetch scores for the IDs the client already has on screen.
        // Cap at 30 to keep the response fast.
        private const int MaxScoreFetch = 30;

        public override bool IsReusable { get { return true; } }

        public override async Task ProcessRequestAsync(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            var tab = context.Request.QueryString["tab"] ?? "top";
            var rawIds = context.Request.QueryString["ids"] ?? string.Empty;

            var service = new HackerNewsService();

            try
            {
                // 1. Fetch the current list of IDs for this tab.
                var currentIds = await GetIdsForTabAsync(service, tab)
                    .ConfigureAwait(false);

                // Take the first page worth for comparison.
                var firstPageIds = currentIds.Take(20).ToList();

                // 2. Parse the IDs the client says it is currently showing.
                var clientIds = ParseIds(rawIds);

                // 3. Decide whether the story list has changed.
                var listChanged = !firstPageIds.SequenceEqual(clientIds);

                // 4. Fetch scores for whichever IDs the client has on screen,
                //    capped to avoid hammering the API.
                var scoreIds = clientIds.Count > 0 ? clientIds : firstPageIds;
                scoreIds = scoreIds.Take(MaxScoreFetch).ToList();

                var scores = new Dictionary<int, int>();
                // Fetch items concurrently — HackerNewsService is stateless.
                var tasks = scoreIds.Select(id => service.GetItemAsync(id));
                var items = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var item in items)
                {
                    if (item != null)
                        scores[item.Id] = item.Score;
                }

                var result = new RefreshResult
                {
                    ListChanged = listChanged,
                    Scores = scores
                };

                context.Response.Write(JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                // Return a safe error payload — client will ignore score updates
                // but won't crash.
                var error = new { error = ex.Message };
                context.Response.StatusCode = 500;
                context.Response.Write(JsonConvert.SerializeObject(error));
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Task<List<int>> GetIdsForTabAsync(HackerNewsService svc, string tab)
        {
            switch (tab)
            {
                case "new": return svc.GetNewStoryIdsAsync();
                case "best": return svc.GetBestStoryIdsAsync();
                case "ask": return svc.GetAskStoryIdsAsync();
                case "show": return svc.GetShowStoryIdsAsync();
                case "jobs": return svc.GetJobStoryIdsAsync();
                default: return svc.GetTopStoryIdsAsync();
            }
        }

        private static List<int> ParseIds(string raw)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(raw)) return result;
            foreach (var part in raw.Split(','))
            {
                int id;
                if (int.TryParse(part.Trim(), out id))
                    result.Add(id);
            }
            return result;
        }

        // ── Response DTO ─────────────────────────────────────────────────────

        private class RefreshResult
        {
            [JsonProperty("listChanged")]
            public bool ListChanged { get; set; }

            /// <summary>Map of storyId → current score.</summary>
            [JsonProperty("scores")]
            public Dictionary<int, int> Scores { get; set; }
        }
    }
}
