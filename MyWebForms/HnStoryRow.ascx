<%@ Control Language="C#" AutoEventWireup="True"
    CodeBehind="HnStoryRow.ascx.cs"
    Inherits="MyWebForms.HnStoryRow" %>

<%--
    HnStoryRow.ascx
    ---------------
    Renders a single Hacker News item in the classic orange-site list style.
    Handles both story-type and comment-type items (e.g. from the Active tab).

    Comment-type items (type == "comment"):
      - Show a snippet of the comment text instead of a title.
      - The "N comments" link navigates to the *parent* story (not the comment
        itself, which has no meaningful comment thread of its own to display).
      - The score badge is hidden (comments have no score in the HN API).

    Score span strategy
    -------------------
    spanScore    (outer) — carries  data-hn-score-id  for general queries.
    spanScoreNum (inner) — carries  data-hn-score-num  so the background JS
                           poller can update ONLY the "NNN pts" text via
                             el.textContent = score + ' pts'
                           without clobbering the ▲ triangle.

    LinkButton vs HyperLink
    -----------------------
    - External story URL  → plain <a> (HyperLink) — leaves the app entirely
    - "N comments" link   → LinkButton — triggers a postback so the parent
      page can load the story detail panel server-side
    - Author name link    → LinkButton — same pattern for the user panel

    hn-postback-link CSS class
    --------------------------
    Applied to both LinkButtons so the Site.Master overlay script can detect
    any click on them and show the global loading spinner immediately, giving
    the user instant feedback while the server-side async load runs.
--%>

<div class="hn-story-row d-flex align-items-start py-2 border-bottom border-light">

    <%-- Rank number --%>
    <span class="hn-rank text-muted me-2 pt-1" style="min-width:2rem; text-align:right;">
        <asp:Literal ID="litRank" runat="server" />
    </span>

    <%--
        Score badge — hidden for comment-type items (comments have no score).
        The inner spanScoreNum carries data-hn-score-num, which the JS poller
        updates in-place without touching the ▲ triangle that lives in
        the outer span as raw HTML text.
    --%>
    <asp:Panel ID="pnlScore" runat="server">
        <span class="hn-score badge bg-warning text-dark me-3 mt-1"
              style="min-width:3.5rem; text-align:center;"
              runat="server" id="spanScore">
            &#9650;&nbsp;<span runat="server" id="spanScoreNum"><asp:Literal ID="litScore" runat="server" /></span>
        </span>
    </asp:Panel>

    <div class="flex-grow-1">
        <%-- Story title / comment snippet --%>
        <asp:HyperLink ID="lnkTitle" runat="server"
            CssClass="hn-title fw-semibold text-decoration-none text-dark"
            Target="_blank" />
        <%-- Comment snippet (shown instead of title for comment-type items) --%>
        <asp:Panel ID="pnlCommentSnippet" runat="server" Visible="false">
            <span class="hn-comment-snippet text-muted fst-italic small">
                <asp:Literal ID="litCommentSnippet" runat="server" />
            </span>
        </asp:Panel>
        <%-- Domain hint (hidden for comment items) --%>
        <asp:Panel ID="pnlDomain" runat="server">
            <small class="text-muted ms-1">
                (<asp:Literal ID="litDomain" runat="server" />)
            </small>
        </asp:Panel>

        <%-- Metadata line --%>
        <div class="hn-meta small text-muted mt-1">
            <asp:Literal ID="litTimeAgo" runat="server" />
            &nbsp;by&nbsp;
            <%-- hn-postback-link tells the Site.Master overlay script to show
                 the loading spinner as soon as this link is clicked. --%>
            <asp:LinkButton ID="lnkAuthor" runat="server"
                CssClass="text-muted hn-author-link hn-postback-link"
                OnClick="lnkAuthor_Click" />
            &nbsp;|&nbsp;
            <asp:LinkButton ID="lnkComments" runat="server"
                CssClass="text-muted hn-comments-link hn-postback-link"
                OnClick="lnkComments_Click" />
        </div>
    </div>

</div>
