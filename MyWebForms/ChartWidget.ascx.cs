using System;
using System.Web.Script.Serialization;

namespace MyWebForms
{
    /// <summary>
    /// A reusable Chart.js bar/line/pie chart widget.
    ///
    /// Educational notes
    /// -----------------
    /// Public properties are the Web Forms equivalent of component inputs.
    /// The parent page sets them before the control's Page_Load runs, so by
    /// the time we write to the HiddenFields the values are already available.
    ///
    /// JavaScriptSerializer (System.Web.Script.Serialization) is part of the
    /// BCL for .NET Framework — no external JSON package needed for this simple
    /// serialisation task.
    /// </summary>
    public partial class ChartWidget : System.Web.UI.UserControl
    {
        // ── Configurable inputs (set by the host page) ──────────────────────

        /// <summary>Heading shown in the card header.</summary>
        public string Title { get; set; }

        /// <summary>Short description shown below the chart.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Chart.js chart type string: "bar", "line", "pie", "doughnut", etc.
        /// Defaults to "bar".
        /// </summary>
        public string ChartType { get; set; }

        /// <summary>X-axis labels / pie slice labels.</summary>
        public string[] Labels { get; set; }

        /// <summary>Numeric values, one per label.</summary>
        public int[] Values { get; set; }

        /// <summary>
        /// CSS rgba() colour strings, one per data point.
        /// If null or mismatched in length, a default palette is generated.
        /// </summary>
        public string[] Colors { get; set; }

        // ── Default dummy data ───────────────────────────────────────────────

        private static readonly string[] DefaultLabels = {
            "jQuery (NuGet)", "Bootstrap (NuGet)", "Chart.js (LibMan)",
            "Animate.css (LibMan)", "Modernizr (NuGet)"
        };

        private static readonly int[] DefaultValues = { 95, 92, 88, 100, 75 };

        private static readonly string[] DefaultColors = {
            "rgba(54,  162, 235, 0.5)",
            "rgba(153, 102, 255, 0.5)",
            "rgba(255,  99, 132, 0.5)",
            "rgba( 75, 192, 192, 0.5)",
            "rgba(255, 159,  64, 0.5)"
        };

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            // Apply defaults for anything the host page did not supply.
            if (string.IsNullOrEmpty(Title)) Title = "Client Library Load Strength";
            if (string.IsNullOrEmpty(Description)) Description = "Dummy reliability scores for libraries used in this project.";
            if (string.IsNullOrEmpty(ChartType)) ChartType = "bar";
            if (Labels == null || Labels.Length == 0) Labels = DefaultLabels;
            if (Values == null || Values.Length == 0) Values = DefaultValues;
            if (Colors == null || Colors.Length == 0) Colors = DefaultColors;

            if (!IsPostBack)
            {
                BindChart(Values, "Initialized with default dummy data.");
            }

            // Always update non-data fields (safe to do on every load).
            litTitle.Text = Title;
            lblDescription.Text = Description;
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            // Demonstrate server-side randomisation surviving a postback.
            var rng = new Random();
            var newValues = new int[Labels.Length];
            for (int i = 0; i < newValues.Length; i++)
                newValues[i] = rng.Next(60, 100);

            BindChart(newValues, "Data refreshed at " + DateTime.Now.ToLongTimeString());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void BindChart(int[] values, string statusMessage)
        {
            // Ensure the Colors array is the same length as Labels.
            var safeColors = EnsureColorLength(Colors, Labels.Length);

            var serialiser = new JavaScriptSerializer();
            hfLabels.Value = serialiser.Serialize(Labels);
            hfValues.Value = serialiser.Serialize(values);
            hfColors.Value = serialiser.Serialize(safeColors);
            hfChartType.Value = ChartType;

            lblLastUpdated.Text = statusMessage;
        }

        private static string[] EnsureColorLength(string[] colors, int requiredLength)
        {
            // Cycle through the supplied palette to match length.
            var result = new string[requiredLength];
            for (int i = 0; i < requiredLength; i++)
                result[i] = colors[i % colors.Length];
            return result;
        }
    }
}
