namespace NamazuFlippers.Data;

public static class WorldData
{
    /// <summary>
    /// All 85 FFXIV worlds as of Dawntrail 7.x, sorted alphabetically for pickers.
    /// </summary>
    public static readonly string[] KnownWorlds =
    [
        "Adamantoise", "Aegis", "Alexander", "Alpha", "Anima", "Asura", "Atomos",
        "Bahamut", "Balmung", "Behemoth", "Belias", "Bismarck", "Brynhildr",
        "Cactuar", "Carbuncle", "Cerberus", "Chocobo", "Coeurl", "Cuchulainn",
        "Diabolos", "Durandal",
        "Excalibur", "Exodus",
        "Faerie", "Famfrit", "Fenrir",
        "Garuda", "Gilgamesh", "Goblin", "Golem", "Gungnir",
        "Hades", "Halicarnassus", "Hyperion",
        "Ifrit", "Ixion",
        "Jenova",
        "Kraken", "Kujata",
        "Lamia", "Leviathan", "Lich", "Louisoix",
        "Maduin", "Malboro", "Mandragora", "Marilith", "Masamune", "Mateus",
        "Midgardsormr", "Moogle",
        "Odin", "Omega",
        "Pandaemonium", "Phantom", "Phoenix",
        "Rafflesia", "Ragnarok", "Raiden", "Ramuh", "Ravana", "Ridill",
        "Sagittarius", "Sargatanas", "Sephirot", "Seraph", "Shinryu", "Shiva",
        "Siren", "Sophia", "Spriggan",
        "Tiamat", "Titan", "Tonberry", "Twintania", "Typhon",
        "Ultima", "Ultros", "Unicorn",
        "Valefor",
        "Yojimbo",
        "Zalera", "Zeromus", "Zodiark", "Zurvan",
    ];

    private static readonly Dictionary<string, string> WorldToDataCenter = new(StringComparer.OrdinalIgnoreCase)
    {
        // Aether
        ["Adamantoise"] = "Aether",
        ["Cactuar"] = "Aether",
        ["Faerie"] = "Aether",
        ["Gilgamesh"] = "Aether",
        ["Jenova"] = "Aether",
        ["Midgardsormr"] = "Aether",
        ["Sargatanas"] = "Aether",
        ["Siren"] = "Aether",

        // Crystal
        ["Balmung"] = "Crystal",
        ["Brynhildr"] = "Crystal",
        ["Coeurl"] = "Crystal",
        ["Diabolos"] = "Crystal",
        ["Goblin"] = "Crystal",
        ["Malboro"] = "Crystal",
        ["Mateus"] = "Crystal",
        ["Zalera"] = "Crystal",

        // Dynamis
        ["Cuchulainn"] = "Dynamis",
        ["Golem"] = "Dynamis",
        ["Halicarnassus"] = "Dynamis",
        ["Kraken"] = "Dynamis",
        ["Maduin"] = "Dynamis",
        ["Marilith"] = "Dynamis",
        ["Rafflesia"] = "Dynamis",
        ["Seraph"] = "Dynamis",

        // Primal
        ["Behemoth"] = "Primal",
        ["Excalibur"] = "Primal",
        ["Exodus"] = "Primal",
        ["Famfrit"] = "Primal",
        ["Hyperion"] = "Primal",
        ["Lamia"] = "Primal",
        ["Leviathan"] = "Primal",
        ["Ultros"] = "Primal",

        // Chaos
        ["Cerberus"] = "Chaos",
        ["Louisoix"] = "Chaos",
        ["Moogle"] = "Chaos",
        ["Omega"] = "Chaos",
        ["Phantom"] = "Chaos",
        ["Ragnarok"] = "Chaos",
        ["Sagittarius"] = "Chaos",
        ["Spriggan"] = "Chaos",

        // Light
        ["Alpha"] = "Light",
        ["Lich"] = "Light",
        ["Odin"] = "Light",
        ["Phoenix"] = "Light",
        ["Raiden"] = "Light",
        ["Shiva"] = "Light",
        ["Twintania"] = "Light",
        ["Zodiark"] = "Light",

        // Elemental
        ["Aegis"] = "Elemental",
        ["Atomos"] = "Elemental",
        ["Carbuncle"] = "Elemental",
        ["Garuda"] = "Elemental",
        ["Gungnir"] = "Elemental",
        ["Kujata"] = "Elemental",
        ["Tonberry"] = "Elemental",
        ["Typhon"] = "Elemental",

        // Gaia
        ["Alexander"] = "Gaia",
        ["Bahamut"] = "Gaia",
        ["Durandal"] = "Gaia",
        ["Fenrir"] = "Gaia",
        ["Ifrit"] = "Gaia",
        ["Ridill"] = "Gaia",
        ["Tiamat"] = "Gaia",
        ["Ultima"] = "Gaia",

        // Mana
        ["Anima"] = "Mana",
        ["Asura"] = "Mana",
        ["Chocobo"] = "Mana",
        ["Hades"] = "Mana",
        ["Ixion"] = "Mana",
        ["Masamune"] = "Mana",
        ["Pandaemonium"] = "Mana",
        ["Titan"] = "Mana",

        // Meteor
        ["Belias"] = "Meteor",
        ["Mandragora"] = "Meteor",
        ["Ramuh"] = "Meteor",
        ["Shinryu"] = "Meteor",
        ["Unicorn"] = "Meteor",
        ["Valefor"] = "Meteor",
        ["Yojimbo"] = "Meteor",
        ["Zeromus"] = "Meteor",

        // Materia
        ["Bismarck"] = "Materia",
        ["Ravana"] = "Materia",
        ["Sephirot"] = "Materia",
        ["Sophia"] = "Materia",
        ["Zurvan"] = "Materia",
    };

    public static bool IsKnownWorld(string world) =>
        !string.IsNullOrWhiteSpace(world) && WorldToDataCenter.ContainsKey(world.Trim());

    public static string? GetDataCenter(string world) =>
        string.IsNullOrWhiteSpace(world)
            ? null
            : WorldToDataCenter.GetValueOrDefault(world.Trim());

    public static int GetTravelFriction(string homeWorld, string purchaseWorld)
    {
        if (string.IsNullOrWhiteSpace(purchaseWorld))
            return 0;

        if (IsVendorSource(purchaseWorld))
            return 0;

        if (purchaseWorld.Equals(homeWorld, StringComparison.OrdinalIgnoreCase))
            return 0;

        var homeDc = GetDataCenter(homeWorld);
        var purchaseDc = GetDataCenter(purchaseWorld);

        if (homeDc == null || purchaseDc == null)
            return 3;

        return homeDc.Equals(purchaseDc, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    public static bool IsVendorSource(string source) =>
        source.Equals("Vendor", StringComparison.OrdinalIgnoreCase) ||
        source.StartsWith("Vendor:", StringComparison.OrdinalIgnoreCase);
}
