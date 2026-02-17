using System;
using System.Web.UI;

namespace MyWebForms
{
    public partial class About : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                SetDefaultData();
            }
        }

        private void SetDefaultData()
        {
            int[] values = { 95, 88, 100 };
            hfChartData.Value = "[" + string.Join(",", values) + "]";
            lblMessage.Text = "System initialized with Server-Side Data.";
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            Random rng = new Random();
            int[] newValues = { rng.Next(70, 100), rng.Next(70, 100), rng.Next(70, 100) };
            hfChartData.Value = "[" + string.Join(",", newValues) + "]";
            lblMessage.Text = "Data refreshed via Code-Behind at " + DateTime.Now.ToLongTimeString();
        }
    }
}
