using System.Numerics;

namespace NamazuFlippers.UI;

/// <summary>
/// UI color palette per 04-UI-SPEC.md (locked Vector4 values).
/// Used by DailyRouteWindow and ConfigWindow. Do not deviate from 04-UI-SPEC.md.
/// </summary>
public static class UiColors
{
    public static readonly Vector4 GilGold       = new(1.0f, 0.85f, 0.1f, 1.0f);
    public static readonly Vector4 PurchaseCyan  = new(0.2f, 0.85f, 0.9f, 1.0f);
    public static readonly Vector4 VendorCyan    = new(0.2f, 0.85f, 0.9f, 1.0f);
    public static readonly Vector4 OosOrange     = new(1.0f, 0.55f, 0.1f, 1.0f);
    public static readonly Vector4 StaleAmber    = new(0.9f, 0.7f,  0.1f, 1.0f);
    public static readonly Vector4 ErrorRed      = new(0.9f, 0.2f,  0.2f, 1.0f);
    public static readonly Vector4 SuccessGreen  = new(0.2f, 0.8f,  0.3f, 1.0f);
    public static readonly Vector4 CompletedGray = new(0.5f, 0.5f,  0.5f, 0.7f);
    public static readonly Vector4 CacheBlue     = new(0.4f, 0.7f,  1.0f, 1.0f);
}
