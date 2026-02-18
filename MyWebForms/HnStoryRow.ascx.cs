using System;
using System.Text.RegularExpressions;
using System.Web.UI.HtmlControls;
using MyWebForms.Models;

namespace MyWebForms
{
    /// <summary>
    /// Code-behind for HnStoryRow.ascx.
    ///
    /// Score span strategy
    /// -------------------
    /// spanScore    (outer) — carries  data-hn-score-id  for general queries.
    /// spanScoreNum (inner) — carries  data-hn-score-num  so the background JS
    ///                        poller can update ONLY the "NNN pts" text via
    ///                          el.textContent = score + ' pts'
    ///                        without clobbering the ▲ triangle that lives in
    ///                        the outer span as raw HTML text.
    ///
    /// Comment-type items (Active tab)
    /// --------------------------------
    /// The Active tab (/v0/updates.json) returns IDs of recently-changed items
    /// which can be comments, not just stories.  Comments have no Title.
    /// When Item.Type == "comment":
    ///   - The score badge is hidden (comments carry no score).
    ///   - A plain-text snippet of the comment HTML is shown instead of a title.
    ///   - lnkComments fires StorySelected with Item.Parent (the owning story)
    ///     so the detail panel loads the actual story + thread rather than
    ///     trying to render the comment as if it were a story.
    ///   - The domain badge is hidden (comments have no URL).
    ///
    /// Events bubble up to the host page via standard EventHandler delegates.
    /// The host page wires StorySelected and AuthorSelected when it creates
    /// these controls so it can update its detail/user panels.
    ///
    /// Control ID stability
    /// --------------------
    /// The host page assigns  row.ID = "row_" + item.Id  so the UniqueID is
    /// deterministic across postbacks.  This lets ASP.NET's event-routing
    /// mechanism find the correct control when the form is submitted.
    ///
    /// Stub controls (event-routing only)
    /// -----------------------------------
    /// RecreateStoryRowsForEventRouting on the host page creates invisible stub
    /// instances with Item = null.  Those stubs must still be able to fire their
    /// click events — the handler cannot short-circuit on Item == null.
    ///
    /// To support this, the host page sets StubItemId / StubItemBy / StubParentId
    /// directly when creating stubs so the handlers can read the correct values
    /// without a full HackerNewsItem being present.  When Item is not null
    /// (normal render path) these Stub* properties are ignored; Item takes
    /// precedence.
    /// </summary>
    public partial class HnStoryRow : System.Web.UI.UserControl
    {
        // ── Inputs ────────────────────────────────────────────────────────────

        public HackerNewsItem Item { get; set; }
        public int Rank { get; set; }

        /// <summary>
        /// Set by the host page when creating an event-routing stub (Item == null).
        /// Provides the item ID for lnkComments_Click → StorySelected.
        /// </summary>
        public int StubItemId { get; set; }

        /// <summary>
        /// Set by the host page when creating an event-routing stub (Item == null).
        /// Provides the author username for lnkAuthor_Click → AuthorSelected.
        /// </summary>
        public string StubItemBy { get; set; }

        /// <summary>
        /// Set by the host page when creating an event-routing stub for a
        /// comment-type item (Item == null, Item.Type == "comment").
        /// When non-zero, lnkComments fires StorySelected with this parent ID
        /// rather than StubItemId so the detail panel loads the owning story.
        /// </summary>
        public int StubParentId { get; set; }

        // ── Bubbled events ────────────────────────────────────────────────────

        public event EventHandler<StorySelectedEventArgs> StorySelected;
        public event EventHandler<AuthorSelectedEventArgs> AuthorSelected;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Item == null)
            {
                // Stub control created for event routing — render nothing.
                Visible = false;
                return;
            }

            Visible = true;

            litRank.Text = Rank.ToString() + ".";

            bool isComment = Item.IsComment;

            // Score badge — comments have no score, hide the panel entirely.
            pnlScore.Visible = !isComment;
            if (!isComment)
            {
                litScore.Text = Item.Score.ToString() + " pts";
                spanScore.Attributes["data-hn-score-id"] = Item.Id.ToString();
                spanScoreNum.Attributes["data-hn-score-num"] = Item.Id.ToString();
            }

            if (isComment)
            {
                // Comment item: show snippet instead of title.
                lnkTitle.Visible = false;
                pnlDomain.Visible = false;
                pnlCommentSnippet.Visible = true;

                // Strip HTML tags and truncate to ~140 chars for the snippet.
                var raw = Item.Text ?? string.Empty;
                var plain = Regex.Replace(raw, "<[^>]+>", " ");
                plain = System.Web.HttpUtility.HtmlDecode(plain);
                plain = Regex.Replace(plain, @"\s+", " ").Trim();
                if (plain.Length > 140) plain = plain.Substring(0, 137) + "…";
                litCommentSnippet.Text = System.Web.HttpUtility.HtmlEncode(plain);

                // "View thread" link navigates to the parent story.
                lnkComments.Text = "view thread";
            }
            else
            {
                // Normal story / job / poll item.
                lnkTitle.Visible = true;
                pnlDomain.Visible = true;
                pnlCommentSnippet.Visible = false;

                lnkTitle.Text = System.Web.HttpUtility.HtmlEncode(Item.Title ?? "(untitled)");
                lnkTitle.NavigateUrl = Item.DisplayUrl;
                litDomain.Text = System.Web.HttpUtility.HtmlEncode(Item.Domain);
                lnkComments.Text = string.Format("{0} comments", Item.Descendants);
            }

            litTimeAgo.Text = Item.TimeAgo;
            lnkAuthor.Text = System.Web.HttpUtility.HtmlEncode(Item.By ?? "[deleted]");
        }

        // ── Events ────────────────────────────────────────────────────────────

        protected void lnkComments_Click(object sender, EventArgs e)
        {
            int itemId;
            if (Item != null)
            {
                // For a comment item, navigate to the parent story's thread.
                itemId = (Item.IsComment && Item.Parent.HasValue && Item.Parent.Value > 0)
                    ? Item.Parent.Value
                    : Item.Id;
            }
            else
            {
                // Stub — use StubParentId if set (comment item), else StubItemId.
                itemId = (StubParentId > 0) ? StubParentId : StubItemId;
            }

            if (itemId == 0) return;
            StorySelected?.Invoke(this, new StorySelectedEventArgs(itemId));
        }

        protected void lnkAuthor_Click(object sender, EventArgs e)
        {
            string by = Item != null ? Item.By : StubItemBy;
            if (string.IsNullOrEmpty(by)) return;
            AuthorSelected?.Invoke(this, new AuthorSelectedEventArgs(by));
        }
    }
}
