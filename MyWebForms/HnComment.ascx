<%@ Control Language="C#" AutoEventWireup="True"
    CodeBehind="HnComment.ascx.cs"
    Inherits="MyWebForms.HnComment" %>

<%--
    HnComment.ascx
    --------------
    Renders a single comment with visual depth indentation.
    The Depth property controls the left-margin indent level.
    HTML from the API is rendered as-is (it is already escaped by HN).

    The API returns comment text as HTML (with <p>, <pre>, <code>, <a> tags).
    We render it with <%# %> eval inside a Literal so it is output verbatim —
    this is intentional and safe because the source is the HN API, not
    user-supplied input arriving at our server.
--%>

<div class="hn-comment" style="margin-left: <%= DepthPx %>px;">
    <div class="card border-0 bg-light mb-2">
        <div class="card-body py-2 px-3">
            <div class="small text-muted mb-1">
                <asp:LinkButton ID="lnkAuthor" runat="server"
                    CssClass="fw-semibold text-secondary hn-author-link"
                    OnClick="lnkAuthor_Click" />
                &nbsp;&middot;&nbsp;
                <asp:Literal ID="litTimeAgo" runat="server" />
            </div>
            <div class="hn-comment-text small">
                <asp:Literal ID="litText" runat="server" Mode="PassThrough" />
            </div>
        </div>
    </div>
</div>
