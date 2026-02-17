using System;
using MyWebForms.Models;

namespace MyWebForms
{
    /// <summary>
    /// Code-behind for HnStoryRow.ascx.
    ///
    /// Events bubble up to the host page via standard EventHandler delegates.
    /// The host page wires these up when it dynamically creates or databinds
    /// the controls, then handles them to update the detail/user panels.
    /// </summary>
    public partial class HnStoryRow : System.Web.UI.UserControl
    {
        // ── Inputs ───────────────────────────────────────────────────────────

        public HackerNewsItem Item { get; set; }
        public int Rank { get; set; }

        // ── Bubbled events ───────────────────────────────────────────────────

        /// <summary>Fired when the user clicks the comment count link.</summary>
        public event EventHandler<StorySelectedEventArgs> StorySelected;

        /// <summary>Fired when the user clicks the author username link.</summary>
        public event EventHandler<AuthorSelectedEventArgs> AuthorSelected;

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Item == null) return;

            litRank.Text = Rank > 0 ? Rank.ToString() + "." : string.Empty;
            litScore.Text = Item.Score.ToString();
            litDomain.Text = string.IsNullOrEmpty(Item.Domain) ? "self" : Item.Domain;
            litTimeAgo.Text = Item.TimeAgo;

            lnkTitle.Text = System.Web.HttpUtility.HtmlEncode(Item.Title ?? "(untitled)");
            lnkTitle.NavigateUrl = Item.DisplayUrl;

            lnkAuthor.Text = System.Web.HttpUtility.HtmlEncode(Item.By ?? "unknown");

            var commentCount = Item.Descendants;
            lnkComments.Text = commentCount == 0
                ? "discuss"
                : commentCount + " comment" + (commentCount == 1 ? "" : "s");
        }

        protected void lnkComments_Click(object sender, EventArgs e)
        {
            if (Item == null) return;
            StorySelected?.Invoke(this, new StorySelectedEventArgs(Item.Id));
        }

        protected void lnkAuthor_Click(object sender, EventArgs e)
        {
            if (Item == null) return;
            AuthorSelected?.Invoke(this, new AuthorSelectedEventArgs(Item.By));
        }
    }

    // ── Event argument types ─────────────────────────────────────────────────

    public sealed class StorySelectedEventArgs : EventArgs
    {
        public int ItemId { get; private set; }
        public StorySelectedEventArgs(int itemId) { ItemId = itemId; }
    }

    public sealed class AuthorSelectedEventArgs : EventArgs
    {
        public string Username { get; private set; }
        public AuthorSelectedEventArgs(string username) { Username = username; }
    }
}
