using System;
using System.Web.UI;

namespace MyWebForms
{
    public partial class _Default : Page
    {
        // -----------------------------------------------------------------------
        // ViewState-backed properties
        // Must be 'protected' so the .aspx template can access them via <%: %>.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Number of times the async "Click Me" button has been pressed.
        /// Persisted in ViewState so the value survives partial-page postbacks.
        /// </summary>
        protected int ClickCount
        {
            get { return ViewState["ClickCount"] != null ? (int)ViewState["ClickCount"] : 0; }
            set { ViewState["ClickCount"] = value; }
        }

        /// <summary>
        /// Number of full (non-async) postbacks that have occurred on this page.
        /// Persisted in ViewState.
        /// </summary>
        protected int FullPostbackCount
        {
            get { return ViewState["FullPostbackCount"] != null ? (int)ViewState["FullPostbackCount"] : 0; }
            set { ViewState["FullPostbackCount"] = value; }
        }

        /// <summary>
        /// Progress percentage (0-100) displayed in the Bootstrap progress bar.
        /// Set from code-behind so the bar width is driven server-side.
        /// </summary>
        protected int ProgressPercent
        {
            get { return ViewState["ProgressPercent"] != null ? (int)ViewState["ProgressPercent"] : 25; }
            set { ViewState["ProgressPercent"] = value; }
        }

        // -----------------------------------------------------------------------
        // Page lifecycle
        // -----------------------------------------------------------------------

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // First request — initialise display labels via the controls
                // declared in Default.aspx (registered in designer file).
                UpdateLifecycleDisplay();
            }
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            // PreRender fires on every request (initial + postback).
            // Good place to push computed values to controls before rendering.
            UpdateLifecycleDisplay();

            // Drive the progress bar width from code-behind.
            // The HtmlGenericControl progressBar is declared in the designer.
            progressBar.Style["width"] = ProgressPercent.ToString() + "%";
            progressBar.Attributes["aria-valuenow"] = ProgressPercent.ToString();
        }

        // -----------------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Async postback via UpdatePanel — only the panel re-renders.
        /// </summary>
        protected void btnAsyncClick_Click(object sender, EventArgs e)
        {
            ClickCount++;

            // Advance progress bar by 15%, wrap at 100.
            ProgressPercent = Math.Min(ProgressPercent + 15, 100);
        }

        /// <summary>
        /// Full postback — entire page re-renders.
        /// </summary>
        protected void btnFullPostback_Click(object sender, EventArgs e)
        {
            FullPostbackCount++;
        }

        /// <summary>
        /// Reset all counters and progress.
        /// </summary>
        protected void btnReset_Click(object sender, EventArgs e)
        {
            ClickCount = 0;
            FullPostbackCount = 0;
            ProgressPercent = 25;
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private void UpdateLifecycleDisplay()
        {
            lblTimestamp.Text = DateTime.Now.ToString("HH:mm:ss.fff");
            lblIsPostBack.Text = IsPostBack.ToString();
        }
    }
}
