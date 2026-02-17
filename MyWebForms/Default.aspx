<%@ Page Title="Home" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Default.aspx.cs" Inherits="MyWebForms._Default" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <%-- Page-specific styles, if any --%>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="MainContent" runat="server">

    <h1 class="mb-4">ASP.NET Web Forms — Learning Sandbox</h1>
    <p class="lead">
        This page demonstrates core Web Forms mechanics: the page lifecycle, postback
        detection, <code>ViewState</code>, <code>UpdatePanel</code> partial rendering,
        and server-driven UI updates.
    </p>

    <%-- =====================================================================
         SECTION 1: Page Lifecycle display
         lblTimestamp and lblIsPostBack are updated in Page_PreRender on every
         request so you can watch them change between full and async postbacks.
    ===================================================================== --%>
    <div class="card mb-4">
        <div class="card-header fw-semibold">1 — Page Lifecycle Snapshot</div>
        <div class="card-body">
            <p class="text-muted small">
                These values are written during <code>Page_PreRender</code>, which
                fires on <em>every</em> request — initial load and postback alike.
            </p>
            <dl class="row mb-0">
                <dt class="col-sm-3">Rendered at</dt>
                <dd class="col-sm-9">
                    <asp:Label ID="lblTimestamp" runat="server" /></dd>

                <dt class="col-sm-3">IsPostBack</dt>
                <dd class="col-sm-9">
                    <asp:Label ID="lblIsPostBack" runat="server" /></dd>

                <dt class="col-sm-3">Is Async Postback</dt>
                <dd class="col-sm-9">
                    <asp:Label ID="lblIsAsync" runat="server" /></dd>
            </dl>
        </div>
    </div>

    <%-- =====================================================================
         SECTION 2: UpdatePanel — async (partial-page) postback
         The UpdatePanel wraps only the counter and progress bar.  Clicking the
         button inside it triggers a partial-page postback: only the panel's
         content is sent back by the server and swapped in by MS Ajax — the
         rest of the page DOM is untouched.
    ===================================================================== --%>
    <div class="card mb-4">
        <div class="card-header fw-semibold">2 — UpdatePanel (Async Postback)</div>
        <div class="card-body">
            <p class="text-muted small">
                Click the button below. Only the counter and progress bar update —
                notice the page does <em>not</em> flash or scroll, because MS Ajax
                intercepts the postback and does a partial-page refresh.
            </p>

            <asp:UpdatePanel ID="upAsync" runat="server" UpdateMode="Conditional">
                <ContentTemplate>

                    <p>
                        Async click count:
                        <strong>
                            <asp:Label ID="lblClickCount" runat="server"
                                Text='<%# ClickCount %>' /></strong>
                    </p>

                    <%-- Progress bar width is set via progressBar.Style in PreRender
                         to avoid invalid CSS warnings in the VS designer. --%>
                    <div class="progress mb-3" style="height: 24px;">
                        <div id="progressBar" runat="server"
                            class="progress-bar progress-bar-striped progress-bar-animated"
                            role="progressbar"
                            aria-valuemin="0" aria-valuemax="100">
                            <%: ProgressPercent %>%
                        </div>
                    </div>

                    <asp:Button ID="btnAsyncClick" runat="server" Text="Click Me (Async)"
                        CssClass="btn btn-primary me-2"
                        OnClick="btnAsyncClick_Click" />

                </ContentTemplate>
            </asp:UpdatePanel>

            <asp:UpdateProgress ID="upProgress" runat="server" AssociatedUpdatePanelID="upAsync"
                DisplayAfter="200">
                <ProgressTemplate>
                    <span class="spinner-border spinner-border-sm text-primary ms-2"
                        role="status" aria-hidden="true"></span>
                    <span class="ms-1 text-muted small">Processing…</span>
                </ProgressTemplate>
            </asp:UpdateProgress>
        </div>
    </div>

    <%-- =====================================================================
         SECTION 3: Full postback
         This button lives outside any UpdatePanel so it causes a traditional
         full-page postback — the entire page lifecycle runs and every control
         re-renders.  Watch the lifecycle snapshot above update.
    ===================================================================== --%>
    <div class="card mb-4">
        <div class="card-header fw-semibold">3 — Full Postback</div>
        <div class="card-body">
            <p class="text-muted small">
                This button triggers a <em>full postback</em>. The entire page
                re-renders. Notice that <code>IsPostBack</code> becomes
                <code>True</code> and the render timestamp changes above.
            </p>

            <p>
                Full postback count:
                <strong><%: FullPostbackCount %></strong>
            </p>

            <asp:Button ID="btnFullPostback" runat="server" Text="Full Postback"
                CssClass="btn btn-warning me-2"
                OnClick="btnFullPostback_Click" />

            <asp:Button ID="btnReset" runat="server" Text="Reset All"
                CssClass="btn btn-outline-secondary"
                OnClick="btnReset_Click" />
        </div>
    </div>

    <%-- =====================================================================
         SECTION 4: ViewState explainer
    ===================================================================== --%>
    <div class="card mb-4">
        <div class="card-header fw-semibold">4 — How ViewState Keeps the Counts</div>
        <div class="card-body">
            <p>
                The click counters and progress value survive every postback because they
                are stored in <strong>ViewState</strong> — a hidden field
                (<code>__VIEWSTATE</code>) that the browser posts back with each request.
                The page deserialises it at the start of the lifecycle (during
                <em>LoadViewState</em>) before your event handler runs, so the previous
                values are available when <code>ClickCount++</code> executes.
            </p>
            <p>
                Open your browser's DevTools → Network tab, submit a postback, inspect
                the form payload and look for <code>__VIEWSTATE</code> to see the
                base-64 encoded blob.
            </p>
        </div>
    </div>

    <%-- Page-specific script at the bottom of MainContent, per coding conventions.
         Nothing is needed client-side here — ScriptManager in Site.Master wires
         up MS Ajax for UpdatePanel support automatically. --%>
    <script>
        // Intentionally empty: all UpdatePanel wiring is handled by the
        // ScriptManager declared in Site.Master.
    </script>

</asp:Content>
