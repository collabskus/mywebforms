using System;
using MyWebForms.Models;

namespace MyWebForms
{
    /// <summary>
    /// Code-behind for HnComment.ascx.
    /// Renders a single flat comment; the host page is responsible for
    /// rendering the full tree by creating one control per comment and
    /// setting the Depth property to control visual indentation.
    /// </summary>
    public partial class HnComment : System.Web.UI.UserControl
    {
        // ── Inputs ───────────────────────────────────────────────────────────

        public HackerNewsItem Item { get; set; }

        /// <summary>
        /// Visual nesting depth (0 = top-level).
        /// Each depth level adds 20 px of left margin.
        /// </summary>
        public int Depth { get; set; }

        // ── Derived ──────────────────────────────────────────────────────────

        protected int DepthPx { get { return Depth * 20; } }

        // ── Bubbled events ───────────────────────────────────────────────────

        public event EventHandler<AuthorSelectedEventArgs> AuthorSelected;

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Item == null)
            {
                Visible = false;
                return;
            }

            lnkAuthor.Text = System.Web.HttpUtility.HtmlEncode(Item.By ?? "[deleted]");
            litTimeAgo.Text = Item.TimeAgo;
            // The API returns HTML — render verbatim.
            litText.Text = string.IsNullOrEmpty(Item.Text) ? "<em>(empty)</em>" : Item.Text;
        }

        protected void lnkAuthor_Click(object sender, EventArgs e)
        {
            if (Item == null || string.IsNullOrEmpty(Item.By)) return;
            AuthorSelected?.Invoke(this, new AuthorSelectedEventArgs(Item.By));
        }
    }
}
