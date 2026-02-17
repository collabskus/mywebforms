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
    /// Solution: cache the current page's story IDs in ViewState ("ShownIds").
    /// On every postback, RecreateStoryRowsForEventRouting() runs in Page_Load,
    /// adding invisible stub HnStoryRow controls with the same stable IDs
    /// (row_{storyId}).  These stubs carry the event-handler wiring, so
    /// lnkComments_Click and lnkAuthor_Click route correctly.
    /// PreRenderComplete then replaces them with fully-populated controls.
    ///
    /// Background refresh
    /// ------------------
    /// A JS IIFE polls HackerNewsRefresh.ashx on a timer and:
    ///   - Updates score spans in the DOM via data-hn-score-num (no postback).
    ///   - Triggers __doPostBack only when listChanged == true.
    ///
    /// Timer restart fix
    /// -----------------
    /// The previous code used  arguments.callee.caller  inside a setTimeout
    /// callback — unreliable and broken in strict mode.
    /// Fix: a named  tick()  function referenced directly by setInterval.
    ///
    /// Score update fix
    /// ----------------
    /// The previous code set  spanScore.textContent  which erased the ▲ triangle
    /// (plain HTML text in the outer span).
    /// Fix: a dedicated inner span carries  data-hn-score-num  and the JS only
    /// updates that inner span's textContent.
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

            // ── JavaScript ────────────────────────────────────────────────────
            // Fix 1 – Timer restart:
            //   Old code used  arguments.callee.caller  inside a setTimeout
            //   callback.  arguments.callee refers to the anonymous setTimeout
            //   callback, not the setInterval tick — so .caller is null/wrong.
            //   Fix: name the tick function and reference it by name in both
            //   setInterval calls.
            //
            // Fix 2 – Score update:
            //   Old code: els[i].textContent = score + ' pts'  where els was
            //   querySelectorAll('[data-hn-score-id="N"]') — the outer badge
            //   span.  Setting textContent on the outer span deletes the ▲
            //   triangle (which is a text node, not a child element).
            //   Fix: the JS now targets  data-hn-score-num  on a dedicated inner
            //   span that wraps only the "NNN pts" text.  The ▲ stays untouched.

            var sb = new StringBuilder();
            sb.AppendLine("(function () {");
            sb.AppendLine("    'use strict';");
            sb.AppendLine("    var remaining = " + seconds + ";");
            sb.AppendLine("    var timer;");
            sb.AppendLine("    var lbl = document.getElementById('" + countdownClientId + "');");
            sb.AppendLine("    var handlerUrl = '" + handlerUrl + "';");
            sb.AppendLine("    var tab = '" + activeTab + "';");
            sb.AppendLine("    var postbackTarget = '" + currentTabBtnUniqueId + "';");
            sb.AppendLine("    var shownIds = '" + shownIdsJoined + "';");
            sb.AppendLine("");
            sb.AppendLine("    function updateCountdown() {");
            sb.AppendLine("        if (lbl) { lbl.textContent = 'next check in ' + remaining + 's'; }");
            sb.AppendLine("    }");
            sb.AppendLine("");
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
            sb.AppendLine("                    if (data.scores) {");
            sb.AppendLine("                        // Target the inner score-num span only — preserves the ▲ triangle.");
            sb.AppendLine("                        Object.keys(data.scores).forEach(function (id) {");
            sb.AppendLine("                            var els = document.querySelectorAll('[data-hn-score-num=\"' + id + '\"]');");
            sb.AppendLine("                            for (var i = 0; i < els.length; i++) {");
            sb.AppendLine("                                els[i].textContent = data.scores[id] + ' pts';");
            sb.AppendLine("                            }");
            sb.AppendLine("                        });");
            sb.AppendLine("                    }");
            sb.AppendLine("                    if (data.listChanged) {");
            sb.AppendLine("                        if (lbl) { lbl.textContent = 'new stories \u2014 reloading\u2026'; }");
            sb.AppendLine("                        __doPostBack(postbackTarget, '');");
            sb.AppendLine("                        return;  // Don't restart timer — page is reloading.");
            sb.AppendLine("                    }");
            sb.AppendLine("                } catch (ex) { /* ignore parse errors */ }");
            sb.AppendLine("            }");
            sb.AppendLine("            // XHR done — reset countdown and restart the interval.");
            sb.AppendLine("            remaining = " + seconds + ";");
            sb.AppendLine("            updateCountdown();");
            sb.AppendLine("            timer = setInterval(tick, 1000);  // Named reference — no arguments.callee needed.");
            sb.AppendLine("        };");
            sb.AppendLine("        xhr.send();");
            sb.AppendLine("    }");
            sb.AppendLine("");
            sb.AppendLine("    // Named tick function so poll() can reference it by name when restarting.");
            sb.AppendLine("    function tick() {");
            sb.AppendLine("        remaining--;");
            sb.AppendLine("        if (remaining <= 0) {");
            sb.AppendLine("            clearInterval(timer);");
            sb.AppendLine("            poll();");
            sb.AppendLine("            // poll()'s onreadystatechange callback restarts the interval.");
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
                litMessage.Text = "No stories found. The API may be temporarily unavailable.";
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
