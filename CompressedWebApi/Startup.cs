using System.Web.Http;
using CompressedWebApi.Handlers;
using Owin;

namespace CompressedWebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.MessageHandlers.Add(new ResponseCompressionHandler());

            appBuilder.UseWebApi(config);
        }
    }
}