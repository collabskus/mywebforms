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
        <span class="ms-auto badge bg-success animate__animated animate__pulse animate__infinite"
              style="font-size:.65rem;">LIVE</span>
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
            <asp:LinkButton ID="btnTabAsk"   runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="ask">Ask HN</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabShow"  runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="show">Show HN</asp:LinkButton>
        </li>
        <li class="nav-item">
            <asp:LinkButton ID="btnTabJobs"  runat="server" CssClass="nav-link" OnClick="btnTab_Click" CommandArgument="jobs">Jobs</asp:LinkButton>
        </li>
    </ul>

    <%-- ═══════════════════════════════════════════════════════════
         Main two-column layout
    ════════════════════════════════════════════════════════════════ --%>
    <div class="row">

        <%-- ── Left: story list ─────────────────────────────────── --%>
        <div class="col-md-7">

            <%-- Loading / error message --%>
            <asp:Panel ID="pnlMessage" runat="server" Visible="false">
                <div class="alert alert-info small">
                    <asp:Literal ID="litMessage" runat="server" />
                </div>
            </asp:Panel>

            <%-- Story rows injected here by code-behind --%>
            <asp:PlaceHolder ID="phStories" runat="server" />

            <%-- Pagination --%>
            <asp:Panel ID="pnlPager" runat="server" CssClass="d-flex justify-content-between align-items-center mt-3">
                <asp:LinkButton ID="btnPrev" runat="server" CssClass="btn btn-sm btn-outline-secondary"
                    OnClick="btnPrev_Click">&#8592; Prev</asp:LinkButton>
                <small class="text-muted">
                    Page <asp:Literal ID="litPage" runat="server" />
                    of <asp:Literal ID="litPageCount" runat="server" />
                </small>
                <asp:LinkButton ID="btnNext" runat="server" CssClass="btn btn-sm btn-outline-secondary"
                    OnClick="btnNext_Click">Next &#8594;</asp:LinkButton>
            </asp:Panel>

        </div>

        <%-- ── Right: detail / user panel ──────────────────────── --%>
        <div class="col-md-5">

            <%-- Story detail panel --%>
            <asp:Panel ID="pnlStoryDetail" runat="server" Visible="false"
                CssClass="card shadow-sm mb-3 animate__animated animate__fadeIn">
                <div class="card-header bg-dark text-white d-flex justify-content-between">
                    <span class="fw-semibold small">
                        <asp:Literal ID="litDetailTitle" runat="server" />
                    </span>
                    <asp:LinkButton ID="btnCloseDetail" runat="server"
                        CssClass="btn-close btn-close-white btn-sm"
                        OnClick="btnCloseDetail_Click" />
                </div>
                <div class="card-body">
                    <div class="small text-muted mb-2">
                        <asp:Literal ID="litDetailMeta" runat="server" />
                    </div>
                    <%-- Self-text (Ask HN / polls) --%>
                    <asp:Panel ID="pnlDetailText" runat="server" CssClass="hn-story-text small mb-3">
                        <asp:Literal ID="litDetailText" runat="server" Mode="PassThrough" />
                    </asp:Panel>
                    <%-- Poll options --%>
                    <asp:Panel ID="pnlPollOptions" runat="server">
                        <asp:PlaceHolder ID="phPollOptions" runat="server" />
                    </asp:Panel>
                </div>
                <div class="card-body border-top">
                    <h6 class="text-muted small mb-2">
                        Comments (<asp:Literal ID="litDetailCommentCount" runat="server" />)
                    </h6>
                    <asp:Panel ID="pnlCommentLoading" runat="server" CssClass="text-muted small">
                        Loading comments&#8230;
                    </asp:Panel>
                    <asp:PlaceHolder ID="phComments" runat="server" />
                </div>
            </asp:Panel>

            <%-- User profile panel --%>
            <asp:Panel ID="pnlUserProfile" runat="server" Visible="false"
                CssClass="animate__animated animate__fadeIn">
                <div class="d-flex justify-content-end mb-1">
                    <asp:LinkButton ID="btnCloseUser" runat="server"
                        CssClass="btn btn-sm btn-outline-secondary"
                        OnClick="btnCloseUser_Click">&#10005; Close</asp:LinkButton>
                </div>
                <uc:UserCard ID="ucUserCard" runat="server" />
            </asp:Panel>

        </div>
    </div>

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
