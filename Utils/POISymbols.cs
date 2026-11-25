// Single source of truth for the POI symbol -> icon mapping.
// To add a new symbol: add ONE line to the symbols list below

using static Android.Service.Notification.NotificationListenerService;

public sealed record POISymbol(string Symbol, string Icon);

public static class POISymbols
{
    //Used when Symbol is null or not in the list below
    public const string DefaultIcon = "hajk.Images.Black-dot.png";

    private static readonly IReadOnlyList<POISymbol> symbols = new List<POISymbol>
    {
        new("Drinking Water",           "hajk.Images.Drinking-water.png"),
        new("Drinking Water - Tank",    "hajk.Images.Water-rain-tank.png"),
        new("Drinking Water - Stream",  "hajk.Images.Water-stream.png"),
        new("Campground",               "hajk.Images.Tent.png"),
        new("Rogaining",                "hajk.Images.RoundFlag.png"),
        new("Man Overboard",            "hajk.Images.RoundFlag.png"),
        new("Lodge",                    "hajk.Images.Cabin.png"),
        new("Flag, Blue",               "hajk.Images.FlagBlue.png"),
        new("Waypoint",                 "hajk.Images.FlagCheck.png"),
        new("Flag",                     "hajk.Images.Flag.png"),
        new("Dog",                      "hajk.Images.Dog.png"),
        new("Parking Area",             "hajk.Images.Parking.png"),
        new("Picnic Area",              "hajk.Images.PicnicArea.png"),
        new("Car",                      "hajk.Images.Car.png"),        
    };

    private static readonly Dictionary<string, string> iconBySymbol = BuildLookup();

    private static Dictionary<string, string> BuildLookup()
    {
        //Case-insensitive: "waypoint" and "Waypoint" are the same symbol
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in symbols)
        {
            if (!lookup.TryAdd(s.Symbol, s.Icon))
            {
                Serilog.Log.Warning($"POISymbols: duplicate symbol '{s.Symbol}' ignored");
            }
        }
        return lookup;
    }

    /// <summary>All known symbols, in the order listed above.</summary>
    public static IReadOnlyList<POISymbol> All => symbols;

    /// <summary>Icon resource name for a symbol; DefaultIcon if null or unknown.</summary>
    public static string GetIcon(string? symbol)
    {
        if (symbol != null && iconBySymbol.TryGetValue(symbol, out var icon))
        {
            return icon;
        }

        return DefaultIcon;
    }
}