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
    /// runs between Load and PreRender).
    ///
    /// Story rows: cache the current page's story IDs in ViewState ("ShownIds"),
    /// author names ("ShownAuthors"), and parent IDs ("ShownParentIds") — all
    /// parallel lists.  On every postback, RecreateStoryRowsForEventRouting()
    /// runs in Page_Load, adding invisible stub HnStoryRow controls with the
    /// same stable IDs (row_{storyId}).  Stubs set StubItemId, StubItemBy, and
    /// StubParentId so the click handlers fire the correct events.
    ///
    /// Comment-type items (Active tab)
    /// --------------------------------
    /// /v0/updates.json returns recently-changed item IDs which may include
    /// comments.  Comments have no Title, so we track Item.Parent in
    /// ShownParentIds.  When a user clicks "view thread" on a comment row,
    /// lnkComments fires StorySelected with the parent story ID — this loads
    /// the actual story + thread in the detail panel.
    ///
    /// Background refresh
    /// ------------------
    /// A JS IIFE polls HackerNewsRefresh.ashx on a timer and:
    ///   - Updates score spans in the DOM via data-hn-score-num (no postback).
    ///   - Triggers __doPostBack only when listChanged == true.
    ///
    /// New tabs
    /// --------
    /// "active"  — Uses the HN /v0/updates.json endpoint which returns a JSON
    ///             object { items: [...], profiles: [...] }.  The "items" array
    ///             contains IDs of recently updated items (within the last few
    ///             minutes).  This mirrors https://news.ycombinator.com/active.
    ///
    /// "rising"  — Filters the standard "new" story list by
    ///             (comments >= MinComments) OR (score >= MinPoints).
    ///             Because the HN Firebase API does not expose sorted/filtered
    ///             endpoints, we fetch the first RisingCandidates new story IDs,
    ///             load those items concurrently, then apply the filter.
    ///             Thresholds are user-configurable and persisted in ViewState.
    /// </summary>
    public partial class HackerNews : Page
    {
        // ── Constants ──────────────────────────────────────────────────────────

        private const int PageSize = 20;
        private const int MaxCommentDepth = 4;
        private const int AutoRefreshSeconds = 60;

        /// <summary>
        /// How many raw "new" story IDs to fetch and inspect when building the
        /// "rising" tab.  A larger number means better coverage but more API
        /// calls.  Cached for 60 s so repeated postbacks don't re-fetch.
        /// </summary>
        private const int RisingCandidates = 200;

        // ── Service ────────────────────────────────────────────────────────────

        private HackerNewsService _service;

        public HackerNewsService Service
        {
            get { return _service ?? (_service = new HackerNewsService()); }
            set { _service = value; }
        }

        // ── Data loaded by async tasks ─────────────────────────────────────────

        private List<HackerNewsItem> _storyPage;
        private int _totalIds;
        private HackerNewsItem _selectedStory;
        private List<HackerNewsItem> _comments;
        private List<HackerNewsItem> _pollOptions;
        private HackerNewsUser _selectedUser;

        // ── ViewState keys ─────────────────────────────────────────────────────

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
        /// Minimum comment count threshold for the "rising" tab.
        /// Persisted in ViewState so the user's choice survives postbacks.
        /// </summary>
        private int MinComments
        {
            get { return (int)(ViewState["MinComments"] ?? 5); }
            set { ViewState["MinComments"] = value; }
        }

        /// <summary>
        /// Minimum score threshold for the "rising" tab.
        /// Persisted in ViewState so the user's choice survives postbacks.
        /// </summary>
        private int MinPoints
        {
            get { return (int)(ViewState["MinPoints"] ?? 5); }
            set { ViewState["MinPoints"] = value; }
        }

        /// <summary>
        /// IDs of the story items currently shown on the page.
        /// Parallel to ShownAuthors and ShownParentIds — same index = same item.
        /// </summary>
        private List<int> ShownIds
        {
            get { return ViewState["ShownIds"] as List<int> ?? new List<int>(); }
            set { ViewState["ShownIds"] = value; }
        }

        /// <summary>
        /// Author names for each item currently shown on the page.
        /// Parallel to ShownIds — ShownAuthors[i] is the author of ShownIds[i].
        /// Needed so event-routing stubs can fire AuthorSelected with the correct
        /// username even though the stubs carry no HackerNewsItem.
        /// </summary>
        private List<string> ShownAuthors
        {
            get { return ViewState["ShownAuthors"] as List<string> ?? new List<string>(); }
            set { ViewState["ShownAuthors"] = value; }
        }

        /// <summary>
        /// Parent IDs for each item currently shown on the page.
        /// Non-zero only for comment-type items (Active tab).
        /// Parallel to ShownIds — ShownParentIds[i] is the parent of ShownIds[i].
        /// When non-zero, lnkComments fires StorySelected with this parent ID
        /// so the detail panel loads the owning story rather than the comment.
        /// </summary>
        private List<int> ShownParentIds
        {
            get { return ViewState["ShownParentIds"] as List<int> ?? new List<int>(); }
            set { ViewState["ShownParentIds"] = value; }
        }

        /// <summary>
        /// Minimal comment data needed to recreate event-routing stub controls.
        /// Format: [ [commentId, parentId], ... ]
        /// Author names stored separately in ShownCommentAuthors (same index)
        /// because ViewState only reliably round-trips primitive arrays.
        /// </summary>
        private List<int[]> ShownCommentMeta
        {
            get { return ViewState["ShownCommentMeta"] as List<int[]> ?? new List<int[]>(); }
            set { ViewState["ShownCommentMeta"] = value; }
        }

        /// <summary>
        /// Author names parallel to ShownCommentMeta (same index).
        /// </summary>
        private List<string> ShownCommentAuthors
        {
            get { return ViewState["ShownCommentAuthors"] as List<string> ?? new List<string>(); }
            set { ViewState["ShownCommentAuthors"] = value; }
        }

        // ── Page lifecycle ─────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ActiveTab = "top";
                CurrentPage = 1;

                // Sync filter textboxes with ViewState defaults on first load.
                txtMinComments.Text = MinComments.ToString();
                txtMinPoints.Text = MinPoints.ToString();
            }
            else
            {
                // Recreate story-row stubs so ASP.NET can route click events
                // before the async data tasks have completed.
                RecreateStoryRowsForEventRouting();

                // Recreate comment stubs for the same reason.
                if (SelectedItemId > 0)
                    RecreateCommentStubsForEventRouting();
            }

            hfActiveTab.Value = ActiveTab;
            RegisterAsyncTask(new PageAsyncTask(LoadDataAsync));
        }

        /// <summary>
        /// Adds invisible HnStoryRow stubs to phStories using IDs cached in
        /// ShownIds, author names from ShownAuthors, and parent IDs from
        /// ShownParentIds.
        ///
        /// Stubs set StubItemId, StubItemBy, and StubParentId instead of Item,
        /// so the click handlers on HnStoryRow can fire the correct events even
        /// though Item == null (and the control renders nothing — Visible = false).
        /// </summary>
        private void RecreateStoryRowsForEventRouting()
        {
            phStories.Controls.Clear();
            var ids = ShownIds;
            var authors = ShownAuthors;
            var parentIds = ShownParentIds;
            int startRank = (CurrentPage - 1) * PageSize + 1;

            for (int i = 0; i < ids.Count; i++)
            {
                var row = (HnStoryRow)LoadControl("~/HnStoryRow.ascx");
                row.ID = "row_" + ids[i];
                row.Item = null;   // Stub — renders nothing
                row.Rank = startRank + i;
                row.StubItemId = ids[i];
                row.StubItemBy = i < authors.Count ? authors[i] : string.Empty;
                row.StubParentId = i < parentIds.Count ? parentIds[i] : 0;
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
                stub.Item = new HackerNewsItem
                {
                    Id = commentId,
                    By = by,
                    Parent = parentId
                };
                stub.Depth = 0;
                stub.AuthorSelected += OnAuthorSelected;
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

                // If the selected item is itself a comment (clicked "view thread"
                // from the Active tab — should not normally happen because
                // lnkComments_Click resolves to the parent — but guard anyway),
                // navigate up to the parent story.
                if (_selectedStory != null && _selectedStory.IsComment
                    && _selectedStory.Parent.HasValue && _selectedStory.Parent.Value > 0)
                {
                    _selectedStory = await Service.GetItemAsync(_selectedStory.Parent.Value)
                                         .ConfigureAwait(false);
                }

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

            // Show the Rising filter controls only on the "rising" tab.
            pnlRisingFilter.Visible = (ActiveTab == "rising");

            BindStoryList();
            BindDetailPanel();
            BindUserPanel();
            BindPager();
            InjectRefreshScript();
        }

        // ── Tab / filter clicks ────────────────────────────────────────────────

        protected void btnTab_Click(object sender, EventArgs e)
        {
            var btn = (LinkButton)sender;
            ActiveTab = btn.CommandArgument;
            CurrentPage = 1;
        }

        protected void btnApplyFilter_Click(object sender, EventArgs e)
        {
            int mc, mp;
            if (int.TryParse(txtMinComments.Text, out mc) && mc >= 0) MinComments = mc;
            if (int.TryParse(txtMinPoints.Text, out mp) && mp >= 0) MinPoints = mp;
            CurrentPage = 1;
        }

        // ── Pager clicks ───────────────────────────────────────────────────────

        protected void btnPrev_Click(object sender, EventArgs e)
        {
            if (CurrentPage > 1) CurrentPage--;
        }

        protected void btnNext_Click(object sender, EventArgs e)
        {
            var pageCount = (int)Math.Ceiling((double)_totalIds / PageSize);
            if (CurrentPage < pageCount) CurrentPage++;
        }

        // ── Detail / user panel close ──────────────────────────────────────────

        protected void btnCloseDetail_Click(object sender, EventArgs e)
        {
            SelectedItemId = 0;
            ShownCommentMeta = new List<int[]>();
            ShownCommentAuthors = new List<string>();
        }

        protected void btnCloseUser_Click(object sender, EventArgs e)
        {
            SelectedUsername = null;
        }

        // ── Bubble-up event handlers ───────────────────────────────────────────

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

        // ── API tab → ID list routing ──────────────────────────────────────────

        private async Task<List<int>> GetIdsForTabAsync(string tab)
        {
            switch (tab)
            {
                case "new": return await Service.GetNewStoryIdsAsync().ConfigureAwait(false);
                case "best": return await Service.GetBestStoryIdsAsync().ConfigureAwait(false);
                case "ask": return await Service.GetAskStoryIdsAsync().ConfigureAwait(false);
                case "show": return await Service.GetShowStoryIdsAsync().ConfigureAwait(false);
                case "jobs": return await Service.GetJobStoryIdsAsync().ConfigureAwait(false);
                case "active": return await Service.GetActiveItemIdsAsync().ConfigureAwait(false);
                case "rising":
                    return await Service.GetRisingStoryIdsAsync(
                                   MinComments, MinPoints, RisingCandidates).ConfigureAwait(false);
                default: return await Service.GetTopStoryIdsAsync().ConfigureAwait(false);
            }
        }

        // ── Binding helpers ────────────────────────────────────────────────────

        private void BindStoryList()
        {
            phStories.Controls.Clear();

            if (_storyPage == null || _storyPage.Count == 0)
            {
                litMessage.Text = "No stories found. " +
                    "The API may be temporarily unavailable.";
                pnlMessage.Visible = true;
                ShownIds = new List<int>();
                ShownAuthors = new List<string>();
                ShownParentIds = new List<int>();
                return;
            }

            pnlMessage.Visible = false;

            var newShownIds = new List<int>();
            var newShownAuthors = new List<string>();
            var newShownParentIds = new List<int>();

            int startRank = (CurrentPage - 1) * PageSize + 1;
            for (int i = 0; i < _storyPage.Count; i++)
            {
                var item = _storyPage[i];
                newShownIds.Add(item.Id);
                newShownAuthors.Add(item.By ?? string.Empty);
                // Track parent ID for comment-type items so stubs can resolve
                // "view thread" clicks to the correct parent story.
                newShownParentIds.Add(item.IsComment && item.Parent.HasValue
                    ? item.Parent.Value : 0);

                var row = (HnStoryRow)LoadControl("~/HnStoryRow.ascx");
                row.ID = "row_" + item.Id;  // stable ID — must match RecreateStoryRowsForEventRouting
                row.Item = item;
                row.Rank = startRank + i;
                row.StorySelected += OnStorySelected;
                row.AuthorSelected += OnAuthorSelected;
                phStories.Controls.Add(row);
            }

            ShownIds = newShownIds;
            ShownAuthors = newShownAuthors;
            ShownParentIds = newShownParentIds;
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
                litDetailText.Text = _selectedStory.Text;
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

                ShownCommentMeta = new List<int[]>();
                ShownCommentAuthors = new List<string>();
            }
            else
            {
                var newMeta = new List<int[]>();
                var newAuthors = new List<string>();
                RenderCommentTree(_comments, _selectedStory.Id, 0, newMeta, newAuthors);
                ShownCommentMeta = newMeta;
                ShownCommentAuthors = newAuthors;
            }
        }

        /// <summary>
        /// Recursively renders the comment tree into phComments and simultaneously
        /// populates the stub-cache lists so the next postback can recreate invisible
        /// routing stubs without re-fetching data.
        ///
        /// newMeta and newAuthors are always appended together so their indices
        /// stay in sync.  Every HN comment has a Parent set by the API, so
        /// comment.Parent is treated as non-null here; if it ever is null
        /// (malformed API response) we default to 0 to avoid a crash.
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
                ctrl.ID = "cmt_" + comment.Id;   // must match RecreateCommentStubsForEventRouting
                ctrl.Item = comment;
                ctrl.Depth = depth;
                ctrl.AuthorSelected += OnAuthorSelected;
                phComments.Controls.Add(ctrl);

                newMeta.Add(new int[] { comment.Id, comment.Parent ?? 0 });
                newAuthors.Add(comment.By ?? string.Empty);

                if (depth < MaxCommentDepth)
                    RenderCommentTree(comments, comment.Id, depth + 1, newMeta, newAuthors);
            }
        }

        private void BindUserPanel()
        {
            if (string.IsNullOrEmpty(SelectedUsername))
            {
                pnlUserProfile.Visible = false;
                return;
            }

            pnlUserProfile.Visible = true;

            if (_selectedUser == null)
            {
                // User not found or API error — show a friendly message instead
                // of leaving the panel blank.
                ucUserCard.User = null;
                litUserNotFound.Text = string.Format(
                    "Could not load profile for <strong>{0}</strong>. " +
                    "The user may not exist or the HN API may be temporarily unavailable.",
                    System.Web.HttpUtility.HtmlEncode(SelectedUsername));
                pnlUserNotFound.Visible = true;
            }
            else
            {
                ucUserCard.User = _selectedUser;
                pnlUserNotFound.Visible = false;
            }
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
                                        btnTabAsk, btnTabShow, btnTabJobs,
                                        btnTabActive, btnTabRising })
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
                case "active": btnTabActive.CssClass = "nav-link active"; break;
                case "rising": btnTabRising.CssClass = "nav-link active"; break;
            }
        }

        private void InjectRefreshScript()
        {
            // Inject the background auto-refresh IIFE.
            // It polls HackerNewsRefresh.ashx every AutoRefreshSeconds seconds,
            // updates score spans in-place, and triggers __doPostBack only when
            // the story list has actually changed.
            var refreshUrl = ResolveUrl("~/hn-refresh") + "?tab=" + ActiveTab;
            var sb = new StringBuilder();
            sb.Append("(function(){");
            sb.AppendFormat("var TAB='{0}',URL='{1}',INTERVAL={2}000;",
                ActiveTab, refreshUrl, AutoRefreshSeconds);
            sb.Append(@"
function poll(){
    var xhr=new XMLHttpRequest();
    xhr.open('GET',URL+'&_='+Date.now(),true);
    xhr.onload=function(){
        if(xhr.status!==200)return;
        try{
            var d=JSON.parse(xhr.responseText);
            if(d.scores){
                Object.keys(d.scores).forEach(function(id){
                    var el=document.querySelector('[data-hn-score-num=""'+id+'""]');
                    if(el)el.textContent=d.scores[id]+' pts';
                });
            }
            if(d.listChanged){
                var hf=document.getElementById('" + hfActiveTab.ClientID + @"');
                if(hf&&hf.value===TAB)__doPostBack('','');
            }
        }catch(ex){}
    };
    xhr.send();
}
setTimeout(function loop(){poll();setTimeout(loop,INTERVAL);},INTERVAL);
}());");
            ScriptManager.RegisterStartupScript(this, GetType(),
                "hnRefresh", sb.ToString(), true);
        }
    }
}
