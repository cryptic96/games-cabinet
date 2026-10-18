namespace Cabinet.BrowserTests.Infrastructure;

/// <summary>
/// The viewport widths, in CSS pixels, that every test of the desktop filter dock uses, kept in one place so moving a breakpoint is
/// one edit. Below 64rem (1024px) there is no dock and the page has the slim bar and the Filters sheet; from 64rem up to 90rem
/// (1440px) the dock's open panel lies over the cabinet; from 90rem the open panel stands beside the cabinet and pushes it aside.
/// </summary>
public static class DockWidths
{
    /// <summary>A phone: no dock, the slim bar and the Filters sheet.</summary>
    public const int Phone = 390;

    /// <summary>A tablet: still no dock, the slim bar and the Filters sheet.</summary>
    public const int Tablet = 768;

    /// <summary>The narrowest desktop: the dock is in use and its open panel lies over the cabinet.</summary>
    public const int NarrowestDesktop = 1024;

    /// <summary>A common laptop width: the dock is in use and its open panel lies over the cabinet.</summary>
    public const int Over = 1280;

    /// <summary>The narrowest width at which the dock's open panel stands beside the cabinet and pushes it aside.</summary>
    public const int Push = 1440;

    /// <summary>A wide desktop: the dock's open panel stands beside the cabinet and pushes it aside.</summary>
    public const int Wide = 1920;
}
