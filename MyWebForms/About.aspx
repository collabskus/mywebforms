<%@ Page Title="About" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="About.aspx.cs"
    Inherits="MyWebForms.About" %>

<%@ Register Src="~/LibraryStatusWidget.ascx" TagPrefix="uc" TagName="StatusWidget" %>
<%@ Register Src="~/ChartWidget.ascx"         TagPrefix="uc" TagName="ChartWidget"  %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <main class="container mt-4">

        <%-- Page heading --%>
        <div class="text-center animate__animated animate__fadeInDown mb-4">
            <h2 class="display-4"><%: Title %></h2>
            <p class="lead">System Status &amp; Library Integration Demo</p>
        </div>

        <div class="row">
            <%-- ── Left column: Chart widget ─────────────────────────────── --%>
            <div class="col-md-7">
                <%--
                    ChartWidget is configured entirely through public properties.
                    If you omit them, the control falls back to its own defaults —
                    useful to know when prototyping.
                --%>
                <uc:ChartWidget runat="server" ID="libraryChart"
                    Title="Client Library Load Strength"
                    Description="Dummy reliability scores (0–100) for the client-side libraries bundled into this project. Hit Refresh to see the server generate a new random dataset via a postback."
                    ChartType="bar" />
            </div>

            <%-- ── Right column: LibraryStatusWidget user controls ────────── --%>
            <div class="col-md-5">
                <h5 class="mb-3">Library Status</h5>

                <%--
                    LibraryStatusWidget demonstrates a different pattern: properties
                    are set declaratively as tag attributes.  The control reads them
                    in its own Page_Load and updates its child controls.
                --%>
                <uc:StatusWidget runat="server"
                    LibraryName="jQuery"
                    Version="3.7.1 (NuGet)"
                    ReliabilityScore="95" />

                <uc:StatusWidget runat="server"
                    LibraryName="Bootstrap"
                    Version="5.3.8 (NuGet)"
                    ReliabilityScore="92" />

                <uc:StatusWidget runat="server"
                    LibraryName="Chart.js"
                    Version="4.4.1 (LibMan)"
                    ReliabilityScore="88" />

                <uc:StatusWidget runat="server"
                    LibraryName="Animate.css"
                    Version="4.1.1 (LibMan)"
                    ReliabilityScore="100" />
            </div>
        </div>

    </main>
</asp:Content>
