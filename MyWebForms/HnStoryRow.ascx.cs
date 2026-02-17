using System;
using System.Web.UI.HtmlControls;
using MyWebForms.Models;

namespace MyWebForms
{
    /// <summary>
    /// Code-behind for HnStoryRow.ascx.
    ///
    /// Events bubble up to the host page via standard EventHandler delegates.
    /// The host page wires these up when it dynamically creates or databinds
    /// the controls, then handles them to update the detail/user panels.
    ///
    /// Score span and data-hn-score-id
    /// ---------------------------------
    /// spanScore is a runat="server" HtmlGenericControl (<span>).  In
    /// Page_Load we stamp its data-hn-score-id attribute with the item's ID.
    /// The background JS poller in HackerNews.aspx.cs uses
    ///   querySelectorAll('[data-hn-score-id="N"]')
    /// to find this element and update the text content without a postback.
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
                Visible = false;
                return;
            }

            litRank.Text = Rank.ToString() + ".";
            litScore.Text = Item.Score.ToString() + " pts";

            // Stamp the score span with the item ID so the JS poller can
            // find it by data attribute and update the score without a postback.
            spanScore.Attributes["data-hn-score-id"] = Item.Id.ToString();

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
