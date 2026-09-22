// Takes the routes away from the pages under Pages/Pruefung/ outside
// development. Those pages exist to make a behaviour visible that would
// otherwise only be claimed by a test; they are not part of the application an
// owner uses, so in a deployed application they have no address at all.

using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ReviewMyDoc.Web.Security;

/// <summary>
/// Removes every route of the check pages, so they answer 404 like a page that
/// does not exist.
/// </summary>
/// <remarks>
/// The route is taken away rather than the page being deleted from the build:
/// the page has to stay compiled, because the integration tests render it, and
/// a page that is compiled but unreachable cannot be reached by accident either.
/// Program.cs adds this convention only outside development, which is the one
/// place that decides.
/// </remarks>
public sealed class ProbePagesConvention : IPageRouteModelConvention
{
    /// <summary>The folder whose pages this convention switches off.</summary>
    public const string Folder = "/Pruefung/";

    /// <summary>Clears the routes of a page in <see cref="Folder"/>.</summary>
    /// <param name="model">The routes of one page.</param>
    public void Apply(PageRouteModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.ViewEnginePath.StartsWith(Folder, StringComparison.Ordinal))
        {
            model.Selectors.Clear();
        }
    }
}
