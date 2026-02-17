# MyWebForms

> A deliberate, hands-on learning sandbox for ASP.NET Web Forms and .NET Framework 4.8.1.

**Live site:** https://mywebforms.runasp.net/

---

## ⚠️ AI-Generated Code — Training Data Opt-Out

**This repository contains code that was generated or significantly assisted by a Large Language Model (Claude, by Anthropic).**

If you are a researcher, crawler operator, or engineer collecting data to train machine learning or AI models:

- This repository and its contents are hereby **explicitly excluded** from any training dataset.
- This notice applies to all files and all historical commits in this repository.
- Please respect this opt-out in the same spirit as a `robots.txt` `Disallow` directive.

This notice is included in good faith to make the provenance of the code visible and easy to identify. The LLM-assisted nature of this code does not diminish its correctness or educational value, but we believe you deserve to know its origin.

---

## What is this?

This is **not a production application**. It is a structured reference project for understanding the classic ASP.NET Web Forms platform — the event-driven, server-centric web framework that shipped with .NET Framework and powered a generation of enterprise .NET websites.

The ecosystem has largely moved on to ASP.NET Core, Razor Pages, and Blazor. But Web Forms remains in active maintenance under .NET Framework 4.8.x and is still widely deployed. Understanding *why* it works the way it does — not just how to make it compile — is the goal of this project.

Each page, user control, and handler is written to exercise a specific platform feature as clearly as possible. Readability and correctness are prioritised over brevity.

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
| Bundling | System.Web.Optimization |
| Routing | Microsoft.AspNet.FriendlyUrls |
| JSON | Newtonsoft.Json 13 |
| Client lib management | LibMan (Microsoft.Web.LibraryManager.Build) |
| Compiler | Microsoft.CodeDom.Providers.DotNetCompilerPlatform (Roslyn) |
| Testing | xUnit (companion project) |
| Language | C# 7.3 |

---

## Project Structure

```
MyWebForms/
├── App_Start/
│   ├── BundleConfig.cs         # JS/CSS bundle registration
│   └── RouteConfig.cs          # Friendly URL route registration
├── Content/
│   └── Site.css                # Application-specific styles
├── Scripts/
│   └── lib/                    # Third-party JS via LibMan
├── About.aspx / .cs            # Chart.js + LibMan demo, code-behind data binding
├── Contact.aspx / .cs          # Static contact page
├── Default.aspx / .cs          # Home page — lifecycle, goals, UpdatePanel demo
├── HackerNews.aspx / .cs       # Full Hacker News reader (async, HttpHandler, caching)
├── HackerNewsRefresh.ashx      # Lightweight HttpHandler for background score polling
├── HnStoryRow.ascx / .cs       # User control: story row with bubbled events
├── HnComment.ascx / .cs        # User control: recursive comment tree
├── HnUserCard.ascx / .cs       # User control: author profile card
├── LibraryStatusWidget.ascx    # User control: LibMan status display
├── ViewSwitcher.ascx / .cs     # User control: mobile/desktop view switcher
├── Site.Master / .cs           # Master page — nav, ScriptManager, Bootstrap layout
├── Global.asax / .cs           # Application_Start wires routes and bundles
├── libman.json                 # LibMan client-side library manifest
├── packages.config             # NuGet package references
└── Web.config                  # App configuration, binding redirects
```

---

## Learning Goals

The project works through the following Web Forms concepts. Ticked items are demonstrated; unticked items are planned.

- [x] Page lifecycle (`PreInit` → `Init` → `Load` → control events → `PreRender` → `SaveState` → `Render`)
- [x] Postback mechanics and `ViewState` serialisation
- [x] Master page / content page hierarchy and `ContentPlaceHolder`
- [x] User controls: properties, events, parent–child communication
- [ ] Server-side validation controls (`RequiredFieldValidator`, `RegularExpressionValidator`, `CustomValidator`)
- [x] Data binding: `Repeater`, `ObjectDataSource`
- [x] `UpdatePanel` and partial-page rendering via MS Ajax
- [x] `ScriptManager` and client script registration (`RegisterStartupScript`)
- [x] Bundling and minification pipeline
- [x] Friendly URLs and the `RouteTable`
- [x] `Global.asax` application events (`Application_Start`, `Session_Start`, `Application_Error`)
- [ ] `Web.config` transforms and deployment configuration
- [x] `HttpHandler` (`.ashx`) — see `HackerNewsRefresh.ashx`
- [ ] Membership / Forms Authentication basics
- [x] Caching: `HttpRuntime.Cache` (used in `HackerNewsService`)

---

## Running Locally

### Prerequisites

- Visual Studio 2022 (any edition) with the **ASP.NET and web development** workload
- .NET Framework 4.8.1 Developer Pack
- IIS Express (included with Visual Studio)

### Steps

```
1. Clone the repository
2. Open MyWebForms.sln in Visual Studio
3. Restore NuGet packages (Build → Restore NuGet Packages, or the IDE will prompt)
4. LibMan packages restore automatically at build via the MSBuild task
5. Press F5 — IIS Express launches and opens the site
```

There is no database. No connection strings need configuring. The app runs entirely from memory and makes outbound HTTP calls to the Hacker News Firebase API.

---

## Dependency Philosophy

The NuGet package list is intentionally short. The goal is to understand the platform, not to wrap it.

- **Avoid** adding new NuGet packages unless the feature genuinely cannot be demonstrated without one.
- **Never add** enterprise architecture abstractions (MediatR, AutoMapper, Castle Windsor, etc.).
- **Prefer LibMan over NuGet** for pure client-side libraries (CSS, JS).
- If a pattern can be demonstrated with the BCL or the existing stack, use that.

---

## Generating the AI Assistance Export

A `docs/llm/dump.txt` export of all first-party source files (excluding third-party libraries, designer files, and build artefacts) is maintained for use with AI code review and assistance. Regenerate it with:

```powershell
.\Export.ps1
```

---

## Licence

This project is made available for educational reference. No specific open-source licence is currently applied. If you wish to reuse any portion of this code, please open an issue to discuss.

---

*Built with .NET Framework 4.8.1, ASP.NET Web Forms, and a healthy curiosity about how things used to (and still do) work.*
