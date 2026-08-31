using UnityEngine;

public static class SkinCatalog
{
    // Only skins with finished art ship. Medic and Commander were placeholder blobs,
    // so they were pulled from the catalog until real assets exist. Add them back here
    // (and re-enable their shop buttons) once their sheets are done.
    public const int SkinCount = 3;

    static readonly string[] Names = { "Knight", "Ninja", "Ranger" };

    public struct SkinEntry
    {
        public int Id;
        public string Name;
        public int CoinPrice;
        public int GemPrice;
        public bool Free;
    }

    // Top-tier skin (Ranger) is the gem sink - it's gem-only, not an alternative to coins,
    // so CoinPrice is 0 there. This was previously `id >= 3`, which SkinCount = 3 can never
    // reach, so gems had no sink at all. Everything below the top tier is coin-only.
    public static SkinEntry Get(int id)
    {
        id = Mathf.Clamp(id, 0, SkinCount - 1);
        bool isGemTier = id == SkinCount - 1 && id > 0;
        return new SkinEntry
        {
            Id = id,
            Name = Names[id],
            CoinPrice = id == 0 || isGemTier ? 0 : 150 * id,
            GemPrice = isGemTier ? 30 : 0,
            Free = id == 0
        };
    }

    public static Sprite GetPreviewSprite(int id, Team team = Team.Blue)
    {
        id = Mathf.Clamp(id, 0, SkinCount - 1);
        string prefix = team == Team.Red ? "Red" : "Blue";
        return Resources.Load<Sprite>($"Skins/{prefix}_{Names[id]}");
    }
}
