using System.Web;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls;

namespace MyWebForms
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            // ── API / handler routes ──────────────────────────────────────────────
            // Register the HN background-refresh handler BEFORE FriendlyUrls
            // so it is matched first and not mistaken for a page route.
            routes.Add("HnRefresh", new Route(
                "hn-refresh",
                new HandlerRouteHandler<HackerNewsRefresh>()));

            // ── Explicit page routes (registered before FriendlyUrls) ─────────────
            // FriendlyUrls resolves HnLive.aspx → /HnLive automatically on IIS
            // Express (dev), but some shared-hosting environments (e.g. runasp.net)
            // run under IIS with a different request pipeline configuration and the
            // extensionless URL may not reach the ASP.NET handler unless an explicit
            // PageRouteHandler entry is present.
            //
            // Educational note — PageRouteHandler:
            //   System.Web.Routing.PageRouteHandler maps a route pattern to a
            //   physical .aspx file.  It is the same mechanism FriendlyUrls uses
            //   internally, but explicit registration guarantees the mapping even
            //   when the hosting pipeline does not enable extensionless URLs by
            //   default.
            routes.MapPageRoute("HnLive", "HnLive", "~/HnLive.aspx");
            routes.MapPageRoute("HackerNewsPage", "HackerNews", "~/HackerNews.aspx");

            // ── FriendlyUrls (covers everything else) ────────────────────────────
            var settings = new FriendlyUrlSettings();
            settings.AutoRedirectMode = RedirectMode.Permanent;
            routes.EnableFriendlyUrls(settings);
        }
    }

    /// <summary>
    /// Generic IRouteHandler adapter that wraps any IHttpHandler as a route.
    ///
    /// Educational note:
    ///   ASP.NET routing (System.Web.Routing) decouples URL matching from
    ///   request handling.  IRouteHandler.GetHttpHandler() is called for each
    ///   matched request and returns the IHttpHandler that will process it.
    ///   Using a generic type parameter keeps the adapter reusable for any
    ///   handler without additional plumbing.
    /// </summary>
    internal sealed class HandlerRouteHandler<T> : IRouteHandler
        where T : IHttpHandler, new()
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return new T();
        }
    }
}
