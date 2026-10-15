using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cabinet.Service.Owner;

/// <summary>
/// Turns the result of an owner change into the answer the browser gets. A script asks for JSON and reads the outcome, the
/// new revision and whether Undo is possible; a form without scripts is sent back to the same page and row with a short
/// code that the page turns into words. The redirect goes to a page path the handler names in code and a row anchor the
/// server builds, and the code comes from a fixed set, so nothing the request carried is ever echoed or followed.
/// </summary>
public static class OwnerResponses
{
    private const string JsonMediaType = "application/json";

    private const string RefusedPrefix = "refused-";

    private const string FallbackRefusal = "save-failed";

    private const string LinkRemovedKey = "link-removed";

    private static readonly HashSet<string> RefusalKeys = new(
        [
            "name-empty",
            "name-too-long",
            "name-taken",
            "too-many-locations",
            "name-characters",
            "unknown-location",
            "unknown-game",
            "no-picture",
            "no-link",
            "link-expired",
            "nothing-selected",
            "confirm-needed",
            "id-clash",
            "unknown-look",
            "save-failed",
            .. Enum.GetValues<LinkProblem>().Select(OwnerStrings.LinkKey),
        ],
        StringComparer.Ordinal);

    /// <summary>Answers a post with the result of the change it asked for.</summary>
    /// <param name="page">The page that handled the post.</param>
    /// <param name="result">What the writer answered.</param>
    /// <param name="pagePath">The page to send a form back to, as the page's own path, written in code.</param>
    /// <param name="anchor">The row to land on, built by the server.</param>
    /// <param name="createdId">The identifier of what the change created, which a script needs to build its row; null when nothing was created.</param>
    /// <param name="extra">More facts a script needs from the answer, such as the counts a page shows; they are only part of the JSON answer.</param>
    /// <param name="view">The view of the page to send a form back to, one of a fixed set the page maps in code; null for the page's default view.</param>
    /// <param name="game">The game whose single-game view a form is sent back to, which the server has looked up in the collection; null for the page's list.</param>
    public static IActionResult For(
        PageModel page,
        OwnerResult result,
        string pagePath,
        string anchor,
        string? createdId = null,
        IReadOnlyDictionary<string, object?>? extra = null,
        string? view = null,
        int? game = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrEmpty(pagePath);
        ArgumentException.ThrowIfNullOrEmpty(anchor);

        if (AcceptsJson(page.Request))
        {
            var body = new Dictionary<string, object?>
            {
                ["outcome"] = JsonName(result.Outcome),
                ["revision"] = result.Data?.Revision ?? 0,
                ["status"] = result.StatusKey,
                ["canUndo"] = result.CanUndo,
                ["message"] = OwnerStrings.Message(result.StatusKey),
            };

            if (createdId is not null)
            {
                body["id"] = createdId;
            }

            foreach (var (name, value) in extra ?? new Dictionary<string, object?>())
            {
                body[name] = value;
            }

            return new JsonResult(body)
            {
                StatusCode = (int)StatusOf(result.Outcome),
            };
        }

        return page.RedirectToPage(pagePath, pageHandler: null, routeValues: new { done = DoneCode(result), view, game }, fragment: anchor);
    }

    /// <summary>The code a redirect carries for a result; always one of a fixed set.</summary>
    /// <param name="result">What the writer answered.</param>
    public static string DoneCode(OwnerResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome switch
        {
            OwnerOutcome.Saved => result.StatusKey switch
            {
                "undone" => "undone",
                LinkRemovedKey => LinkRemovedKey,
                _ => "saved",
            },
            OwnerOutcome.NoChange => "unchanged",
            OwnerOutcome.OutOfDate => result.StatusKey == "cant-undo" ? "cant-undo" : "out-of-date",
            OwnerOutcome.Unreadable => "unreadable",
            _ => RefusedPrefix + (RefusalKeys.Contains(result.StatusKey) ? result.StatusKey : FallbackRefusal),
        };
    }

    /// <summary>The words for a code a redirect carried, or null when the code is not one of the fixed set.</summary>
    /// <param name="done">The query value of the page.</param>
    public static string? NoticeFor(string? done) => StatusKeyOf(done) is { } key ? OwnerStrings.Message(key) : null;

    /// <summary>The problem with a pasted address that a code a redirect carried stands for, or null when it stands for anything else.</summary>
    /// <param name="done">The query value of the page.</param>
    public static LinkProblem? LinkProblemOf(string? done) =>
        done is not null && done.StartsWith(RefusedPrefix, StringComparison.Ordinal)
            ? OwnerStrings.LinkProblemOf(done[RefusedPrefix.Length..])
            : null;

    /// <summary>Whether a code a redirect carried says something went wrong.</summary>
    /// <param name="done">The query value of the page.</param>
    public static bool IsProblem(string? done) => StatusKeyOf(done) is { } key && key is not ("game-moved" or "unchanged" or "undone" or LinkRemovedKey);

    private static string? StatusKeyOf(string? done)
    {
        if (done is null)
        {
            return null;
        }

        if (done.StartsWith(RefusedPrefix, StringComparison.Ordinal))
        {
            var key = done[RefusedPrefix.Length..];

            return RefusalKeys.Contains(key) ? key : null;
        }

        return done switch
        {
            "saved" => "game-moved",
            "unchanged" => "unchanged",
            "undone" => "undone",
            LinkRemovedKey => LinkRemovedKey,
            "out-of-date" => "out-of-date",
            "cant-undo" => "cant-undo",
            "unreadable" => "unreadable",
            _ => null,
        };
    }

    /// <summary>Whether the request asks for a JSON answer, as a script's request does.</summary>
    /// <param name="request">The request.</param>
    public static bool AsksForJson(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AcceptsJson(request);
    }

    private static bool AcceptsJson(HttpRequest request) =>
        request.Headers.Accept.Any(value => value is not null && value.Contains(JsonMediaType, StringComparison.OrdinalIgnoreCase));

    private static string JsonName(OwnerOutcome outcome)
    {
        var name = outcome.ToString();

        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }

    private static HttpStatusCode StatusOf(OwnerOutcome outcome) => outcome switch
    {
        OwnerOutcome.Saved or OwnerOutcome.NoChange => HttpStatusCode.OK,
        OwnerOutcome.OutOfDate or OwnerOutcome.Unreadable => HttpStatusCode.Conflict,
        _ => HttpStatusCode.UnprocessableEntity,
    };
}
