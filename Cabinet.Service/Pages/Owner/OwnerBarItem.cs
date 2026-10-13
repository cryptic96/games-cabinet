namespace Cabinet.Service.Pages.Owner;

/// <summary>One link of the owner tools bar.</summary>
/// <param name="Name">The link's text, which is also the name of the page it leads to.</param>
/// <param name="Href">The path of the page, from the site root.</param>
public sealed record OwnerBarItem(string Name, string Href);
