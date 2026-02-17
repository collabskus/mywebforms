using System;

namespace MyWebForms
{
    public partial class LibraryStatusWidget : System.Web.UI.UserControl
    {
        // Custom Properties to set from the parent page
        public string LibraryName { get; set; }
        public string Version { get; set; }
        public int ReliabilityScore { get; set; } // 0 to 100

        protected void Page_Load(object sender, EventArgs e)
        {
            litLibraryName.Text = LibraryName;
            litVersion.Text = Version;

            // Set the width for our jQuery animation to pick up
            progressBar.Attributes.Add("aria-valuenow", ReliabilityScore.ToString());

            // Change color based on score
            if (ReliabilityScore > 90) progressBar.Attributes["class"] += " bg-success";
            else if (ReliabilityScore > 70) progressBar.Attributes["class"] += " bg-warning";
            else progressBar.Attributes["class"] += " bg-danger";
        }
    }
}
