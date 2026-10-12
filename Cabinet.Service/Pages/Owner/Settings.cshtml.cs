using Cabinet.Service.Owner;

namespace Cabinet.Service.Pages.Owner;

/// <summary>The owner's settings page. It exists only on the owner port.</summary>
public class SettingsModel : OwnerPage
{
    /// <inheritdoc />
    public override string PageName => OwnerStrings.Settings;

    /// <summary>Shows the page.</summary>
    public void OnGet()
    {
    }
}
