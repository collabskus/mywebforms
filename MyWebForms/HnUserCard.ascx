<%@ Control Language="C#" AutoEventWireup="true"
    CodeBehind="HnUserCard.ascx.cs"
    Inherits="MyWebForms.HnUserCard" %>

<%--
    HnUserCard.ascx
    ---------------
    Displays a Hacker News user profile card.
    The User property must be set by the host page before Page_Load runs.
--%>

<div class="card shadow-sm">
    <div class="card-header bg-warning text-dark d-flex align-items-center gap-2">
        <strong>&#128100;</strong>
        <span>
            <asp:Literal ID="litUsername" runat="server" />
        </span>
        <span class="ms-auto badge bg-dark">
            <asp:Literal ID="litKarma" runat="server" /> karma
        </span>
    </div>
    <div class="card-body">
        <dl class="row mb-0 small">
            <dt class="col-sm-4">Member since</dt>
            <dd class="col-sm-8">
                <asp:Literal ID="litMemberSince" runat="server" />
            </dd>

            <dt class="col-sm-4">Submissions</dt>
            <dd class="col-sm-8">
                <asp:Literal ID="litSubmissionCount" runat="server" />
            </dd>

            <asp:Panel ID="pnlAbout" runat="server">
                <dt class="col-sm-4">About</dt>
                <dd class="col-sm-8 hn-user-about">
                    <asp:Literal ID="litAbout" runat="server" Mode="PassThrough" />
                </dd>
            </asp:Panel>
        </dl>
    </div>
    <div class="card-footer text-end">
        <a href="https://news.ycombinator.com/user?id=<%= EncodedUsername %>"
           class="btn btn-sm btn-outline-warning" target="_blank">
            View on HN &#8599;
        </a>
    </div>
</div>
