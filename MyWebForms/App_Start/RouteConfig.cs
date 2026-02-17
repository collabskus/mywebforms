using System.Web;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls;

namespace MyWebForms
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            // ── API / handler routes ──────────────────────────────────────────
            // Register the HN background-refresh handler BEFORE FriendlyUrls
            // so it is matched first and not mistaken for a page route.
            //
            // Educational note — IRouteHandler vs HttpHandler:
            //   RouteTable entries use IRouteHandler.  For an IHttpHandler we
            //   wrap it with a RouteHandlerAdapter that calls GetHttpHandler()
            //   on the registered handler instance.
            routes.Add("HnRefresh", new Route(
                "hn-refresh",
                new HandlerRouteHandler<HackerNewsRefresh>()));

            // ── FriendlyUrls ──────────────────────────────────────────────────
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
