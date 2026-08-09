using UnityEngine;

// The design system, in one place. Every screen pulls colour, size and sprite
// from here so the menus stay consistent instead of drifting per-scene.
//
// Palette values are lifted from in-game assets: the grass in shadow, the crate
// timber, the coin, and the two team flags.
public static class GameUI
{
    // ---- ground / structure ----
    // Supercell-style chrome: deep saturated blue panels on a lighter blue field, with a
    // near-black navy outline on everything. The previous green was lifted from the arena
    // grass, which made the UI read as more arena rather than as a layer above it.
    public static readonly Color Ground   = Hex("15274F");
    public static readonly Color Panel    = Hex("1E3F7D");
    public static readonly Color PanelHi  = Hex("2E5AA8");
    public static readonly Color Outline  = Hex("0E1730");

    // ---- action ----
    // Exactly one gold CTA per screen. Everything secondary stays blue - the moment a
    // second gold button appears, neither reads as the primary action any more.
    public static readonly Color Crate    = Hex("FFC93C");   // primary CTA
    public static readonly Color CrateDk  = Hex("C08A16");
    public static readonly Color Go       = Hex("4CD964");   // confirm
    public static readonly Color GoDk     = Hex("2E9E45");
    public static readonly Color Coin     = Hex("FFC63D");
    public static readonly Color CoinDk   = Hex("C08A16");

    // ---- team identity (never used for buttons) ----
    public static readonly Color Red      = Hex("F2564B");
    public static readonly Color RedDk    = Hex("B3352C");
    public static readonly Color Blue     = Hex("3D8BF5");
    public static readonly Color BlueDk   = Hex("2560B0");

    // ---- text ----
    // Pure white over the navy outline, rather than cream: the warm cast that read well on
    // green turns muddy on blue.
    public static readonly Color Cream    = Hex("FFFFFF");
    public static readonly Color CreamDim = Hex("B9C7E0");

    // ---- type scale (canvas is 1920x1080) ----
    public const int TitleSize   = 96;
    public const int NumberSize  = 62;
    public const int ButtonSize  = 40;
    public const int ItemSize    = 32;
    public const int BodySize    = 24;
    public const int LabelSize   = 20;

    // ---- metrics ----
    public const float ButtonEdge     = 10f;  // solid bottom edge, the genre's 3D tell
    public const float ButtonEdgeSm   = 8f;
    public const float ScreenMargin   = 48f;
    public const float MinTouchTarget = 88f;

    public static Color Darken(Color c, float amount = 0.30f) =>
        new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);

    public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    public static Color TeamColor(Team t) => t == Team.Red ? Red : Blue;

    static Color Hex(string hex)
    {
        return new Color(
            int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            1f);
    }

    // ---- shared sprites, generated once into Resources/UI ----
    static Sprite roundFill, roundOutline, pillFill, pillOutline, circleFill, circleOutline;

    public static Sprite RoundFill    => roundFill    ??= Resources.Load<Sprite>("UI/ui_round_fill");
    public static Sprite RoundOutline => roundOutline ??= Resources.Load<Sprite>("UI/ui_round_outline");
    public static Sprite PillFill     => pillFill     ??= Resources.Load<Sprite>("UI/ui_pill_fill");
    public static Sprite PillOutline  => pillOutline  ??= Resources.Load<Sprite>("UI/ui_pill_outline");
    public static Sprite CircleFill   => circleFill   ??= Resources.Load<Sprite>("UI/ui_circle_fill");
    public static Sprite CircleOutline=> circleOutline??= Resources.Load<Sprite>("UI/ui_circle_outline");
}
