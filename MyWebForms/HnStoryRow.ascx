<%@ Control Language="C#" AutoEventWireup="True"
    CodeBehind="HnStoryRow.ascx.cs"
    Inherits="MyWebForms.HnStoryRow" %>

<%--
    HnStoryRow.ascx
    ---------------
    Renders a single Hacker News story in the classic orange-site list style.
    The parent page supplies the Item and Rank properties before Page_Load runs.

    LinkButton vs HyperLink:
      - External story URL  → plain <a> (HyperLink) — leaves the app entirely
      - "N comments" link   → LinkButton — triggers a postback so the parent
        page can load the story detail panel server-side without a full redirect
      - Author name link    → LinkButton — same pattern for the user panel
--%>

<div class="hn-story-row d-flex align-items-start py-2 border-bottom border-light">

    <%-- Rank number --%>
    <span class="hn-rank text-muted me-2 pt-1" style="min-width:2rem; text-align:right;">
        <asp:Literal ID="litRank" runat="server" />
    </span>

    <%-- Score badge --%>
    <span class="hn-score badge bg-warning text-dark me-3 mt-1" style="min-width:3rem;">
        <asp:Literal ID="litScore" runat="server" />▲
    </span>

    <div class="flex-grow-1">
        <%-- Story title — external URL opens in new tab --%>
        <asp:HyperLink ID="lnkTitle" runat="server"
            CssClass="hn-title fw-semibold text-decoration-none text-dark"
            Target="_blank" />
        <%-- Domain hint --%>
        <small class="text-muted ms-1">
            (<asp:Literal ID="litDomain" runat="server" />)
        </small>

        <%-- Metadata line --%>
        <div class="hn-meta small text-muted mt-1">
            <asp:Literal ID="litTimeAgo" runat="server" />
            &nbsp;by&nbsp;
            <asp:LinkButton ID="lnkAuthor" runat="server"
                CssClass="text-muted hn-author-link"
                OnClick="lnkAuthor_Click" />
            &nbsp;|&nbsp;
            <asp:LinkButton ID="lnkComments" runat="server"
                CssClass="text-muted hn-comments-link"
                OnClick="lnkComments_Click" />
        </div>
    </div>

</div>
