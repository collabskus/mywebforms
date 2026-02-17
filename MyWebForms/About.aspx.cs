using System;
using System.Web.UI;

namespace MyWebForms
{
    /// <summary>
    /// About page — intentionally thin code-behind.
    /// All chart logic lives in ChartWidget.ascx.cs;
    /// all status-bar logic lives in LibraryStatusWidget.ascx.cs.
    /// This page is responsible only for wiring them together.
    /// </summary>
    public partial class About : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Nothing to do here: both user controls initialise themselves.
            // This empty guard is left deliberately so you can see the pattern —
            // IsPostBack checks belong in Page_Load when the page itself needs
            // to distinguish first load from postback.
        }
    }
}
