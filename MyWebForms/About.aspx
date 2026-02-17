<%@ Page Title="About" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="About.aspx.cs" Inherits="MyWebForms.About" %>

<%@ Register Src="~/LibraryStatusWidget.ascx" TagPrefix="uc" TagName="StatusWidget" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <main class="container mt-4">
        <div class="text-center animate__animated animate__fadeInDown">
            <h2 id="title" class="display-4"><%: Title %></h2>
            <p class="lead">System Status & Library Integration Demo</p>
        </div>

        <div class="row">
            <div class="col-md-8 offset-md-2">
                <div class="card shadow">
                    <div class="card-header bg-primary text-white">
                        LibMan + Chart.js Live Demo
                    </div>
                    <div class="card-body text-center">
                        <canvas id="libmanChart" width="400" height="200"></canvas>

                        <%-- Hidden field carries server-side data to the client safely --%>
                        <asp:HiddenField ID="hfChartData" runat="server" />

                        <hr />

                        <asp:Label ID="lblMessage" runat="server" CssClass="h5 text-success" />
                        <br /><br />
                        <asp:Button ID="btnRefresh" runat="server" Text="Update Data from Server"
                            CssClass="btn btn-outline-primary" OnClick="btnRefresh_Click" />
                    </div>
                </div>
            </div>
        </div>
    </main>

    <script>
        $(document).ready(function () {
            // 1. Read the JSON that the code-behind wrote into the hidden field
            var rawValue = $('#<%= hfChartData.ClientID %>').val();
            var chartData = JSON.parse(rawValue);

            // 2. Initialize Chart.js
            var ctx = document.getElementById('libmanChart').getContext('2d');
            new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: ['jQuery 3 (NuGet)', 'Chart.js (LibMan)', 'Animate.css (LibMan)'],
                    datasets: [{
                        label: 'Library Load Strength',
                        data: chartData,
                        backgroundColor: [
                            'rgba(54, 162, 235, 0.6)',
                            'rgba(255, 99, 132, 0.6)',
                            'rgba(75, 192, 192, 0.6)'
                        ],
                        borderWidth: 1
                    }]
                },
                options: {
                    scales: { y: { beginAtZero: true, max: 100 } },
                    plugins: {
                        legend: { display: false }
                    }
                }
            });

            // 3. Simple jQuery effect to show the library is working
            $('#title').on('mouseenter', function () {
                $(this).addClass('animate__animated animate__pulse');
            }).on('mouseleave', function () {
                $(this).removeClass('animate__animated animate__pulse');
            });
        });
    </script>
</asp:Content>
