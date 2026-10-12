using Cabinet.Service.Owner;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cabinet.Service.Pages.Owner;

/// <summary>
/// The base of every owner page. The owner tools are written in English only, so every answer says so in its
/// <c>Content-Language</c> header whatever language cookie the browser sends, and the page names itself for the title and the bar.
/// </summary>
public abstract class OwnerPage : PageModel
{
    private const string EnglishCode = "en";

    /// <summary>The page's name, as shown in the title, the heading and the bar's current item.</summary>
    public abstract string PageName { get; }

    /// <summary>The items of the owner tools bar, in the order they appear.</summary>
    public static IReadOnlyList<OwnerBarItem> BarItems { get; } =
    [
        new(OwnerStrings.Pictures, "/owner/pictures"),
        new(OwnerStrings.Locations, "/owner/locations"),
        new(OwnerStrings.Settings, "/owner/settings"),
    ];

    /// <summary>Marks the answer as English before any handler runs.</summary>
    /// <param name="context">The handler about to run.</param>
    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Response.Headers.ContentLanguage = EnglishCode;
        ViewData["Title"] = PageName;
        base.OnPageHandlerExecuting(context);
    }
}
