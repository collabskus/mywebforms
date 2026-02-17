using System;
using System.Web.UI;

namespace MyWebForms
{
    public partial class About : Page
    {
        // This property will be accessed in the .aspx via <%= ChartDataJson %>
        public string ChartDataJson { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SetDefaultData();
            }
        }

        private void SetDefaultData()
        {
            // Simulating server-side logic to determine "Health" of libraries
            int[] values = { 95, 88, 100 };
            ChartDataJson = $"[{string.Join(",", values)}]";

            lblMessage.Text = "System initialized with Server-Side Data.";
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            // Change data on button click to show PostBack capability
            Random rng = new Random();
            int[] newValues = { rng.Next(70, 100), rng.Next(70, 100), rng.Next(70, 100) };

            ChartDataJson = $"[{string.Join(",", newValues)}]";

            lblMessage.Text = "Data refreshed via Code-Behind at " + DateTime.Now.ToLongTimeString();

            // Note: Chart will re-render on page reload with these new values
        }
    }
}
