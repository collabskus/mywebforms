# MyWebForms — Project Instructions

## Purpose

This project exists as a **personal learning and reference sandbox** for refreshing and deepening knowledge of:

- **.NET Framework 4.8.1** — runtime, CLR, BCL, compilation pipeline
- **ASP.NET Web Forms** — page lifecycle, postback model, `ViewState`, server controls, master pages, user controls (`.ascx`), code-behind pattern, `Global.asax`, `RouteConfig`, `BundleConfig`
- **Classic ASP.NET patterns** — `ScriptManager`, `UpdatePanel`, URL routing with Friendly URLs, Web Optimization bundling/minification
- **Best practices for the above**, even where the ecosystem has moved on — the goal is to understand *why* things work the way they do, not just make them work

This is not a production application. It is a deliberate, hands-on reference project. Code should be written to **demonstrate and exercise** the underlying platform features clearly, favouring readability and correctness over brevity.

---

## Project Structure

```
MyWebForms/
├── App_Start/
│   ├── BundleConfig.cs       # JS/CSS bundle registration
│   └── RouteConfig.cs        # Friendly URL route registration
├── Content/
│   └── Site.css              # Custom application styles only
├── Scripts/
│   └── lib/                  # Third-party JS (e.g. Chart.js via LibMan)
├── About.aspx / .cs          # Demo page: LibMan, Chart.js, code-behind data binding
├── Contact.aspx / .cs        # Static contact page
├── Default.aspx / .cs        # Home page
├── LibraryStatusWidget.ascx / .cs   # User control demo
├── ViewSwitcher.ascx / .cs          # Mobile/desktop view switcher
├── Site.Master / .cs         # Master page with nav, ScriptManager, Bootstrap layout
├── Global.asax / .cs         # Application startup (routes, bundles)
├── libman.json               # LibMan config for client-side library acquisition
├── packages.config           # NuGet package references
└── Web.config                # App configuration, compiler settings, binding redirects
```

---

## Technology Stack

| Concern | Technology |
|---|---|
| Framework | .NET Framework 4.8.1 |
| Web platform | ASP.NET Web Forms |
| UI framework | Bootstrap 5.3 (NuGet) |
| CSS animation | Animate.css 4.1 (LibMan / cdnjs) |
| Charting | Chart.js 4.4 (LibMan / cdnjs) |
| jQuery | 3.7.1 (NuGet) |
| Bundling | System.Web.Optimization (Microsoft.AspNet.Web.Optimization) |
| Routing | Microsoft.AspNet.FriendlyUrls |
| JSON | Newtonsoft.Json 13 |
| Client lib management | LibMan (Microsoft.Web.LibraryManager.Build) |
| Compiler | Microsoft.CodeDom.Providers.DotNetCompilerPlatform (Roslyn for .NET Framework) |

---

## Dependency Philosophy

> **Keep the NuGet package list short and meaningful.**

- **Avoid adding new NuGet packages** unless the feature genuinely cannot be demonstrated without one, or the package is a direct Microsoft/framework extension (e.g. `Microsoft.AspNet.*`)
- **Never add** packages that are primarily enterprise architecture abstractions: no MediatR, no AutoMapper, no Moq, no Castle Windsor, no StructureMap, no MassTransit
- **Never add** packages with restrictive or non-standard licences without explicit review
- **Prefer LibMan over NuGet** for pure client-side libraries (CSS, JS). NuGet is for server-side .NET dependencies
- If a pattern can be demonstrated with the BCL or the existing stack, use that — the point is to learn the platform, not to wrap it

### Acceptable future additions
- `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` — for a companion test project
- `Microsoft.AspNet.*` packages that extend the core Web Forms/MVC surface
- Nothing else without a documented reason in this file

---

## Coding Conventions

### General
- Language: **C# only**
- Target: **.NET Framework 4.8.1** — do not use .NET Core / .NET 5+ APIs even if they appear available
- Namespaces: `MyWebForms` root namespace throughout
- Code-behind files (`.aspx.cs`, `.ascx.cs`) should be kept **thin** — logic beyond simple UI wiring belongs in a separate class or service method
- Designer files (`*.designer.cs`) are auto-generated — never edit manually

### Web Forms specifics
- Use `<%: %>` (HTML-encoded output) by default; only use `<%= %>` when raw output is intentional and safe
- Prefer `IsPostBack` guards in `Page_Load` to avoid redundant initialisation on postbacks
- `ViewState` should be understood and used deliberately — disable it at the control or page level when it adds no value
- Master pages provide the shared layout; content pages must not re-declare `<html>`, `<head>`, or `<body>`
- User controls (`.ascx`) are the Web Forms component model — use them to encapsulate reusable UI + logic

### Bundling and scripts
- Scripts and styles are registered in `BundleConfig.cs` and rendered via `Scripts.Render` / `Styles.Render` or `webopt:BundleReference`
- The `ScriptManager` in `Site.Master` owns all framework script delivery — do not add `<script>` tags for framework scripts in individual pages
- Page-specific scripts belong at the bottom of the content area inside `<asp:Content>`, not in `<head>`
- Chart.js and other LibMan libraries are referenced via `ResolveUrl` from their destination paths (see `Site.Master`)

### Configuration
- `Web.config` is the single source of truth for app config, connection strings, and binding redirects
- `Web.Debug.config` / `Web.Release.config` are transform files — use them for environment-specific overrides, not `Web.config` directly
- Do not store secrets in any config file committed to source control

---

## Testing

- Unit tests live in a **separate class library project** (e.g. `MyWebForms.Tests`) targeting .NET Framework 4.8.1
- Test framework: **xUnit** (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) — no other test framework
- No mocking frameworks — write simple fakes and test doubles by hand where isolation is needed; this is good practice and keeps dependencies minimal
- Test coverage focus: pure C# logic extracted from code-behind, not UI rendering

---

## Learning Goals Checklist

Use this project to explore and document the following. Add new pages, controls, or examples as needed:

- [ ] Page lifecycle (`PreInit` → `Init` → `Load` → `Validate` → `LoadComplete` → `PreRender` → `SaveState` → `Render`)
- [ ] Postback mechanics and `ViewState` serialisation
- [ ] Master page / content page hierarchy and `ContentPlaceHolder`
- [ ] User controls: properties, events, parent-child communication
- [ ] Server-side validation controls (`RequiredFieldValidator`, `RegularExpressionValidator`, `CustomValidator`)
- [ ] Data binding: `GridView`, `DetailsView`, `Repeater`, `ObjectDataSource`
- [ ] `UpdatePanel` and partial-page rendering via MS Ajax
- [ ] `ScriptManager` and client script registration (`RegisterStartupScript`, `RegisterClientScriptBlock`)
- [ ] Bundling and minification pipeline
- [ ] Friendly URLs and the `RouteTable`
- [ ] `Global.asax` application events (`Application_Start`, `Session_Start`, `Application_Error`)
- [ ] `Web.config` transforms and deployment configuration
- [ ] HttpModules and HttpHandlers (`.ashx`)
- [ ] Membership / Forms Authentication basics (without external providers)
- [ ] Caching: output cache (`<%@ OutputCache %>`) and `HttpRuntime.Cache`

---

## LLM / AI Assistance Notes

A `docs/llm/dump.txt` export of the project source (excluding third-party libraries) is maintained for use with AI code review and assistance. Regenerate it by running `Export.ps1` from the repo root:

```powershell
.\Export.ps1
```

The export script excludes: Bootstrap, jQuery, Modernizr, Animate.css, WebForms/MSAjax built-in scripts, Chart.js, designer files, and the `packages/` and `bin/obj` build artefacts. Only first-party application code is included.

Note to Claude: 
Please read the FULL dump.txt in its entirety for each prompt. 
Do not skim it. 
Do not search for keywords in it. 
Do not retrieve parts from it. 
It isn't that big. 
Read the whole thing. 
And please generate FULL files for any file that needs to change 
Please do not hallucinate. 
Take some more time up front to save time and effort for the humans. 
When in doubt, use your best judgment. 
Use best engineering practices
within the limits of this project. 
Remember, we can only use C Sharp version 7.3 
We cannot use C Sharp 8 or above. 
