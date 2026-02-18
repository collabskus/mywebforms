<%@ Page Title="Hacker News" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="HackerNews.aspx.cs"
    Inherits="MyWebForms.HackerNews" Async="true" %>

<%@ Register Src="~/HnStoryRow.ascx"  TagPrefix="uc" TagName="StoryRow"  %>
<%@ Register Src="~/HnComment.ascx"   TagPrefix="uc" TagName="Comment"   %>
<%@ Register Src="~/HnUserCard.ascx"  TagPrefix="uc" TagName="UserCard"  %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

<main class="container-fluid mt-3" style="max-width:960px;">

    <%-- ══════════════════════════════════════════════════════════════
         Page header
    ════════════════════════════════════════════════════════════════════ --%>
    <div class="d-flex align-items-center mb-3 flex-wrap gap-2">
        <span class="hn-logo me-2" style="background:#ff6600;color:#fff;font-weight:bold;padding:2px 6px;font-family:monospace;">Y</span>
        <h4 class="mb-0 fw-bold">Hacker News</h4>

        <%-- LIVE badge: shown on the "new" / "active" / "rising" tabs.
             The server sets its Visible flag; the JS countdown sits next to it. --%>
        <asp:Panel ID="pnlLiveBadge" runat="server" Visible="false">
            <span class="badge bg-danger" style="font-size:.65rem;">● LIVE</span>
        </asp:Panel>
    </div>

    <%-- Hidden field: keeps the active tab name in the DOM so the JS
         refresh poller can read it without a round-trip. --%>
    <asp:HiddenField ID="hfActiveTab" runat="server" />

    <%-- ══════════════════════════════════════════════════════════════
         Tab bar
    ════════════════════════════════════════════════════════════════════ --%>
    <ul class="nav nav-underline mb-3 flex-wrap" style="border-bottom:1px solid #dee2e6;">
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
            <%-- Active: recently-updated items from /v0/updates.json.
                 May include comment-type items; HnStoryRow handles these specially. --%>
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

    <%-- ══════════════════════════════════════════════════════════════
         Rising tab filter controls — only visible on "rising" tab
    ════════════════════════════════════════════════════════════════════ --%>
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

    <%-- ══════════════════════════════════════════════════════════════
         Error / info message panel
    ════════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlMessage" runat="server" Visible="false"
        CssClass="alert alert-warning">
        <asp:Literal ID="litMessage" runat="server" />
    </asp:Panel>

    <%-- ══════════════════════════════════════════════════════════════
         Story list (dynamically populated)
    ════════════════════════════════════════════════════════════════════ --%>
    <asp:PlaceHolder ID="phStories" runat="server" />

    <%-- ══════════════════════════════════════════════════════════════
         Pager
    ════════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlPager" runat="server" Visible="false"
        CssClass="d-flex align-items-center gap-3 mt-3">
        <asp:LinkButton ID="btnPrev" runat="server"
            CssClass="btn btn-sm btn-outline-secondary hn-postback-link"
            OnClick="btnPrev_Click">&laquo; Prev</asp:LinkButton>
        <span class="small text-muted">
            Page <asp:Literal ID="litPage" runat="server" /> of
            <asp:Literal ID="litPageCount" runat="server" />
        </span>
        <asp:LinkButton ID="btnNext" runat="server"
            CssClass="btn btn-sm btn-outline-secondary hn-postback-link"
            OnClick="btnNext_Click">Next &raquo;</asp:LinkButton>
    </asp:Panel>

    <%-- ══════════════════════════════════════════════════════════════
         Story detail panel
    ════════════════════════════════════════════════════════════════════ --%>
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

            <%-- pnlCommentLoading is shown via JavaScript (see script below)
                 as soon as the user clicks "N comments", giving immediate feedback
                 during the server round-trip.  The server sets Visible=false once
                 the comments are bound and rendered. --%>
            <asp:Panel ID="pnlCommentLoading" runat="server" Visible="false"
                CssClass="text-center py-3">
                <div class="spinner-border spinner-border-sm text-warning" role="status"></div>
                <span class="ms-2 small text-muted">Loading comments…</span>
            </asp:Panel>

            <asp:PlaceHolder ID="phComments" runat="server" />
        </div>
    </asp:Panel>

    <%-- ══════════════════════════════════════════════════════════════
         User profile panel
    ════════════════════════════════════════════════════════════════════ --%>
    <asp:Panel ID="pnlUserProfile" runat="server" Visible="false"
        CssClass="animate__animated animate__fadeIn">
        <div class="d-flex justify-content-end mb-1">
            <asp:LinkButton ID="btnCloseUser" runat="server"
                CssClass="btn btn-sm btn-outline-secondary"
                OnClick="btnCloseUser_Click">&#10005; Close</asp:LinkButton>
        </div>

        <%-- Shown when the HN API returns null for the requested username --%>
        <asp:Panel ID="pnlUserNotFound" runat="server" Visible="false"
            CssClass="alert alert-warning mt-2">
            <asp:Literal ID="litUserNotFound" runat="server" />
        </asp:Panel>

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
    /* Comment snippet shown in place of title for comment-type active items */
    .hn-comment-snippet {
        display: block;
        max-width: 100%;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
    }
</style>

<%-- Page-specific script: shows pnlCommentLoading as soon as a "comments"
     link is clicked, providing immediate visual feedback for the slow
     server-side API call.

     We can't show the server-rendered spinner before the round-trip, but we
     CAN show a client-side spinner by replacing phComments content in-place
     until the response arrives and re-renders the panel. --%>
<script>
    (function () {
        document.addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== document) {
                if (t.classList && t.classList.contains('hn-comments-link')) {
                    // Show an inline loading message inside the detail area if it
                    // is already open, so the user knows something is happening.
                    var loading = document.getElementById('<%= pnlCommentLoading.ClientID %>');
                var comments = document.getElementById('<%= phComments.ClientID %>');
                if (loading) {
                    loading.style.display = 'block';
                    loading.style.visibility = 'visible';
                }
                if (comments) comments.style.opacity = '0.3';
                break;
            }
            t = t.parentNode;
        }
    });
    }());
</script>

</asp:Content>
