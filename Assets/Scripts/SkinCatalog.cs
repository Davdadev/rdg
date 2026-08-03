using UnityEngine;

public static class SkinCatalog
{
    public const int SkinCount = 5;

    static readonly string[] Names = { "Knight", "Ninja", "Ranger", "Medic", "Commander" };

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
            CoinPrice = id == 0 ? 0 : 150 * id,
            GemPrice = id >= 3 ? 10 * (id - 2) : 0,
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
