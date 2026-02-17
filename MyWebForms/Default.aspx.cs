using System;
using System.Collections.Generic;
using System.Web.UI;

namespace MyWebForms
{
    /// <summary>
    /// Home page code-behind.
    ///
    /// Demonstrates:
    ///   - IsPostBack guard (init data only on first request)
    ///   - ViewState for persisting lightweight state across postbacks
    ///   - Repeater data binding with a typed list of POCOs
    ///   - UpdatePanel partial-page rendering (wired up in markup)
    ///   - Surfacing calculated properties to <%: %> expressions in markup
    /// </summary>
    public partial class _Default : Page
    {
        // ── ViewState helpers ────────────────────────────────────────────────

        /// <summary>Number of UpdatePanel partial-postback clicks this session.</summary>
        private int ClickCount
        {
            get { return (int)(ViewState["ClickCount"] ?? 0); }
            set { ViewState["ClickCount"] = value; }
        }

        /// <summary>
        /// Full-page postback counter stored in Session so it survives
        /// UpdatePanel partial postbacks (which do NOT touch Session).
        /// Contrasted with ClickCount to show the difference between
        /// ViewState (travels with the page) and Session (server-side).
        /// </summary>
        private int FullPostbackCount
        {
            get { return (int)(Session["FullPostbackCount"] ?? 0); }
            set { Session["FullPostbackCount"] = value; }
        }

        // ── Computed properties for markup expressions ───────────────────────

        protected int TotalGoalCount { get; private set; }
        protected int CompletedCount { get; private set; }
        protected int ProgressPercent { get; private set; }

        // ── Page events ──────────────────────────────────────────────────────

        protected void Page_Load(object sender, EventArgs e)
        {
            // Increment the full-page postback counter on every non-partial
            // request.  Because UpdatePanel uses a special async postback
            // mechanism, Page_Load IS called for partial postbacks too — so we
            // use ScriptManager.IsInAsyncPostBack to distinguish them.
            if (!ScriptManager.GetCurrent(this).IsInAsyncPostBack)
            {
                FullPostbackCount++;
            }

            if (!IsPostBack)
            {
                // Data binding runs only on the initial GET.  On postbacks the
                // page is rebuilt from ViewState, so rebinding would be redundant.
                BindStack();
                BindGoals();
                BindLifecycle();
            }
        }

        // ── Button handlers ──────────────────────────────────────────────────

        protected void BtnClick_Click(object sender, EventArgs e)
        {
            ClickCount++;
            // UpdatePanel.UpdateMode = Conditional means we must call Update()
            // explicitly, or the panel triggers from a child control postback.
            // Child button triggers are registered automatically, so this is
            // handled by the framework — no explicit Update() call needed here.
        }

        protected void BtnReset_Click(object sender, EventArgs e)
        {
            ClickCount = 0;
        }

        // ── Data preparation ─────────────────────────────────────────────────

        private void BindStack()
        {
            var items = new List<StackItem>
            {
                new StackItem("⚙️",  "ASP.NET Web Forms",               "Event-driven, server-centric page framework; page lifecycle, postback model, ViewState, server controls, master pages, user controls (.ascx)"),
                new StackItem("🟣",  ".NET Framework 4.8.1",            "Runtime, CLR, BCL. The final major release of the classic .NET Framework — still widely deployed."),
                new StackItem("🅱️",  "Bootstrap 5.3",                   "Responsive UI framework (via NuGet). BundleConfig registers it; Site.Master renders it."),
                new StackItem("📊",  "Chart.js 4.4",                    "Client-side charting library acquired via LibMan from cdnjs. Demonstrated on the About page."),
                new StackItem("💎",  "jQuery 3.7.1",                    "DOM utility library (via NuGet). Registered through the bundling pipeline."),
                new StackItem("✨",  "Animate.css 4.1",                 "CSS animation library acquired via LibMan. Used for entrance animations on this page."),
                new StackItem("📦",  "System.Web.Optimization",         "Microsoft's bundling and minification pipeline. BundleConfig.cs registers script/style bundles; Scripts.Render / Styles.Render emit them."),
                new StackItem("🗺️",  "Microsoft.AspNet.FriendlyUrls",   "Maps clean URL segments (e.g. /About) to .aspx files. Registered in RouteConfig.cs via Application_Start."),
                new StackItem("🔗",  "Newtonsoft.Json 13",              "JSON serialisation for API calls (e.g. Hacker News Firebase API). No new JSON abstraction — BCL JsonSerializer is .NET Core."),
                new StackItem("📥",  "LibMan",                          "Client-side library manager. Fetches Chart.js and Animate.css from cdnjs at build time; no npm/node required."),
                new StackItem("🌹",  "Roslyn (CodeDom Provider)",       "Replaces the legacy C# 5 compiler with Roslyn so we can write C# 7.3 in .NET Framework 4.8.1 projects."),
                new StackItem("🧪",  "xUnit (companion project)",       "Unit test framework for the MyWebForms.Tests class library. Thin code-behind means logic is easily testable."),
            };

            rptStack.DataSource = items;
            rptStack.DataBind();
        }

        private void BindGoals()
        {
            var goals = new List<LearningGoal>
            {
                new LearningGoal("Page lifecycle (PreInit → Render)",            true,  null),
                new LearningGoal("Postback mechanics and ViewState serialisation", true,  null),
                new LearningGoal("Master page / ContentPlaceHolder hierarchy",    true,  null),
                new LearningGoal("User controls: properties, events, parent-child comm.", true, null),
                new LearningGoal("Server-side validation controls",               false, null),
                new LearningGoal("Data binding: GridView, DetailsView, Repeater", true,  null),
                new LearningGoal("UpdatePanel and partial-page rendering",        true,  null),
                new LearningGoal("ScriptManager and client script registration",  true,  null),
                new LearningGoal("Bundling and minification pipeline",            true,  null),
                new LearningGoal("Friendly URLs and the RouteTable",              true,  null),
                new LearningGoal("Global.asax application events",                true,  null),
                new LearningGoal("Web.config transforms and deployment config",   false, null),
                new LearningGoal("HttpHandlers (.ashx)",                          true,  "~/HackerNews"),
                new LearningGoal("Membership / Forms Authentication basics",      false, null),
                new LearningGoal("Caching: OutputCache and HttpRuntime.Cache",    true,  "~/HackerNews"),
            };

            TotalGoalCount = goals.Count;
            CompletedCount = 0;
            foreach (var g in goals)
            {
                if (g.Done) CompletedCount++;
            }
            ProgressPercent = TotalGoalCount > 0
                ? (int)Math.Round((CompletedCount * 100.0) / TotalGoalCount)
                : 0;

            rptGoals.DataSource = goals;
            rptGoals.DataBind();
        }

        private void BindLifecycle()
        {
            var stages = new List<LifecycleStage>
            {
                new LifecycleStage("PreInit",           "First event. Master page is merged, theme applied, dynamic controls can be created. ViewState is NOT yet loaded."),
                new LifecycleStage("Init",              "Controls are initialised. UniqueID is assigned. Use for wiring up events before ViewState is restored."),
                new LifecycleStage("InitComplete",      "Raised after all Init events. Good point to read Request data before ViewState is applied."),
                new LifecycleStage("PreLoad",           "Raised before Page_Load. ViewState and postback data have been restored to controls."),
                new LifecycleStage("Load",              "Page_Load fires here. IsPostBack is meaningful. Data-bind controls on !IsPostBack to avoid overwriting ViewState."),
                new LifecycleStage("Control events",    "Postback event handlers fire (Button.Click, etc.) after Load. Control state is already restored from ViewState."),
                new LifecycleStage("LoadComplete",      "All Load and control events are done. Good for tasks that depend on all controls being loaded."),
                new LifecycleStage("PreRender",         "Last chance to change output before rendering. Data binding called here is reflected in HTML. RegisterStartupScript goes here."),
                new LifecycleStage("PreRenderComplete",  "Raised after all PreRender events including child controls. Use instead of Page_PreRender when child state matters."),
                new LifecycleStage("SaveStateComplete", "ViewState is serialised to the hidden __VIEWSTATE field. Do NOT modify control state after this point."),
                new LifecycleStage("Render",            "HTML is generated and written to the response stream. Not an event; override Render() to intercept raw output."),
                new LifecycleStage("Unload",            "Page is released from memory. Close connections/files here. Response is already sent — no more output."),
            };

            rptLifecycle.DataSource = stages;
            rptLifecycle.DataBind();
        }

        // ── Inner POCOs ──────────────────────────────────────────────────────

        private sealed class StackItem
        {
            public string Icon { get; private set; }
            public string Name { get; private set; }
            public string Description { get; private set; }

            public StackItem(string icon, string name, string description)
            {
                Icon = icon;
                Name = name;
                Description = description;
            }
        }

        private sealed class LearningGoal
        {
            public string Topic { get; private set; }
            public bool Done { get; private set; }
            public string Link { get; private set; }

            public LearningGoal(string topic, bool done, string link)
            {
                Topic = topic;
                Done = done;
                Link = link;
            }
        }

        private sealed class LifecycleStage
        {
            public string Event { get; private set; }
            public string Notes { get; private set; }

            public LifecycleStage(string ev, string notes)
            {
                Event = ev;
                Notes = notes;
            }
        }
    }
}
