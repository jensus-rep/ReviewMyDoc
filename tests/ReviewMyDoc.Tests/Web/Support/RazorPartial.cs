// Renders one Razor partial of the application to a string, with the services
// of the running application behind it. It exists so the partials of the frame
// can be checked without a page of their own: the pages stay empty in this
// stage, and a partial nobody renders is a partial nobody has checked.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ReviewMyDoc.Tests.Web;

/// <summary>Renders a partial of the web application to html.</summary>
internal static class RazorPartial
{
    /// <summary>Renders the partial at <paramref name="viewPath"/>.</summary>
    /// <param name="services">Services of the application under test.</param>
    /// <param name="viewPath">Path of the view, starting at the project root.</param>
    /// <param name="model">The model the partial is rendered with.</param>
    /// <returns>The html the partial produced.</returns>
    /// <exception cref="InvalidOperationException">The view was not found.</exception>
    public static async Task<string> RenderAsync(IServiceProvider services, string viewPath, object model)
    {
        var viewEngine = services.GetRequiredService<IRazorViewEngine>();
        var tempDataProvider = services.GetRequiredService<ITempDataProvider>();

        var result = viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"The view '{viewPath}' was not found. Searched: {string.Join(", ", result.SearchedLocations ?? [])}");
        }

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        using var writer = new StringWriter();

        var viewContext = new ViewContext(
            actionContext,
            result.View,
            viewData,
            new TempDataDictionary(httpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await result.View.RenderAsync(viewContext);

        return writer.ToString();
    }
}
