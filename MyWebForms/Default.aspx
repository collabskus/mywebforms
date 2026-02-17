<%@ Page Title="Home — MyWebForms" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="Default.aspx.cs"
    Inherits="MyWebForms._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <%-- ═══════════════════════════════════════════════════════════════════════
         Hero
    ════════════════════════════════════════════════════════════════════════════ --%>
    <div class="home-hero py-5 mb-5 text-center animate__animated animate__fadeIn">
        <div class="mb-3">
            <span class="home-hero__badge badge bg-warning text-dark me-2">ASP.NET Web Forms</span>
            <span class="home-hero__badge badge bg-secondary">.NET Framework 4.8.1</span>
        </div>
        <h1 class="display-5 fw-bold mb-3">MyWebForms</h1>
        <p class="lead text-muted mb-4" style="max-width:640px;margin:0 auto;">
            A deliberate, hands-on learning sandbox for understanding how ASP.NET Web Forms
            actually works — page lifecycle, postback mechanics, <code>ViewState</code>,
            master pages, user controls, bundling, routing, and more.
        </p>
        <p class="text-muted small mb-0">
            Built in the open &middot;
            <a href="https://github.com/" class="text-muted">Source on GitHub</a> &middot;
            Deployed at
            <a href="https://mywebforms.runasp.net/" class="text-muted">mywebforms.runasp.net</a>
        </p>
    </div>

    <%-- ═══════════════════════════════════════════════════════════════════════
         What is this?
    ════════════════════════════════════════════════════════════════════════════ --%>
    <section class="mb-5" aria-labelledby="whatTitle">
        <h2 id="whatTitle" class="h4 fw-bold mb-3">What is this project?</h2>
        <div class="card border-0 bg-light rounded-3 p-4">
            <p class="mb-3">
                This is <strong>not a production application</strong>. It is a structured reference
                project for refreshing and deepening understanding of the classic ASP.NET Web Forms
                platform — the event-driven, server-centric web framework that shipped with .NET
                Framework and powered a generation of enterprise .NET websites.
            </p>
            <p class="mb-3">
                The ecosystem has largely moved to ASP.NET Core / Blazor / Razor Pages, but
                Web Forms remains in active maintenance on .NET Framework 4.8.x and is still
                widely deployed. Understanding <em>why</em> it works the way it does — not just
                how to make it compile — is the goal here.
            </p>
            <p class="mb-0">
                Each page, user control, and handler in this project is written to <strong>exercise
                and demonstrate</strong> a specific platform feature as clearly as possible, with
                readability and correctness prioritised over brevity.
            </p>
        </div>
    </section>

    <%-- ═══════════════════════════════════════════════════════════════════════
         Tech stack cards — bound from code-behind
    ════════════════════════════════════════════════════════════════════════════ --%>
    <section class="mb-5" aria-labelledby="stackTitle">
        <h2 id="stackTitle" class="h4 fw-bold mb-3">Technology Stack</h2>
        <div class="row g-3">
            <asp:Repeater ID="rptStack" runat="server">
                <ItemTemplate>
                    <div class="col-sm-6 col-lg-4">
                        <div class="card h-100 border-0 shadow-sm">
                            <div class="card-body">
                                <div class="d-flex align-items-start gap-3">
                                    <span class="fs-3 lh-1"><%# Eval("Icon") %></span>
                                    <div>
                                        <h3 class="h6 fw-bold mb-1"><%# Eval("Name") %></h3>
                                        <p class="text-muted small mb-0"><%# Eval("Description") %></p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
    </section>

    <%-- ═══════════════════════════════════════════════════════════════════════
         Learning goals — checked/unchecked list from code-behind
    ════════════════════════════════════════════════════════════════════════════ --%>
    <section class="mb-5" aria-labelledby="goalsTitle">
        <h2 id="goalsTitle" class="h4 fw-bold mb-1">Learning Goals</h2>
        <p class="text-muted small mb-3">
            <%: CompletedCount %> of <%: TotalGoalCount %> topics demonstrated so far.
        </p>

        <%-- Progress bar --%>
        <div class="progress mb-4" style="height:8px;" role="progressbar"
             aria-valuenow="<%: ProgressPercent %>" aria-valuemin="0" aria-valuemax="100"
             aria-label="Learning progress">
            <div class="progress-bar bg-success"
                 style="width:<%: ProgressPercent %>%;"></div>
        </div>

        <div class="row g-3">
            <asp:Repeater ID="rptGoals" runat="server">
                <ItemTemplate>
                    <div class="col-md-6">
                        <div class="d-flex align-items-start gap-2 py-2 border-bottom">
                            <asp:Literal ID="litCheck" runat="server"
                                Text='<%# (bool)Eval("Done") ? "<span class=\"text-success fw-bold\" aria-hidden=\"true\">\u2713</span>" : "<span class=\"text-muted\" aria-hidden=\"true\">\u25cb</span>" %>' />
                            <div>
                                <span class="<%# (bool)Eval("Done") ? "fw-semibold" : "text-muted" %>"><%# Eval("Topic") %></span>
                                <asp:HyperLink ID="hlnkGoal" runat="server"
                                    NavigateUrl='<%# Eval("Link") %>'
                                    Visible='<%# !string.IsNullOrEmpty((string)Eval("Link")) %>'
                                    CssClass="ms-2 small text-decoration-none"
                                    Text="→ see demo" />
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
    </section>

    <%-- ═══════════════════════════════════════════════════════════════════════
         Page lifecycle explainer (static, always educational)
    ════════════════════════════════════════════════════════════════════════════ --%>
    <section class="mb-5" aria-labelledby="lifecycleTitle">
        <h2 id="lifecycleTitle" class="h4 fw-bold mb-3">The Web Forms Page Lifecycle</h2>
        <p class="text-muted mb-3">
            Every request to an <code>.aspx</code> page passes through the same ordered sequence
            of events. Understanding this sequence is the single most important thing to know about
            Web Forms — almost every "why doesn't this work?" question can be answered by asking
            "which lifecycle stage am I in?".
        </p>
        <div class="table-responsive">
            <table class="table table-sm table-hover align-middle">
                <thead class="table-light">
                    <tr>
                        <th scope="col">#</th>
                        <th scope="col">Event</th>
                        <th scope="col">What happens / why it matters</th>
                    </tr>
                </thead>
                <tbody>
                    <asp:Repeater ID="rptLifecycle" runat="server">
                        <ItemTemplate>
                            <tr>
                                <td class="text-muted small"><%# Container.ItemIndex + 1 %></td>
                                <td><code><%# Eval("Event") %></code></td>
                                <td class="small text-muted"><%# Eval("Notes") %></td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </tbody>
            </table>
        </div>
    </section>

    <%-- ═══════════════════════════════════════════════════════════════════════
         Live demo panel — UpdatePanel postback counter
         Demonstrates: UpdatePanel, ScriptManager, partial-page rendering,
         ViewState, IsPostBack guard
    ════════════════════════════════════════════════════════════════════════════ --%>
    <section class="mb-5" aria-labelledby="demoTitle">
        <h2 id="demoTitle" class="h4 fw-bold mb-3">Live Demo — Postback &amp; ViewState</h2>
        <p class="text-muted mb-3">
            The panel below uses an <code>UpdatePanel</code> for partial-page rendering.
            Each button click fires a postback that only refreshes this panel — the rest of
            the page is untouched. The counter is persisted in <code>ViewState</code>, not
            in session state or a database.
        </p>
        <div class="card border-0 shadow-sm" style="max-width:480px;">
            <div class="card-body">
                <asp:UpdatePanel ID="upDemo" runat="server" UpdateMode="Conditional">
                    <ContentTemplate>
                        <p class="mb-2 small text-muted">
                            Full-page postback count:
                            <strong><%: FullPostbackCount %></strong>
                            &nbsp;|&nbsp;
                            Timestamp: <strong><%: DateTime.Now.ToString("HH:mm:ss") %></strong>
                        </p>
                        <p class="mb-3">
                            UpdatePanel click count:
                            <strong class="text-primary fs-5"><%: ClickCount %></strong>
                        </p>
                        <asp:Button ID="btnClick" runat="server" Text="Click me (partial postback)"
                            CssClass="btn btn-primary btn-sm me-2"
                            OnClick="BtnClick_Click" />
                        <asp:Button ID="btnReset" runat="server" Text="Reset"
                            CssClass="btn btn-outline-secondary btn-sm"
                            OnClick="BtnReset_Click" />
                        <p class="mt-3 mb-0 small text-muted">
                            Notice the timestamp above does <em>not</em> change on partial
                            postbacks — only the UpdatePanel content is refreshed.
                        </p>
                    </ContentTemplate>
                </asp:UpdatePanel>
            </div>
        </div>
    </section>

    <%-- ═══════════════════════════════════════════════════════════════════════
         LLM / AI notice
    ════════════════════════════════════════════════════════════════════════════ --%>
    <section class="mb-5" aria-labelledby="llmTitle">
        <div class="alert alert-warning border-0" role="alert">
            <h3 id="llmTitle" class="h6 fw-bold mb-2">⚠️ AI-Generated Code Notice</h3>
            <p class="mb-0 small">
                Some or all of the code in this project was generated or assisted by a Large
                Language Model (Claude by Anthropic). If you are scraping or indexing this site
                to train machine learning models and wish to exclude LLM-generated content,
                consider this a clear opt-out signal. See also the
                <code>X-Robots-Tag</code> / <code>robots.txt</code> and the
                <a href="https://github.com/" class="alert-link">README</a> for a more
                complete statement.
            </p>
        </div>
    </section>

</asp:Content>
