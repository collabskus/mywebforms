<%@ Page Title="Hacker News" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="HackerNews.aspx.cs"
    Inherits="MyWebForms.HackerNews" Async="true" %>

<%@ Register Src="~/HnStoryRow.ascx"  TagPrefix="uc" TagName="StoryRow"  %>
<%@ Register Src="~/HnComment.ascx"   TagPrefix="uc" TagName="Comment"   %>
<%@ Register Src="~/HnUserCard.ascx"  TagPrefix="uc" TagName="UserCard"  %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

<main class="container-fluid mt-3" style="max-width:960px;">

    <%-- ═══════════════════════════════════════════════════════════════
         Page header
    ═══════════════════════════════════════════════════════════════════ --%>
    <div class="d-flex align-items-center mb-3 flex-wrap gap-2">
        <span class="hn-logo me-2" style="background:#ff6600;color:#fff;font-weight:bold;padding:2px 6px;font-family:monospace;">Y</span>
        <h4 class="mb-0 fw-bold">Hacker News</h4>

        <%-- LIVE badge: shown on the "new" / "active" / "rising" tabs.
             The server sets its Visible flag; the JS countdown sits next to it. --%>
        <asp:HiddenField ID="hfActiveTab" runat="server" />
        <span class="ms-auto d-flex align-items-center gap-2">
            <asp:Label ID="lblRefreshCountdown" runat="server" Visible="false"
                CssClass="text-muted small" />
            <asp:Label ID="lblLiveBadge" runat="server"
                CssClass="badge bg-success animate__animated animate__pulse animate__infinite"
                style="font-size:.65rem;">LIVE</asp:Label>
        </span>
    </div>

    <%-- ═══════════════════════════════════════════════════════════════
         Tab navigation
         Tabs: Top | New | Best | Ask | Show | Jobs | Active | Rising
    ═══════════════════════════════════════════════════════════════════ --%>
    <ul class="nav nav-tabs mb-3" id="hnTabs">
        <li class="nav-item">
            <asp:LinkButton ID="btnTabTop"    runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="top">Top</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabNew"    runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="new">New</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabBest"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="best">Best</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabAsk"    runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="ask">Ask</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabShow"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="show">Show</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabJobs"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="jobs">Jobs</asp:LinkButton>
        </li>
        <li class="nav-item">
            <%-- Active: mirrors https://news.ycombinator.com/active
                 Uses the HN /v0/updates.json "items" list — recently changed IDs. --%>
            <asp:LinkButton ID="btnTabActive" runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="active">
                Active <span class="badge bg-secondary ms-1" style="font-size:.6rem;">⚡</span>
            </asp:LinkButton>
        </li>
        <li class="nav-item">
            <%-- Rising: new stories filtered by minimum comments OR points.
                 Thresholds are configurable via the controls below. --%>
            <asp:LinkButton ID="btnTabRising" runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="rising">
                Rising <span class="badge bg-secondary ms-1" style="font-size:.6rem;">🔥</span>
            </asp:LinkButton>
        </li>
    </ul>

    <%-- ═══════════════════════════════════════════════════════════════
         Rising tab filter controls — only visible on "rising" tab
    ═══════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlRisingFilter" runat="server" Visible="false"
        CssClass="card card-body mb-3 py-2 d-flex flex-row align-items-center gap-3 flex-wrap">
        <span class="small fw-semibold text-muted">Filter new stories by:</span>

        <div class="d-flex align-items-center gap-1">
            <label class="form-label mb-0 small" for="<%= txtMinComments.ClientID %>">Min comments:</label>
            <asp:TextBox ID="txtMinComments" runat="server"
                CssClass="form-control form-control-sm"
                Style="width:70px;"
                TextMode="Number"
                Text="5" />
        </div>

        <div class="d-flex align-items-center gap-1">
            <label class="form-label mb-0 small" for="<%= txtMinPoints.ClientID %>">Min points:</label>
            <asp:TextBox ID="txtMinPoints" runat="server"
                CssClass="form-control form-control-sm"
                Style="width:70px;"
                TextMode="Number"
                Text="5" />
        </div>

        <asp:Button ID="btnApplyFilter" runat="server"
            CssClass="btn btn-sm btn-outline-warning"
            Text="Apply"
            OnClick="btnApplyFilter_Click" />

        <span class="text-muted small">
            Shows new stories where <em>comments ≥ min</em> <strong>or</strong> <em>points ≥ min</em>.
        </span>
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════════
         Message panel (errors / empty state)
    ═══════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlMessage" runat="server" Visible="false"
        CssClass="alert alert-warning">
        <asp:Literal ID="litMessage" runat="server" />
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════════
         Story list
    ═══════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlStoryList" runat="server">
        <asp:PlaceHolder ID="phStories" runat="server" />
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════════
         Pager
    ═══════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlPager" runat="server" Visible="false"
        CssClass="d-flex justify-content-center align-items-center gap-3 my-3">
        <asp:Button ID="btnPrev" runat="server"
            CssClass="btn btn-sm btn-outline-secondary"
            Text="&laquo; Prev"
            OnClick="btnPrev_Click" />
        <span class="text-muted small">
            Page <asp:Literal ID="litPage" runat="server" /> of
            <asp:Literal ID="litPageCount" runat="server" />
        </span>
        <asp:Button ID="btnNext" runat="server"
            CssClass="btn btn-sm btn-outline-secondary"
            Text="Next &raquo;"
            OnClick="btnNext_Click" />
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════════
         Story detail panel
    ═══════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlStoryDetail" runat="server" Visible="false"
        CssClass="mt-4 animate__animated animate__fadeIn">

        <div class="d-flex justify-content-end mb-1">
            <asp:LinkButton ID="btnCloseDetail" runat="server"
                CssClass="btn btn-sm btn-outline-secondary"
                OnClick="btnCloseDetail_Click">&#10005; Close</asp:LinkButton>
        </div>

        <div class="card">
            <div class="card-body">
                <h5 class="card-title hn-title">
                    <asp:Literal ID="litDetailTitle" runat="server" />
                </h5>
                <p class="card-text hn-meta">
                    <asp:Literal ID="litDetailMeta" runat="server" />
                </p>

                <%-- Story body text (Ask HN, polls, etc.) --%>
                <asp:Panel ID="pnlDetailText" runat="server" Visible="false"
                    CssClass="hn-story-text border-top pt-2 mt-2">
                    <asp:Literal ID="litDetailText" runat="server" />
                </asp:Panel>

                <%-- Poll options --%>
                <asp:Panel ID="pnlPollOptions" runat="server" Visible="false"
                    CssClass="mt-3">
                    <h6>Poll options</h6>
                    <asp:PlaceHolder ID="phPollOptions" runat="server" />
                </asp:Panel>
            </div>
        </div>

        <%-- Comments --%>
        <div class="mt-3">
            <h6 class="text-muted small fw-semibold text-uppercase mb-2">
                <asp:Literal ID="litDetailCommentCount" runat="server" /> comments
            </h6>

            <asp:Panel ID="pnlCommentLoading" runat="server" Visible="false"
                CssClass="text-center py-3">
                <div class="spinner-border spinner-border-sm text-warning" role="status"></div>
                <span class="ms-2 small text-muted">Loading comments…</span>
            </asp:Panel>

            <asp:PlaceHolder ID="phComments" runat="server" />
        </div>
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════════
         User profile panel
    ═══════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlUserProfile" runat="server" Visible="false"
        CssClass="animate__animated animate__fadeIn">
        <div class="d-flex justify-content-end mb-1">
            <asp:LinkButton ID="btnCloseUser" runat="server"
                CssClass="btn btn-sm btn-outline-secondary"
                OnClick="btnCloseUser_Click">&#10005; Close</asp:LinkButton>
        </div>
        <uc:UserCard ID="ucUserCard" runat="server" />
    </asp:Panel>

</main>

<style>
    .hn-title { font-size: .95rem; }
    .hn-rank  { font-size: .85rem; }
    .hn-meta  { font-size: .78rem; }
    .hn-comment-text pre,
    .hn-comment-text code { white-space: pre-wrap; word-break: break-word; }
    .hn-story-text  pre,
    .hn-story-text  code { white-space: pre-wrap; word-break: break-word; }
    .hn-user-about a { color: inherit; }
    .hn-author-link, .hn-comments-link {
        background: none; border: none; padding: 0;
        color: inherit; text-decoration: underline dotted;
        cursor: pointer;
    }
    .nav-link.active { color: #ff6600 !important; border-bottom-color: #ff6600 !important; }
    .hn-poll-option   { padding: .4rem 0; border-bottom: 1px solid #eee; }
</style>

</asp:Content>
