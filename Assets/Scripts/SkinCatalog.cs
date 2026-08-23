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
            // Knight is the free starter, Ninja is a coin sink, Ranger is the gem sink -
            // the old `id >= 3` gem branch was unreachable since SkinCount clamps to 0..2.
            CoinPrice = id == 1 ? 150 : 0,
            GemPrice = id == 2 ? 20 : 0,
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
