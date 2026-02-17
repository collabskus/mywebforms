using System;
using MyWebForms.Models;

namespace MyWebForms
{
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
            if (User == null)
            {
                Visible = false;
                return;
            }

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
                // About can contain HTML tags — render verbatim (source is HN API).
                litAbout.Text = User.About;
            }
        }
    }
}
