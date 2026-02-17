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
    /// 1. Page_Load         — restores tab/page state from ViewState; queues
    ///                        the async data load via RegisterAsyncTask.
    /// 2. [Event handlers]  — btnTab_Click etc. update ViewState before
    ///                        PreRenderComplete runs.
    /// 3. PreRenderComplete — fires after all async tasks complete; calls
    ///                        HighlightActiveTab (so the active tab is always
    ///                        correct after event handlers have run), binds
    ///                        all panels, and injects the background-refresh
    ///                        script.
    ///
    /// Background refresh (all tabs)
    /// ------------------------------
    /// Instead of a blunt "full postback after N seconds", we inject a
    /// JavaScript poller that calls HackerNewsRefresh.ashx every
    /// AutoRefreshSeconds seconds.  The handler returns:
    ///   { listChanged: bool, scores: { id: score, ... } }
    ///
    /// The script:
    ///   - Updates score spans in the DOM immediately (no postback needed).
    ///   - Only does a __doPostBack if listChanged == true (new stories in
    ///     the list that the server knows about but the client does not have).
    ///
    /// This is a demonstration of the HttpHandler + client polling pattern,
    /// which is the .NET 4.8 / Web Forms idiom for lightweight background
    /// updates without a full UpdatePanel.
    ///
    /// Why not UpdatePanel here?
    ///   UpdatePanel wraps a ScriptManager partial-render postback — perfectly
    ///   valid but brings in the MS Ajax infrastructure.  The polling approach
    ///   is deliberately lighter and exercises the IHttpAsyncHandler pattern
    ///   from the learning checklist.
    /// </summary>
    public partial class HackerNews : Page
    {
        // ── Constants ────────────────────────────────────────────────────────

        private const int PageSize = 20;
        private const int MaxCommentDepth = 4;

        /// <summary>
        /// How many seconds between background refresh polls.
        /// 60 s is respectful of the HN Firebase API rate limits.
        /// </summary>
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

        // ── Page lifecycle ───────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ActiveTab = "top";
                CurrentPage = 1;
            }

            // Expose the active tab to JavaScript via a hidden field.
            hfActiveTab.Value = ActiveTab;

            // NOTE: HighlightActiveTab() is intentionally NOT called here.
            // It is called in OnPreRenderComplete so that tab click event
            // handlers (which run after Page_Load but before PreRender) have
            // already updated ActiveTab in ViewState before we read it.
            // Calling it here would mean the highlight reflects the *previous*
            // tab on the very first click.

            RegisterAsyncTask(new PageAsyncTask(LoadDataAsync));
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

            // Run here (not in Page_Load) so tab-click event handlers have
            // already updated ActiveTab before we apply the highlight.
            HighlightActiveTab();

            BindStoryList();
            BindDetailPanel();
            BindUserPanel();
            BindPager();

            // Show the LIVE badge on all tabs (it indicates live polling).
            lblLiveBadge.Visible = true;

            // Inject the background-refresh script on all tabs when no detail
            // panel is open (avoid interrupting reading).
            if (SelectedItemId == 0 && string.IsNullOrEmpty(SelectedUsername))
            {
                InjectAutoRefreshScript();
            }
            else
            {
                lblRefreshCountdown.Visible = false;
            }
        }

        // ── Auto-refresh (background polling) ────────────────────────────────

        /// <summary>
        /// Injects a JavaScript poller that calls HackerNewsRefresh.ashx
        /// every AutoRefreshSeconds seconds.
        ///
        /// On each poll:
        ///   1. Score spans in the DOM are updated in-place — no postback.
        ///   2. If the server reports the story list has changed (new IDs),
        ///      we do a __doPostBack to reload the page with fresh data.
        ///
        /// The countdown label gives the user a live "next check in N s" cue.
        ///
        /// Score span convention (set in HnStoryRow.ascx):
        ///   Each score element must have  data-hn-score-id="{itemId}"
        ///   so the JS can find it by story ID.
        ///
        /// C# 7.3 compatible — no $@ interpolated verbatim strings.
        /// </summary>
        private void InjectAutoRefreshScript()
        {
            lblRefreshCountdown.Visible = true;
            lblRefreshCountdown.Text = string.Format(
                "next check in {0}s", AutoRefreshSeconds);

            // Collect the IDs currently shown so we can send them to the handler.
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
            // Build the current tab's postback target (whatever tab we are on).
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
            var shownIdsJson = string.Join(",", shownIds.ToArray());

            var sb = new StringBuilder();
            sb.AppendLine("(function () {");
            sb.AppendLine("    var remaining = " + seconds + ";");
            sb.AppendLine("    var lbl = document.getElementById('" + countdownClientId + "');");
            sb.AppendLine("    var handlerUrl = '" + handlerUrl + "';");
            sb.AppendLine("    var tab = '" + activeTab + "';");
            sb.AppendLine("    var postbackTarget = '" + currentTabBtnUniqueId + "';");
            sb.AppendLine("    var shownIds = '" + shownIdsJson + "';");
            sb.AppendLine("");
            sb.AppendLine("    function updateCountdown() {");
            sb.AppendLine("        if (lbl) { lbl.textContent = 'next check in ' + remaining + 's'; }");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Poll the lightweight handler — no full postback unless needed.");
            sb.AppendLine("    function poll() {");
            sb.AppendLine("        if (lbl) { lbl.textContent = 'checking\u2026'; }");
            sb.AppendLine("        var url = handlerUrl + '?tab=' + tab + '&ids=' + shownIds;");
            sb.AppendLine("        var xhr = new XMLHttpRequest();");
            sb.AppendLine("        xhr.open('GET', url, true);");
            sb.AppendLine("        xhr.onreadystatechange = function () {");
            sb.AppendLine("            if (xhr.readyState !== 4) return;");
            sb.AppendLine("            if (xhr.status === 200) {");
            sb.AppendLine("                try {");
            sb.AppendLine("                    var data = JSON.parse(xhr.responseText);");
            sb.AppendLine("                    // Update scores in the DOM without a postback.");
            sb.AppendLine("                    if (data.scores) {");
            sb.AppendLine("                        Object.keys(data.scores).forEach(function (id) {");
            sb.AppendLine("                            var els = document.querySelectorAll('[data-hn-score-id=\"' + id + '\"]');");
            sb.AppendLine("                            for (var i = 0; i < els.length; i++) {");
            sb.AppendLine("                                els[i].textContent = data.scores[id] + ' pts';");
            sb.AppendLine("                            }");
            sb.AppendLine("                        });");
            sb.AppendLine("                    }");
            sb.AppendLine("                    // Only do a full postback if the story list changed.");
            sb.AppendLine("                    if (data.listChanged) {");
            sb.AppendLine("                        if (lbl) { lbl.textContent = 'new stories — reloading\u2026'; }");
            sb.AppendLine("                        __doPostBack(postbackTarget, '');");
            sb.AppendLine("                        return;");
            sb.AppendLine("                    }");
            sb.AppendLine("                } catch (ex) { /* ignore parse errors */ }");
            sb.AppendLine("            }");
            sb.AppendLine("            // Reset countdown for next poll.");
            sb.AppendLine("            remaining = " + seconds + ";");
            sb.AppendLine("            updateCountdown();");
            sb.AppendLine("        };");
            sb.AppendLine("        xhr.send();");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Tick the countdown every second; poll when it hits zero.");
            sb.AppendLine("    var timer = setInterval(function () {");
            sb.AppendLine("        remaining--;");
            sb.AppendLine("        if (remaining <= 0) {");
            sb.AppendLine("            clearInterval(timer);");
            sb.AppendLine("            poll();");
            sb.AppendLine("            // After the poll resets 'remaining', restart the ticker.");
            sb.AppendLine("            // We restart it inside the XHR callback above by re-invoking");
            sb.AppendLine("            // the whole IIFE via a recursive call — but to keep things");
            sb.AppendLine("            // simple and avoid a closure loop, we just restart the interval");
            sb.AppendLine("            // after a short grace period to let the XHR finish.");
            sb.AppendLine("            setTimeout(function () {");
            sb.AppendLine("                remaining = " + seconds + ";");
            sb.AppendLine("                timer = setInterval(arguments.callee.caller, 1000);");
            sb.AppendLine("            }, 2000);");
            sb.AppendLine("        } else {");
            sb.AppendLine("            updateCountdown();");
            sb.AppendLine("        }");
            sb.AppendLine("    }, 1000);");
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

                row.StorySelected += OnStorySelected;
                row.AuthorSelected += OnAuthorSelected;

                phStories.Controls.Add(row);
            }
        }

        private void BindDetailPanel()
        {
            if (SelectedItemId == 0 || _selectedStory == null)
            {
                pnlDetail.Visible = false;
                return;
            }

            pnlDetail.Visible = true;
            ucStoryDetail.Item = _selectedStory;
            ucStoryDetail.PollOptions = _pollOptions;
            ucStoryDetail.AuthorSelected += OnAuthorSelected;

            phComments.Controls.Clear();

            if (_comments == null || _comments.Count == 0)
            {
                var noComments = new HtmlGenericControl("p");
                noComments.Attributes["class"] = "text-muted small";
                noComments.InnerText = "No comments yet.";
                phComments.Controls.Add(noComments);
            }
            else
            {
                RenderCommentTree(_comments, _selectedStory.Id, 0);
            }
        }

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

                if (depth < MaxCommentDepth)
                    RenderCommentTree(comments, comment.Id, depth + 1);
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
