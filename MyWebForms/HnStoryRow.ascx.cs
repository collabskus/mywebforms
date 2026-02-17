using System;
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
    /// Events bubble up to the host page via standard EventHandler delegates.
    /// The host page wires StorySelected and AuthorSelected when it creates
    /// these controls so it can update its detail/user panels.
    ///
    /// Control ID stability
    /// --------------------
    /// The host page assigns  row.ID = "row_" + item.Id  so the UniqueID is
    /// deterministic across postbacks.  This lets ASP.NET's event-routing
    /// mechanism find the correct control when the form is submitted.
    /// </summary>
    public partial class HnStoryRow : System.Web.UI.UserControl
    {
        // ── Inputs ───────────────────────────────────────────────────────────

        public HackerNewsItem Item { get; set; }
        public int Rank { get; set; }

        // ── Bubbled events ───────────────────────────────────────────────────

        public event EventHandler<StorySelectedEventArgs> StorySelected;
        public event EventHandler<AuthorSelectedEventArgs> AuthorSelected;

        // ── Lifecycle ────────────────────────────────────────────────────────

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
            litScore.Text = Item.Score.ToString() + " pts";

            // Outer span: ID-based lookup (kept for backward compat / CSS hooks).
            spanScore.Attributes["data-hn-score-id"] = Item.Id.ToString();

            // Inner span: targeted by the JS poller so it can update the score
            // number text WITHOUT replacing the ▲ triangle in the outer span.
            spanScoreNum.Attributes["data-hn-score-num"] = Item.Id.ToString();

            lnkTitle.Text = System.Web.HttpUtility.HtmlEncode(Item.Title ?? "(untitled)");
            lnkTitle.NavigateUrl = Item.DisplayUrl;
            litDomain.Text = System.Web.HttpUtility.HtmlEncode(Item.Domain);
            litTimeAgo.Text = Item.TimeAgo;

            lnkAuthor.Text = System.Web.HttpUtility.HtmlEncode(Item.By ?? "[deleted]");
            lnkComments.Text = string.Format("{0} comments", Item.Descendants);
        }

        // ── Events ───────────────────────────────────────────────────────────

        protected void lnkComments_Click(object sender, EventArgs e)
        {
            if (Item == null) return;
            StorySelected?.Invoke(this, new StorySelectedEventArgs(Item.Id));
        }

        protected void lnkAuthor_Click(object sender, EventArgs e)
        {
            if (Item == null || string.IsNullOrEmpty(Item.By)) return;
            AuthorSelected?.Invoke(this, new AuthorSelectedEventArgs(Item.By));
        }
    }
}
