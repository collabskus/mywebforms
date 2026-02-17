using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using MyWebForms.Models;
using MyWebForms.Services;

namespace MyWebForms
{
    /// <summary>
    /// HackerNews.aspx code-behind.
    ///
    /// Page lifecycle summary
    /// ----------------------
    /// 1. Page_Load   — restores tab/page state from ViewState; calls
    ///                  RegisterAsyncTask to queue the async data load.
    /// 2. PreRenderComplete (raised after all async tasks finish) — binds
    ///                  the story rows and detail panel to the loaded data,
    ///                  and injects the auto-refresh script when on the New tab.
    ///
    /// Why RegisterAsyncTask instead of async void Page_Load?
    ///   RegisterAsyncTask is the correct Web Forms idiom for async work.
    ///   It integrates with the page's async pipeline so the response is
    ///   not flushed until all tasks complete. Requires Async="true" on
    ///   the <%@ Page %> directive.
    ///
    /// Why not UpdatePanel?
    ///   UpdatePanel would add MS Ajax partial rendering — valid, but this
    ///   demo keeps the dependency surface small and lets LinkButton postbacks
    ///   do a full-page render, which is simpler to reason about.
    ///
    /// Auto-refresh (New tab only)
    /// ---------------------------
    ///   When ActiveTab == "new" a JavaScript countdown is injected via
    ///   RegisterStartupScript. After AutoRefreshSeconds the script submits
    ///   btnTabNew to trigger a fresh server-side load — the same postback
    ///   the user would do manually. The LIVE badge is shown only on this tab.
    /// </summary>
    public partial class HackerNews : Page
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const int PageSize = 20;
        private const int MaxCommentDepth = 4;

        /// <summary>
        /// How many seconds between automatic refreshes on the New tab.
        /// 60 seconds is respectful of the HN Firebase API rate limits.
        /// </summary>
        private const int AutoRefreshSeconds = 60;

        // ── Service (injectable via property for testability) ────────────────

        private HackerNewsService _service;

        /// <summary>
        /// Allows a test to substitute a fake service. The real service is
        /// created lazily on first access if none has been assigned.
        /// </summary>
        public HackerNewsService Service
        {
            get { return _service ?? (_service = new HackerNewsService()); }
            set { _service = value; }
        }

        // ── Data loaded by async tasks ───────────────────────────────────────

        private List<HackerNewsItem> _storyPage;
        private int _totalIds;
        private HackerNewsItem _selectedStory;
        private List<HackerNewsItem> _comments;
        private List<HackerNewsItem> _pollOptions;
        private HackerNewsUser _selectedUser;

        // ── ViewState keys ───────────────────────────────────────────────────

        private string ActiveTab
        {
            get { return ViewState["Tab"] as string ?? "top"; }
            set { ViewState["Tab"] = value; }
        }

        private int CurrentPage
        {
            get { return (int)(ViewState["Page"] ?? 1); }
            set { ViewState["Page"] = value; }
        }

        private int SelectedItemId
        {
            get { return (int)(ViewState["SelItem"] ?? 0); }
            set { ViewState["SelItem"] = value; }
        }

        private string SelectedUsername
        {
            get { return ViewState["SelUser"] as string; }
            set { ViewState["SelUser"] = value; }
        }

        // ── Page lifecycle ───────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ActiveTab = "top";
                CurrentPage = 1;
            }

            // Expose the active tab to JavaScript via a hidden field so the
            // client-side countdown script can confirm which tab is active.
            hfActiveTab.Value = ActiveTab;

            HighlightActiveTab();

            // Queue async data fetching. PreRenderComplete fires after all
            // registered tasks have completed.
            RegisterAsyncTask(new PageAsyncTask(LoadDataAsync));
        }

        private async Task LoadDataAsync()
        {
            // ── Fetch the story list ─────────────────────────────────────────
            var ids = await GetIdsForTabAsync(ActiveTab).ConfigureAwait(false);
            _totalIds = ids.Count;
            _storyPage = await Service.GetItemPageAsync(ids, CurrentPage, PageSize)
                .ConfigureAwait(false);

            // ── Fetch story detail if one is selected ────────────────────────
            if (SelectedItemId > 0)
            {
                _selectedStory = await Service.GetItemAsync(SelectedItemId)
                    .ConfigureAwait(false);

                if (_selectedStory != null)
                {
                    _comments = await Service.GetCommentTreeAsync(
                        _selectedStory, MaxCommentDepth).ConfigureAwait(false);

                    if (_selectedStory.IsPoll && _selectedStory.Parts != null)
                    {
                        _pollOptions = new List<HackerNewsItem>();
                        foreach (var partId in _selectedStory.Parts)
                        {
                            var opt = await Service.GetItemAsync(partId)
                                .ConfigureAwait(false);
                            if (opt != null) _pollOptions.Add(opt);
                        }
                    }
                }
            }

            // ── Fetch user profile if one is selected ────────────────────────
            if (!string.IsNullOrEmpty(SelectedUsername))
            {
                _selectedUser = await Service.GetUserAsync(SelectedUsername)
                    .ConfigureAwait(false);
            }
        }

        protected override void OnPreRenderComplete(EventArgs e)
        {
            base.OnPreRenderComplete(e);
            BindStoryList();
            BindDetailPanel();
            BindUserPanel();
            BindPager();

            // Show the LIVE badge only on the "new" tab.
            lblLiveBadge.Visible = (ActiveTab == "new");

            // Inject the auto-refresh countdown script when on the New tab
            // and no detail/user panel is open (to avoid interrupting reading).
            if (ActiveTab == "new" && SelectedItemId == 0 && string.IsNullOrEmpty(SelectedUsername))
            {
                InjectAutoRefreshScript();
            }
            else
            {
                lblRefreshCountdown.Visible = false;
            }
        }

        // ── Auto-refresh ─────────────────────────────────────────────────────

        /// <summary>
        /// Injects a JavaScript countdown timer that, after AutoRefreshSeconds,
        /// clicks the "New" tab LinkButton to trigger a fresh postback.
        ///
        /// Educational notes:
        ///   - RegisterStartupScript emits the script after the page form so
        ///     all controls are in the DOM when it runs.
        ///   - We use __doPostBack directly (the standard Web Forms JS function)
        ///     because LinkButton renders as an anchor that calls __doPostBack.
        ///   - The countdown label is updated via setInterval so users see a
        ///     live "refreshing in N s" indicator — making the LIVE badge honest.
        ///   - C# 7.3 compatible string building (no interpolated verbatim $@"").
        /// </summary>
        private void InjectAutoRefreshScript()
        {
            lblRefreshCountdown.Visible = true;
            lblRefreshCountdown.Text = string.Format(
                "refreshing in {0}s", AutoRefreshSeconds);

            // The UniqueID of a LinkButton is what __doPostBack expects as its
            // first argument (the event target).
            var newTabUniqueId = btnTabNew.UniqueID;
            var countdownClientId = lblRefreshCountdown.ClientID;
            var seconds = AutoRefreshSeconds;

            var sb = new StringBuilder();
            sb.AppendLine("(function () {");
            sb.AppendLine("    var remaining = " + seconds + ";");
            sb.AppendLine("    var lbl = document.getElementById('" + countdownClientId + "');");
            sb.AppendLine("    var timer = setInterval(function () {");
            sb.AppendLine("        remaining--;");
            sb.AppendLine("        if (lbl) { lbl.textContent = 'refreshing in ' + remaining + 's'; }");
            sb.AppendLine("        if (remaining <= 0) {");
            sb.AppendLine("            clearInterval(timer);");
            sb.AppendLine("            if (lbl) { lbl.textContent = 'refreshing\u2026'; }");
            // __doPostBack(eventTarget, eventArgument) — standard Web Forms postback mechanism.
            sb.AppendLine("            __doPostBack('" + newTabUniqueId + "', '');");
            sb.AppendLine("        }");
            sb.AppendLine("    }, 1000);");
            sb.AppendLine("})();");

            // RegisterStartupScript key must be unique per script block.
            // Using the page type as the type argument is the standard idiom.
            ScriptManager.RegisterStartupScript(
                this,
                typeof(HackerNews),
                "HnAutoRefresh",
                sb.ToString(),
                addScriptTags: true);
        }

        // ── Tab click ────────────────────────────────────────────────────────

        protected void btnTab_Click(object sender, EventArgs e)
        {
            var btn = (LinkButton)sender;
            ActiveTab = btn.CommandArgument;
            CurrentPage = 1;
            SelectedItemId = 0;
            SelectedUsername = null;
        }

        // ── Pager clicks ─────────────────────────────────────────────────────

        protected void btnPrev_Click(object sender, EventArgs e)
        {
            if (CurrentPage > 1) CurrentPage--;
        }

        protected void btnNext_Click(object sender, EventArgs e)
        {
            var pageCount = (int)Math.Ceiling((double)_totalIds / PageSize);
            if (CurrentPage < pageCount) CurrentPage++;
        }

        // ── Detail panel close ───────────────────────────────────────────────

        protected void btnCloseDetail_Click(object sender, EventArgs e)
        {
            SelectedItemId = 0;
        }

        protected void btnCloseUser_Click(object sender, EventArgs e)
        {
            SelectedUsername = null;
        }

        // ── Binding helpers ───────────────────────────────────────────────────

        private void BindStoryList()
        {
            phStories.Controls.Clear();

            if (_storyPage == null || _storyPage.Count == 0)
            {
                litMessage.Text = "No stories found. The API may be temporarily unavailable.";
                pnlMessage.Visible = true;
                return;
            }

            pnlMessage.Visible = false;
            var offset = (CurrentPage - 1) * PageSize;

            for (int i = 0; i < _storyPage.Count; i++)
            {
                var story = _storyPage[i];

                var row = (HnStoryRow)LoadControl("~/HnStoryRow.ascx");
                row.Item = story;
                row.Rank = offset + i + 1;

                // Wire bubble-up events so clicks update ViewState before the
                // next LoadDataAsync call (triggered by the postback).
                row.StorySelected += OnStorySelected;
                row.AuthorSelected += OnAuthorSelected;

                phStories.Controls.Add(row);
            }
        }

        private void BindDetailPanel()
        {
            if (SelectedItemId == 0 || _selectedStory == null)
            {
                pnlStoryDetail.Visible = false;
                return;
            }

            pnlStoryDetail.Visible = true;

            litDetailTitle.Text = System.Web.HttpUtility.HtmlEncode(
                _selectedStory.Title ?? "(untitled)");

            litDetailMeta.Text = string.Format(
                "{0} points &middot; by {1} &middot; {2}",
                _selectedStory.Score,
                System.Web.HttpUtility.HtmlEncode(_selectedStory.By ?? "unknown"),
                _selectedStory.TimeAgo);

            // Self-text (Ask HN, polls, jobs)
            if (!string.IsNullOrEmpty(_selectedStory.Text))
            {
                pnlDetailText.Visible = true;
                litDetailText.Text = _selectedStory.Text;
            }
            else
            {
                pnlDetailText.Visible = false;
            }

            // Poll options
            if (_pollOptions != null && _pollOptions.Count > 0)
            {
                pnlPollOptions.Visible = true;
                phPollOptions.Controls.Clear();
                foreach (var opt in _pollOptions)
                {
                    var div = new HtmlGenericControl("div");
                    div.Attributes["class"] = "hn-poll-option d-flex justify-content-between";
                    div.InnerHtml = string.Format(
                        "<span class=\"small\">{0}</span>" +
                        "<span class=\"badge bg-secondary\">{1} votes</span>",
                        System.Web.HttpUtility.HtmlEncode(opt.Text ?? string.Empty),
                        opt.Score);
                    phPollOptions.Controls.Add(div);
                }
            }
            else
            {
                pnlPollOptions.Visible = false;
            }

            litDetailCommentCount.Text = _selectedStory.Descendants.ToString();
            pnlCommentLoading.Visible = false;

            // Render flat comment list with depth tracking
            phComments.Controls.Clear();
            if (_comments != null && _comments.Count > 0)
            {
                RenderCommentTree(_comments, _selectedStory.Id, 0);
            }
            else if (_selectedStory.Descendants > 0)
            {
                var noComments = new HtmlGenericControl("p");
                noComments.Attributes["class"] = "text-muted small";
                noComments.InnerText = "Comments could not be loaded.";
                phComments.Controls.Add(noComments);
            }
        }

        /// <summary>
        /// Walks the flat comment list (pre-order, depth already encoded via
        /// parent relationships) and creates HnComment controls at the
        /// correct visual depth.
        /// </summary>
        private void RenderCommentTree(List<HackerNewsItem> comments, int parentId, int depth)
        {
            foreach (var comment in comments)
            {
                if (comment.Parent != parentId) continue;

                var ctrl = (HnComment)LoadControl("~/HnComment.ascx");
                ctrl.Item = comment;
                ctrl.Depth = depth;
                ctrl.AuthorSelected += OnAuthorSelected;
                phComments.Controls.Add(ctrl);

                // Recurse for children if within depth limit
                if (depth < MaxCommentDepth)
                {
                    RenderCommentTree(comments, comment.Id, depth + 1);
                }
            }
        }

        private void BindUserPanel()
        {
            if (string.IsNullOrEmpty(SelectedUsername) || _selectedUser == null)
            {
                pnlUserProfile.Visible = false;
                return;
            }

            pnlUserProfile.Visible = true;
            ucUserCard.User = _selectedUser;
        }

        private void BindPager()
        {
            var pageCount = _totalIds > 0
                ? (int)Math.Ceiling((double)_totalIds / PageSize)
                : 1;

            litPage.Text = CurrentPage.ToString();
            litPageCount.Text = pageCount.ToString();
            btnPrev.Enabled = CurrentPage > 1;
            btnNext.Enabled = CurrentPage < pageCount;
            pnlPager.Visible = pageCount > 1;
        }

        private void HighlightActiveTab()
        {
            // Reset all tabs then activate the current one.
            foreach (var btn in new[] { btnTabTop, btnTabNew, btnTabBest,
                                        btnTabAsk, btnTabShow, btnTabJobs })
            {
                btn.CssClass = "nav-link";
            }

            switch (ActiveTab)
            {
                case "top": btnTabTop.CssClass = "nav-link active"; break;
                case "new": btnTabNew.CssClass = "nav-link active"; break;
                case "best": btnTabBest.CssClass = "nav-link active"; break;
                case "ask": btnTabAsk.CssClass = "nav-link active"; break;
                case "show": btnTabShow.CssClass = "nav-link active"; break;
                case "jobs": btnTabJobs.CssClass = "nav-link active"; break;
            }
        }

        // ── Event handlers from child controls ───────────────────────────────

        private void OnStorySelected(object sender, StorySelectedEventArgs e)
        {
            SelectedItemId = e.ItemId;
            SelectedUsername = null;        // close user panel when opening story
        }

        private void OnAuthorSelected(object sender, AuthorSelectedEventArgs e)
        {
            SelectedUsername = e.Username;
            // Keep story detail open — user profile appears below it
        }

        // ── API tab → ID list routing ────────────────────────────────────────

        private Task<List<int>> GetIdsForTabAsync(string tab)
        {
            switch (tab)
            {
                case "new": return Service.GetNewStoryIdsAsync();
                case "best": return Service.GetBestStoryIdsAsync();
                case "ask": return Service.GetAskStoryIdsAsync();
                case "show": return Service.GetShowStoryIdsAsync();
                case "jobs": return Service.GetJobStoryIdsAsync();
                default: return Service.GetTopStoryIdsAsync();
            }
        }
    }
}
