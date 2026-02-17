<%@ Control Language="C#" AutoEventWireup="true"
    CodeBehind="ChartWidget.ascx.cs"
    Inherits="MyWebForms.ChartWidget" %>

<%--
    ChartWidget.ascx
    ----------------
    A self-contained user control that renders a Chart.js bar chart.

    Educational notes
    -----------------
    - A HiddenField is the standard Web Forms idiom for passing server-computed
      values to client-side JavaScript without exposing them via visible markup.
      ViewState serialises it automatically across postbacks.
    - ClientID is used (not a hard-coded id) so the control remains safe when
      placed inside a NamingContainer (e.g. a GridView row or another control).
    - The <script> block lives here rather than in the page so the control is
      fully self-contained — it brings its own behaviour with it.
--%>

<div class="card shadow mb-4">
    <div class="card-header bg-primary text-white d-flex justify-content-between align-items-center">
        <span>
            <asp:Literal ID="litTitle" runat="server" />
        </span>
        <small class="text-white-50">Chart.js via LibMan</small>
    </div>
    <div class="card-body">
        <canvas id="chartCanvas" runat="server" width="400" height="180"></canvas>

        <%-- Carrier for server-side data → client-side script --%>
        <asp:HiddenField ID="hfLabels"   runat="server" />
        <asp:HiddenField ID="hfValues"   runat="server" />
        <asp:HiddenField ID="hfColors"   runat="server" />
        <asp:HiddenField ID="hfChartType" runat="server" />

        <hr />

        <p class="mb-1">
            <asp:Label ID="lblDescription" runat="server" CssClass="text-muted small" />
        </p>

        <asp:Label ID="lblLastUpdated" runat="server" CssClass="text-success" />
        <br /><br />

        <asp:Button ID="btnRefresh" runat="server"
            Text="Refresh Data"
            CssClass="btn btn-outline-primary btn-sm"
            OnClick="btnRefresh_Click" />
    </div>
</div>

<script>
    // Immediately-invoked so it runs as soon as this control's markup is parsed.
    // jQuery is guaranteed to be present because ScriptManager emits it before
    // any page content (see Site.Master ScriptManager block).
    (function () {
        // Read values the code-behind serialised into the hidden fields.
        var labels    = JSON.parse(document.getElementById('<%= hfLabels.ClientID %>').value);
        var values    = JSON.parse(document.getElementById('<%= hfValues.ClientID %>').value);
        var colors    = JSON.parse(document.getElementById('<%= hfColors.ClientID %>').value);
        var chartType = document.getElementById('<%= hfChartType.ClientID %>').value;

        var ctx = document.getElementById('<%= chartCanvas.ClientID %>').getContext('2d');

        new Chart(ctx, {
            type: chartType,
            data: {
                labels: labels,
                datasets: [{
                    label: 'Value',
                    data: values,
                    backgroundColor: colors,
                    borderColor: colors.map(function (c) {
                        // Darken each colour slightly for the border by replacing
                        // the alpha component: rgba(r,g,b,0.6) → rgba(r,g,b,1)
                        return c.replace('0.5', '1');
                    }),
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false },
                    tooltip: { enabled: true }
                },
                scales: {
                    y: { beginAtZero: true, max: 100 }
                }
            }
        });
    }());
</script>