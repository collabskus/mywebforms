<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="LibraryStatusWidget.ascx.cs" Inherits="MyWebForms.LibraryStatusWidget" %>

<div class="card border-0 bg-light mb-3 animate__animated animate__fadeIn">
    <div class="card-body">
        <h6 class="card-title d-flex justify-content-between">
            <asp:Literal ID="litLibraryName" runat="server" />
            <span class="badge bg-info text-dark"><asp:Literal ID="litVersion" runat="server" /></span>
        </h6>
        
        <div class="progress" style="height: 10px;">
            <div id="progressBar" runat="server" 
                 class="progress-bar progress-bar-striped progress-bar-animated" 
                 role="progressbar" 
                 style="width: 0%"></div>
        </div>
        
        <small class="text-muted mt-2 d-block">
            Status: <asp:Label ID="lblStatus" runat="server" Text="Active" />
        </small>
    </div>
</div>

<script>
    // Self-contained logic: Trigger an extra animation when this specific control loads
    $(document).ready(function () {
        $('.progress-bar').each(function() {
            var targetWidth = $(this).attr('aria-valuenow');
            $(this).animate({ width: targetWidth + '%' }, 1500);
        });
    });
</script>
