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

    public static SkinEntry Get(int id)
    {
        id = Mathf.Clamp(id, 0, SkinCount - 1);

        // The top-tier skin is the gem sink. The old formula (id >= 3) priced Medic/Commander
        // in gems, but both were pulled from the catalog above and Get() clamps to SkinCount-1,
        // so that branch was unreachable and gems had nothing to spend on. Until replacement
        // premium skins ship, Ranger is the one skin gems buy instead of coins.
        bool gemSkin = id == SkinCount - 1 && id != 0;
        return new SkinEntry
        {
            Id = id,
            Name = Names[id],
            CoinPrice = (id == 0 || gemSkin) ? 0 : 150 * id,
            GemPrice = gemSkin ? 20 : 0,
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
