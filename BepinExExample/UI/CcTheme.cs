using UnityEngine;

namespace CrowdControl.UI;

/// <summary>
/// Crowd Control's palette, so the in-game overlay looks like it belongs to the same product.
/// </summary>
/// <remarks>
/// Values are taken from the brand palette in <c>frontend/styles/colors.cjs</c> - the same tokens
/// the desktop app and the stream overlay are built from. Names match the source (royal, slate,
/// white, teal...) so anything that changes upstream is easy to find and update here.
/// </remarks>
public static class CcTheme
{
    private static Color Hex(int rgb, float a = 1f) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

    // Surfaces - the deep purple-navy the app's cards sit on
    public static readonly Color Slate800 = Hex(0x1A172A);
    public static readonly Color Slate700 = Hex(0x212136);
    public static readonly Color Slate600 = Hex(0x282943);
    public static readonly Color Slate500 = Hex(0x2D304D);
    public static readonly Color Slate400 = Hex(0x323353);

    // Brand purple, used for accents and progress
    public static readonly Color Royal50 = Hex(0x767BF4);
    public static readonly Color Royal100 = Hex(0x5B52C0);
    public static readonly Color Royal200 = Hex(0x332772);

    // Text
    public static readonly Color White100 = Hex(0xFAFAFA);
    public static readonly Color White200 = Hex(0xCDCDE9);
    public static readonly Color White300 = Hex(0xAAAACD);

    // Status
    public static readonly Color Teal200 = Hex(0x1EE29D);   //connected
    public static readonly Color Red200 = Hex(0xEA3D6A);    //disconnected
    public static readonly Color Yellow300 = Hex(0xFFA705); //paused
    public static readonly Color Blurple200 = Hex(0x40B1F7);

    /// <summary>Same colour with its alpha scaled.</summary>
    public static Color Fade(this Color c, float alpha) => new(c.r, c.g, c.b, c.a * alpha);
}
