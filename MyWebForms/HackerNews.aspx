<%@ Page Title="Hacker News" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="HackerNews.aspx.cs"
    Inherits="MyWebForms.HackerNews" Async="true" %>

<%@ Register Src="~/HnStoryRow.ascx"  TagPrefix="uc" TagName="StoryRow"  %>
<%@ Register Src="~/HnComment.ascx"   TagPrefix="uc" TagName="Comment"   %>
<%@ Register Src="~/HnUserCard.ascx"  TagPrefix="uc" TagName="UserCard"  %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

<main class="container-fluid mt-3" style="max-width:960px;">

    <%-- ═══════════════════════════════════════════════════════════
         Page header
    ════════════════════════════════════════════════════════════════ --%>
    <div class="d-flex align-items-center mb-3">
        <span class="hn-logo me-2" style="background:#ff6600;color:#fff;font-weight:bold;padding:2px 6px;font-family:monospace;">Y</span>
        <h4 class="mb-0 fw-bold">Hacker News</h4>

        <%-- LIVE badge: only shown/animated when on the "new" tab.
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

    <%-- ═══════════════════════════════════════════════════════════
         Tab navigation
    ════════════════════════════════════════════════════════════════ --%>
    <ul class="nav nav-tabs mb-3" id="hnTabs">
        <li class="nav-item">
            <asp:LinkButton ID="btnTabTop"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="top">Top</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabNew"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="new">New</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabBest"  runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="best">Best</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabAsk"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="ask">Ask</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabShow"  runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="show">Show</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabJobs"  runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="jobs">Jobs</asp:LinkButton>
        </li>
    </ul>

    <%-- ═══════════════════════════════════════════════════════════
         Message panel (errors / empty state)
    ════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlMessage" runat="server" Visible="false"
        CssClass="alert alert-warning">
        <asp:Literal ID="litMessage" runat="server" />
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════
         Story list
    ════════════════════════════════════════════════════════════════ --%>
    <asp:PlaceHolder ID="phStories" runat="server" />

    <%-- ═══════════════════════════════════════════════════════════
         Pager
    ════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlPager" runat="server" CssClass="d-flex align-items-center gap-3 mt-3">
        <asp:LinkButton ID="btnPrev" runat="server" CssClass="btn btn-outline-secondary btn-sm"
            OnClick="btnPrev_Click">&laquo; Prev</asp:LinkButton>
        <span class="small text-muted">
            Page <asp:Literal ID="litPage" runat="server" /> of
            <asp:Literal ID="litPageCount" runat="server" />
        </span>
        <asp:LinkButton ID="btnNext" runat="server" CssClass="btn btn-outline-secondary btn-sm"
            OnClick="btnNext_Click">Next &raquo;</asp:LinkButton>
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════
         Story detail panel
    ════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlStoryDetail" runat="server" Visible="false"
        CssClass="mt-4 p-3 border rounded animate__animated animate__fadeIn">

        <div class="d-flex justify-content-end mb-1">
            <asp:LinkButton ID="btnCloseDetail" runat="server"
                CssClass="btn btn-sm btn-outline-secondary"
                OnClick="btnCloseDetail_Click">&#10005; Close</asp:LinkButton>
        </div>

        <h5><asp:Literal ID="litDetailTitle" runat="server" /></h5>
        <p class="small text-muted mb-2">
            <asp:Literal ID="litDetailMeta" runat="server" />
        </p>

        <%-- Self text (Ask HN / jobs) --%>
        <asp:Panel ID="pnlDetailText" runat="server" Visible="false"
            CssClass="hn-story-text mb-3 p-2 bg-light rounded small">
            <asp:Literal ID="litDetailText" runat="server" />
        </asp:Panel>

        <%-- Poll options --%>
        <asp:Panel ID="pnlPollOptions" runat="server" Visible="false" CssClass="mb-3">
            <h6 class="small text-uppercase text-muted">Poll options</h6>
            <asp:PlaceHolder ID="phPollOptions" runat="server" />
        </asp:Panel>

        <%-- Comments --%>
        <h6 class="mt-3">
            <asp:Literal ID="litDetailCommentCount" runat="server" /> comments
        </h6>
        <asp:Panel ID="pnlCommentLoading" runat="server" Visible="true"
            CssClass="text-muted small">Loading comments&hellip;</asp:Panel>
        <asp:PlaceHolder ID="phComments" runat="server" />
    </asp:Panel>

    <%-- ═══════════════════════════════════════════════════════════
         User profile panel
    ════════════════════════════════════════════════════════════════ --%>
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
