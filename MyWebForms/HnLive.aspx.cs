using System;
using System.Web.UI;

namespace MyWebForms
{
    /// <summary>
    /// HnLive.aspx code-behind.
    ///
    /// This page is intentionally thin — all live-feed logic runs entirely
    /// in the browser via the HN Firebase REST API.  The server's only job
    /// is to serve the page shell; no postbacks, no ViewState, no async tasks.
    ///
    /// Educational note:
    ///   Web Forms does not require postback-driven interaction.  A page can
    ///   be a pure host for client-side JavaScript just as easily as an MVC
    ///   or Razor Pages view.  EnableViewState is disabled at the page level
    ///   (see the @Page directive) because nothing here round-trips.
    /// </summary>
    public partial class HnLive : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Nothing to do server-side — the live feed is driven entirely
            // by the inline JavaScript in HnLive.aspx.
        }
    }
}
