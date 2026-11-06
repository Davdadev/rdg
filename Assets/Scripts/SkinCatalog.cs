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

    // The top skin is the hard-currency sink: it is priced in gems instead of coins,
    // which is what gives the gem packs in IAPService something to buy. Without this,
    // gems have no sink and the gem packs have no reason to exist (see IAPService).
    const int PremiumSkinId = SkinCount - 1;
    const int PremiumGemPrice = 50;

    public static SkinEntry Get(int id)
    {
        id = Mathf.Clamp(id, 0, SkinCount - 1);
        bool premium = id == PremiumSkinId;
        return new SkinEntry
        {
            Id = id,
            Name = Names[id],
            CoinPrice = (id == 0 || premium) ? 0 : 150 * id,
            GemPrice = premium ? PremiumGemPrice : 0,
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
