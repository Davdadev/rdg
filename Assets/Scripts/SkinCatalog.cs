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
        return new SkinEntry
        {
            Id = id,
            Name = Names[id],
            // One skin of each kind: free starter, coin-priced, gem-priced. Gems need a real
            // sink to be worth selling packs for - Ranger is it, until more skins ship.
            CoinPrice = id == 1 ? 150 : 0,
            GemPrice = id == 2 ? 30 : 0,
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
