using System;
using MyWebForms.Models;

namespace MyWebForms
{
    /// <summary>
    /// Code-behind for HnUserCard.ascx.
    /// Displays a Hacker News user profile card.
    ///
    /// Lifecycle fix — why BindData() exists
    /// ---------------------------------------
    /// The host page (HackerNews.aspx) sets the User property in its
    /// OnPreRenderComplete override, which fires AFTER all child controls
    /// have already run their Page_Load and PreRender phases.  If we put
    /// the binding logic in Page_Load (as before), User is always null at
    /// that point and the control hides itself.
    ///
    /// The solution is to expose a public BindData() method that the host
    /// page calls explicitly after setting the User property.  This is a
    /// common Web Forms pattern for "late-bound" user controls where the
    /// data isn't available until after the normal lifecycle phases.
    ///
    /// Educational note:
    ///   In the Web Forms page lifecycle, events fire in this order:
    ///     Page.Load → ChildControl.Load → ... → Page.PreRender →
    ///     ChildControl.PreRender → Page.PreRenderComplete
    ///   So a parent page's PreRenderComplete is the LAST lifecycle hook
    ///   before SaveViewState/Render.  Any property set there is invisible
    ///   to the child control's Load and PreRender.  The explicit-bind
    ///   pattern avoids this timing problem entirely.
    /// </summary>
    public partial class HnUserCard : System.Web.UI.UserControl
    {
        // ── Inputs ───────────────────────────────────────────────────────────

        public HackerNewsUser User { get; set; }

        // ── Derived (used in markup via <%= %>) ──────────────────────────────

        protected string EncodedUsername
        {
            get
            {
                return User != null
                    ? System.Web.HttpUtility.UrlEncode(User.Id)
                    : string.Empty;
            }
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            // Intentionally empty.
            //
            // Binding is deferred to the explicit BindData() call made by the
            // host page in OnPreRenderComplete.  At Page_Load time the User
            // property has not yet been set by the host page, so any binding
            // here would always see User == null and hide the control.
        }

        // ── Public binding method ────────────────────────────────────────────

        /// <summary>
        /// Populates the control's UI from the current User property.
        /// Must be called by the host page AFTER setting User — typically
        /// from OnPreRenderComplete or equivalent late-lifecycle hook.
        ///
        /// If User is null, the control hides itself.
        /// </summary>
        public void BindData()
        {
            if (User == null)
            {
                Visible = false;
                return;
            }

            Visible = true;

            litUsername.Text = System.Web.HttpUtility.HtmlEncode(User.Id);
            litKarma.Text = User.Karma.ToString("N0");
            litMemberSince.Text = User.MemberSince;
            litSubmissionCount.Text = User.Submitted != null
                ? User.Submitted.Count.ToString("N0")
                : "0";

            if (string.IsNullOrEmpty(User.About))
            {
                pnlAbout.Visible = false;
            }
            else
            {
                pnlAbout.Visible = true;
                // About can contain HTML tags — render verbatim (source is HN API).
                litAbout.Text = User.About;
            }
        }
    }
}
