using System.Web.Optimization;
using System.Web.UI;

namespace MyWebForms
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            RegisterScriptManagerMappings();

            // ── MS Ajax bundle ──────────────────────────────────────────────
            // Order matters: MicrosoftAjax.js must load before the extension
            // scripts that depend on the Sys namespace it defines.
            bundles.Add(new ScriptBundle("~/bundles/MsAjaxJs").Include(
                "~/Scripts/WebForms/MSAjax/MicrosoftAjax.js",
                "~/Scripts/WebForms/MSAjax/MicrosoftAjaxApplicationServices.js",
                "~/Scripts/WebForms/MSAjax/MicrosoftAjaxTimer.js",
                "~/Scripts/WebForms/MSAjax/MicrosoftAjaxWebForms.js"));

            // ── WebForms helper scripts ─────────────────────────────────────
            // Provides GridView, validation, menus, etc.
            bundles.Add(new ScriptBundle("~/bundles/WebFormsJs").Include(
                "~/Scripts/WebForms/WebForms.js",
                "~/Scripts/WebForms/WebUIValidation.js",
                "~/Scripts/WebForms/MenuStandards.js",
                "~/Scripts/WebForms/Focus.js",
                "~/Scripts/WebForms/GridView.js",
                "~/Scripts/WebForms/DetailsView.js",
                "~/Scripts/WebForms/TreeView.js",
                "~/Scripts/WebForms/WebParts.js"));

            // ── Modernizr ───────────────────────────────────────────────────
            // Rendered in <head> so the browser feature-detects before the
            // page body is parsed. Keep it separate from other bundles.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                "~/Scripts/modernizr-*"));
        }

        /// <summary>
        /// Maps named script references so that <asp:ScriptManager> can resolve
        /// them to local bundle paths rather than falling back to the built-in
        /// web resources embedded in System.Web.
        ///
        /// Without these mappings ScriptManager would attempt to locate e.g.
        /// "MsAjaxBundle" as an assembly web resource (WebResourceUtil) and
        /// throw InvalidOperationException: "Assembly does not contain a Web
        /// resource with name 'ScriptManager.js'".
        /// </summary>
        private static void RegisterScriptManagerMappings()
        {
            // jquery ─────────────────────────────────────────────────────────
            // Referenced by Name="jquery" in Site.Master's ScriptManager.
            ScriptManager.ScriptResourceMapping.AddDefinition("jquery",
                new ScriptResourceDefinition
                {
                    Path = "~/Scripts/jquery-3.7.1.min.js",
                    DebugPath = "~/Scripts/jquery-3.7.1.js",
                    CdnPath = "https://ajax.aspnetcdn.com/ajax/jQuery/jquery-3.7.1.min.js",
                    CdnDebugPath = "https://ajax.aspnetcdn.com/ajax/jQuery/jquery-3.7.1.js"
                });

            // MsAjaxBundle ───────────────────────────────────────────────────
            // Referenced by Name="MsAjaxBundle" in Site.Master's ScriptManager.
            // Points to the bundle registered above; ScriptManager will emit
            // a <script> tag for the bundle URL.
            ScriptManager.ScriptResourceMapping.AddDefinition("MsAjaxBundle",
                new ScriptResourceDefinition
                {
                    Path = "~/bundles/MsAjaxJs",
                    DebugPath = "~/bundles/MsAjaxJs"
                });

            // WebForms ───────────────────────────────────────────────────────
            // Referenced by Name="WebForms" in Site.Master's ScriptManager.
            ScriptManager.ScriptResourceMapping.AddDefinition("WebForms",
                new ScriptResourceDefinition
                {
                    Path = "~/bundles/WebFormsJs",
                    DebugPath = "~/bundles/WebFormsJs"
                });
        }
    }
}
