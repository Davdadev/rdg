using UnityEngine;

public static class SkinCatalog
{
    // Only skins with finished art ship. Medic and Commander were placeholder blobs,
    // so they were pulled from the catalog until real assets exist. Add them back here
    // (and re-enable their shop buttons) once their sheets are done.
    public const int SkinCount = 3;

    // The last skin in the catalog is the gem sink. It used to be priced by an
    // `id >= 3` check that SkinCount = 3 made unreachable, so nothing ever cost
    // gems and the gem packs in IAPService had nothing to sell. Tying the price to
    // "last id" instead keeps it reachable no matter how many skins ship.
    const int GemSkinId = SkinCount - 1;

    static readonly string[] Names = { "Knight", "Ninja", "Ranger" };

    public struct SkinEntry
    {
        public int Id;
        public string Name;
        public int CoinPrice;
        public int GemPrice;
        public bool Free;
    }

    public static SkinEntry Get(int id)
    {
        id = Mathf.Clamp(id, 0, SkinCount - 1);
        bool gemPriced = id == GemSkinId && id != 0;
        return new SkinEntry
        {
            Id = id,
            Name = Names[id],
            CoinPrice = id == 0 ? 0 : (gemPriced ? 0 : 150 * id),
            GemPrice = gemPriced ? 20 : 0,
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
