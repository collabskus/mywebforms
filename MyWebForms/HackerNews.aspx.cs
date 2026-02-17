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
    /// 1. Page_Load         — restores tab/page state from ViewState.
    ///                        On postback, immediately re-creates the story-row
    ///                        controls from ViewState-cached IDs so that ASP.NET
    ///                        can route LinkButton click events (which happen
    ///                        between Load and PreRender).
    ///                        ALSO re-creates comment stubs for the same reason.
    ///                        Queues the async data load via RegisterAsyncTask.
    /// 2. [Event handlers]  — btnTab_Click, lnkComments, lnkAuthor etc. update
    ///                        ViewState before PreRenderComplete runs.
    /// 3. PreRenderComplete — fires after all async tasks complete; fully binds
    ///                        all panels with fresh API data and injects the
    ///                        background-refresh script.
    ///
    /// Dynamic control event routing — important Web Forms note
    /// ---------------------------------------------------------
    /// Controls added dynamically to a PlaceHolder only receive postback events
    /// if they exist in the control tree BEFORE the event-dispatch phase (which
    /// runs between Page_Load and Page_PreRender).
    ///
    /// Story rows: cache the current page's story IDs in ViewState ("ShownIds").
    /// On every postback, RecreateStoryRowsForEventRouting() runs in Page_Load,
    /// adding invisible stub HnStoryRow controls with the same stable IDs
    /// (row_{storyId}).  These stubs carry the event-handler wiring, so
    /// lnkComments_Click and lnkAuthor_Click route correctly.
    ///
    /// Comments: the same problem applies to HnComment controls inside the
    /// detail panel.  If the user clicks an author link in the comment list,
    /// the postback targets an HnComment control — but those controls are only
    /// added in PreRenderComplete (too late).  Fix: cache a flat list of
    /// (CommentId, AuthorName, ParentId) in ViewState ("ShownComments") and
    /// recreate minimal stub HnComment controls in Page_Load on every postback
    /// when a story is selected.  The stubs have Item set just enough for the
    /// click handler to read Item.By; they are Visible=false so they don't render.
    ///
    /// Background refresh
    /// ------------------
    /// A JS IIFE polls HackerNewsRefresh.ashx on a timer and:
    ///   - Updates score spans in the DOM via data-hn-score-num (no postback).
    ///   - Triggers __doPostBack only when listChanged == true.
    /// </summary>
    public partial class HackerNews : Page
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const int PageSize = 20;
        private const int MaxCommentDepth = 4;
        private const int AutoRefreshSeconds = 60;

        // ── Service ──────────────────────────────────────────────────────────

        private HackerNewsService _service;

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

        /// <summary>
        /// IDs of the story items currently shown on the page.
        /// Stored so we can recreate event-routing stubs on the next postback.
        /// </summary>
        private List<int> ShownIds
        {
            get { return ViewState["ShownIds"] as List<int> ?? new List<int>(); }
            set { ViewState["ShownIds"] = value; }
        }

        /// <summary>
        /// Minimal comment data needed to recreate event-routing stub controls.
        /// Each entry is [commentId, authorName, parentId].
        /// Stored as a List of int-arrays so it survives ViewState serialisation
        /// without needing a custom serialisable type.
        /// Format: [ [id, parentId], ... ] — authorName stored separately in
        /// ShownCommentAuthors because ViewState only reliably round-trips
        /// primitives and simple arrays.
        /// </summary>
        private List<int[]> ShownCommentMeta
        {
            get { return ViewState["ShownCommentMeta"] as List<int[]> ?? new List<int[]>(); }
            set { ViewState["ShownCommentMeta"] = value; }
        }

        /// <summary>
        /// Author names parallel to ShownCommentMeta (same index).
        /// Stored separately because ViewState doesn't reliably round-trip
        /// mixed-type arrays.
        /// </summary>
        private List<string> ShownCommentAuthors
        {
            get { return ViewState["ShownCommentAuthors"] as List<string> ?? new List<string>(); }
            set { ViewState["ShownCommentAuthors"] = value; }
        }

        // ── Page lifecycle ───────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ActiveTab = "top";
                CurrentPage = 1;
            }
            else
            {
                // Recreate story-row stubs so ASP.NET can route click events
                // before the async data tasks have completed.
                RecreateStoryRowsForEventRouting();

                // Recreate comment stubs for the same reason.
                // Without this, clicking an author link inside the comment list
                // causes a runtime error because the HnComment control does not
                // yet exist in the control tree when the event-dispatch phase runs.
                if (SelectedItemId > 0)
                {
                    RecreateCommentStubsForEventRouting();
                }
            }

            hfActiveTab.Value = ActiveTab;
            RegisterAsyncTask(new PageAsyncTask(LoadDataAsync));
        }

        /// <summary>
        /// Adds invisible HnStoryRow stubs to phStories using only the IDs
        /// cached in ShownIds.  The stubs have no Item data (Item == null →
        /// Visible = false), but their UniqueIDs match those produced by
        /// BindStoryList, giving ASP.NET a valid event target for each row's
        /// LinkButtons.
        /// </summary>
        private void RecreateStoryRowsForEventRouting()
        {
            phStories.Controls.Clear();
            var ids = ShownIds;
            int startRank = (CurrentPage - 1) * PageSize + 1;
            for (int i = 0; i < ids.Count; i++)
            {
                var row = (HnStoryRow)LoadControl("~/HnStoryRow.ascx");
                row.ID = "row_" + ids[i];
                row.Item = null;  // Stub — no data, will be invisible
                row.Rank = startRank + i;
                row.StorySelected += OnStorySelected;
                row.AuthorSelected += OnAuthorSelected;
                phStories.Controls.Add(row);
            }
        }

        /// <summary>
        /// Adds invisible HnComment stubs to phComments using data cached in
        /// ShownCommentMeta / ShownCommentAuthors.  Each stub has a minimal
        /// HackerNewsItem set so lnkAuthor_Click can read Item.By.
        /// The IDs must exactly match those assigned in RenderCommentTree so
        /// ASP.NET's event-routing finds the correct control.
        /// </summary>
        private void RecreateCommentStubsForEventRouting()
        {
            phComments.Controls.Clear();
            var meta = ShownCommentMeta;
            var authors = ShownCommentAuthors;

            for (int i = 0; i < meta.Count; i++)
            {
                int commentId = meta[i][0];
                int parentId = meta[i][1];
                string by = i < authors.Count ? authors[i] : string.Empty;

                var stub = (HnComment)LoadControl("~/HnComment.ascx");
                stub.ID = "cmt_" + commentId;   // must match RenderCommentTree
                stub.Item = new HackerNewsItem     // minimal — just enough for the event handler
                {
                    Id = commentId,
                    By = by,
                    Parent = parentId
                };
                stub.Depth = 0;           // depth doesn't matter for invisible stubs
                stub.AuthorSelected += OnAuthorSelected;
                // Keep it invisible so it doesn't render duplicate HTML.
                stub.Visible = false;
                phComments.Controls.Add(stub);
            }
        }

        private async Task LoadDataAsync()
        {
            var ids = await GetIdsForTabAsync(ActiveTab).ConfigureAwait(false);
            _totalIds = ids.Count;
            _storyPage = await Service.GetItemPageAsync(ids, CurrentPage, PageSize)
                             .ConfigureAwait(false);

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

            if (!string.IsNullOrEmpty(SelectedUsername))
            {
                _selectedUser = await Service.GetUserAsync(SelectedUsername)
                                    .ConfigureAwait(false);
            }
        }

        protected override void OnPreRenderComplete(EventArgs e)
        {
            base.OnPreRenderComplete(e);

            HighlightActiveTab();

            BindStoryList();
            BindDetailPanel();
            BindUserPanel();
            BindPager();

            lblLiveBadge.Visible = true;

            if (SelectedItemId == 0 && string.IsNullOrEmpty(SelectedUsername))
            {
                InjectAutoRefreshScript();
            }
        }

        // ── Auto-refresh script ──────────────────────────────────────────────

        private void InjectAutoRefreshScript()
        {
            var shownIds = new List<string>();
            if (_storyPage != null)
            {
                foreach (var s in _storyPage)
                    shownIds.Add(s.Id.ToString());
            }

            var activeTab = ActiveTab;
            var countdownClientId = lblRefreshCountdown.ClientID;
            var seconds = AutoRefreshSeconds;
            var handlerUrl = ResolveUrl("~/hn-refresh");

            string currentTabBtnUniqueId;
            switch (activeTab)
            {
                case "new": currentTabBtnUniqueId = btnTabNew.UniqueID; break;
                case "best": currentTabBtnUniqueId = btnTabBest.UniqueID; break;
                case "ask": currentTabBtnUniqueId = btnTabAsk.UniqueID; break;
                case "show": currentTabBtnUniqueId = btnTabShow.UniqueID; break;
                case "jobs": currentTabBtnUniqueId = btnTabJobs.UniqueID; break;
                default: currentTabBtnUniqueId = btnTabTop.UniqueID; break;
            }

            var shownIdsJoined = string.Join(",", shownIds.ToArray());

            var sb = new StringBuilder();
            sb.AppendLine("(function () {");
            sb.AppendLine("    var remaining = " + seconds + ";");
            sb.AppendLine("    var timer;");
            sb.AppendLine("    var countdownEl = document.getElementById('" + countdownClientId + "');");
            sb.AppendLine("");
            sb.AppendLine("    function updateCountdown() {");
            sb.AppendLine("        if (countdownEl) {");
            sb.AppendLine("            countdownEl.style.display = '';");
            sb.AppendLine("            countdownEl.textContent = 'Refreshing in ' + remaining + 's';");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function poll() {");
            sb.AppendLine("        var xhr = new XMLHttpRequest();");
            sb.AppendLine("        xhr.open('GET', '" + handlerUrl + "?tab=" + activeTab + "&ids=" + shownIdsJoined + "', true);");
            sb.AppendLine("        xhr.onreadystatechange = function () {");
            sb.AppendLine("            if (xhr.readyState !== 4) return;");
            sb.AppendLine("            if (xhr.status === 200) {");
            sb.AppendLine("                try {");
            sb.AppendLine("                    var result = JSON.parse(xhr.responseText);");
            sb.AppendLine("                    if (result.listChanged) {");
            sb.AppendLine("                        __doPostBack('" + currentTabBtnUniqueId + "', '');");
            sb.AppendLine("                        return;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    if (result.scores) {");
            sb.AppendLine("                        for (var id in result.scores) {");
            sb.AppendLine("                            var span = document.querySelector('[data-hn-score-num=\"' + id + '\"]');");
            sb.AppendLine("                            if (span) span.textContent = result.scores[id] + ' pts';");
            sb.AppendLine("                        }");
            sb.AppendLine("                    }");
            sb.AppendLine("                } catch (ex) { }");
            sb.AppendLine("            }");
            sb.AppendLine("            remaining = " + seconds + ";");
            sb.AppendLine("            updateCountdown();");
            sb.AppendLine("            timer = setInterval(tick, 1000);");
            sb.AppendLine("        };");
            sb.AppendLine("        xhr.send();");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    function tick() {");
            sb.AppendLine("        remaining--;");
            sb.AppendLine("        if (remaining <= 0) {");
            sb.AppendLine("            clearInterval(timer);");
            sb.AppendLine("            poll();");
            sb.AppendLine("        } else {");
            sb.AppendLine("            updateCountdown();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    updateCountdown();");
            sb.AppendLine("    timer = setInterval(tick, 1000);");
            sb.AppendLine("})();");

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

        // ── Detail / user panel close ─────────────────────────────────────────

        protected void btnCloseDetail_Click(object sender, EventArgs e)
        {
            SelectedItemId = 0;
            // Clear comment stubs too so stale data doesn't linger in ViewState.
            ShownCommentMeta = new List<int[]>();
            ShownCommentAuthors = new List<string>();
        }

        protected void btnCloseUser_Click(object sender, EventArgs e)
        {
            SelectedUsername = null;
        }

        // ── Binding helpers ──────────────────────────────────────────────────

        private void BindStoryList()
        {
            phStories.Controls.Clear();

            if (_storyPage == null || _storyPage.Count == 0)
            {
                litMessage.Text = "No stories found. " +
                    "The API may be temporarily unavailable.";
                pnlMessage.Visible = true;
                ShownIds = new List<int>();
                return;
            }

            pnlMessage.Visible = false;

            // Cache IDs so the next postback can create event-routing stubs.
            var newShownIds = new List<int>();
            foreach (var s in _storyPage)
                newShownIds.Add(s.Id);
            ShownIds = newShownIds;

            int startRank = (CurrentPage - 1) * PageSize + 1;
            for (int i = 0; i < _storyPage.Count; i++)
            {
                var row = (HnStoryRow)LoadControl("~/HnStoryRow.ascx");
                row.ID = "row_" + _storyPage[i].Id;  // Stable ID — must match RecreateStoryRowsForEventRouting
                row.Item = _storyPage[i];
                row.Rank = startRank + i;
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
                "{0} points by <strong>{1}</strong> &nbsp;|&nbsp; {2} &nbsp;|&nbsp; {3} comments",
                _selectedStory.Score,
                System.Web.HttpUtility.HtmlEncode(_selectedStory.By ?? "[deleted]"),
                _selectedStory.TimeAgo,
                _selectedStory.Descendants);

            if (!string.IsNullOrEmpty(_selectedStory.Text))
            {
                litDetailText.Text = _selectedStory.Text; // HN returns trusted HTML
                pnlDetailText.Visible = true;
            }
            else
            {
                pnlDetailText.Visible = false;
            }

            if (_pollOptions != null && _pollOptions.Count > 0)
            {
                phPollOptions.Controls.Clear();
                foreach (var opt in _pollOptions)
                {
                    var div = new HtmlGenericControl("div");
                    div.Attributes["class"] = "hn-poll-option";
                    div.InnerHtml = string.Format(
                        "<strong>{0}</strong> &mdash; {1} pts",
                        System.Web.HttpUtility.HtmlEncode(opt.Title ?? string.Empty),
                        opt.Score);
                    phPollOptions.Controls.Add(div);
                }
                pnlPollOptions.Visible = true;
            }
            else
            {
                pnlPollOptions.Visible = false;
            }

            litDetailCommentCount.Text = _selectedStory.Descendants.ToString();
            phComments.Controls.Clear();
            pnlCommentLoading.Visible = false;

            if (_comments == null || _comments.Count == 0)
            {
                var noComments = new HtmlGenericControl("p");
                noComments.Attributes["class"] = "text-muted small";
                noComments.InnerText = "No comments yet.";
                phComments.Controls.Add(noComments);

                // No comments to stub — clear cached comment meta.
                ShownCommentMeta = new List<int[]>();
                ShownCommentAuthors = new List<string>();
            }
            else
            {
                // Reset the comment-stub caches before rebuilding.
                var newMeta = new List<int[]>();
                var newAuthors = new List<string>();

                RenderCommentTree(_comments, _selectedStory.Id, 0, newMeta, newAuthors);

                // Persist so the next postback can recreate stubs.
                ShownCommentMeta = newMeta;
                ShownCommentAuthors = newAuthors;
            }
        }

        /// <summary>
        /// Recursively renders the comment tree into phComments and simultaneously
        /// populates the stub-cache lists (newMeta / newAuthors) so that the next
        /// postback can recreate invisible routing stubs without re-fetching data.
        /// </summary>
        private void RenderCommentTree(
            List<HackerNewsItem> comments,
            int parentId,
            int depth,
            List<int[]> newMeta,
            List<string> newAuthors)
        {
            foreach (var comment in comments)
            {
                if (comment.Parent != parentId) continue;

                var ctrl = (HnComment)LoadControl("~/HnComment.ascx");
                ctrl.ID = "cmt_" + comment.Id;  // must match RecreateCommentStubsForEventRouting
                ctrl.Item = comment;
                ctrl.Depth = depth;
                ctrl.AuthorSelected += OnAuthorSelected;
                phComments.Controls.Add(ctrl);

                // Cache the minimal data needed to recreate this stub on postback.
                newMeta.Add(new int[] { comment.Id, comment.Parent });
                newAuthors.Add(comment.By ?? string.Empty);

                if (depth < MaxCommentDepth)
                    RenderCommentTree(comments, comment.Id, depth + 1, newMeta, newAuthors);
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

        // ── Bubble-up event handlers ─────────────────────────────────────────

        private void OnStorySelected(object sender, StorySelectedEventArgs e)
        {
            SelectedItemId = e.ItemId;
            SelectedUsername = null;
        }

        private void OnAuthorSelected(object sender, AuthorSelectedEventArgs e)
        {
            SelectedUsername = e.Username;
            SelectedItemId = 0;
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
