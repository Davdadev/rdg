using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerDataService
{
    public const int DefaultSkinIndex = 0;
    public const string CoinsKey = "SoftCurrency";
    public const string GemsKey = "HardCurrency";
    public const string EquippedSkinKey = "EquippedSkinId";
    public const string OwnedSkinsKey = "OwnedSkins";
    public const string PlayerNameKey = "PlayerName";

    public static event Action OnCurrencyChanged;
    public static event Action OnLoadoutChanged;

    public static int Coins
    {
        get => PlayerPrefs.GetInt(CoinsKey, 0);
        set { PlayerPrefs.SetInt(CoinsKey, value); PlayerPrefs.Save(); OnCurrencyChanged?.Invoke(); }
    }

    public static int Gems
    {
        get => PlayerPrefs.GetInt(GemsKey, 0);
        set { PlayerPrefs.SetInt(GemsKey, value); PlayerPrefs.Save(); OnCurrencyChanged?.Invoke(); }
    }

    public static string PlayerName
    {
        get => PlayerPrefs.GetString(PlayerNameKey, "Player");
        set { PlayerPrefs.SetString(PlayerNameKey, value); PlayerPrefs.Save(); }
    }

    public static int EquippedSkinIndex
    {
        get => PlayerPrefs.GetInt(EquippedSkinKey, DefaultSkinIndex);
        set
        {
            int clamped = Mathf.Clamp(value, 0, SkinCatalog.SkinCount - 1);
            if (!OwnsSkin(clamped)) clamped = DefaultSkinIndex;
            PlayerPrefs.SetInt(EquippedSkinKey, clamped);
            PlayerPrefs.Save();
            OnLoadoutChanged?.Invoke();
        }
    }

    public static void EnsureDefaults()
    {
        if (!PlayerPrefs.HasKey(OwnedSkinsKey))
            SetOwnedSkins(new HashSet<int> { DefaultSkinIndex });

        if (!OwnsSkin(EquippedSkinIndex))
            EquippedSkinIndex = DefaultSkinIndex;
    }

    public static bool OwnsSkin(int skinIndex)
    {
        return GetOwnedSkins().Contains(skinIndex);
    }

    public static HashSet<int> GetOwnedSkins()
    {
        var owned = new HashSet<int>();
        string raw = PlayerPrefs.GetString(OwnedSkinsKey, DefaultSkinIndex.ToString());
        foreach (string part in raw.Split(','))
        {
            if (int.TryParse(part.Trim(), out int id))
                owned.Add(id);
        }
        if (owned.Count == 0)
            owned.Add(DefaultSkinIndex);
        return owned;
    }

    public static void UnlockSkin(int skinIndex)
    {
        var owned = GetOwnedSkins();
        if (owned.Add(skinIndex))
            SetOwnedSkins(owned);
    }

    static void SetOwnedSkins(HashSet<int> owned)
    {
        PlayerPrefs.SetString(OwnedSkinsKey, string.Join(",", owned));
        PlayerPrefs.Save();
        OnLoadoutChanged?.Invoke();
    }

    // Payload layout: [0..3] int skin index, [4..] UTF8 player name.
    // Skin stays in the first 4 bytes so ParseSkinFromPayload is unchanged.
    public static byte[] BuildConnectionPayload()
    {
        byte[] skinBytes = BitConverter.GetBytes(EquippedSkinIndex);
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(SanitizeName(PlayerName));

        var payload = new byte[skinBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(skinBytes, 0, payload, 0, skinBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, payload, skinBytes.Length, nameBytes.Length);
        return payload;
    }

    public static int ParseSkinFromPayload(byte[] payload)
    {
        if (payload == null || payload.Length < 4)
            return DefaultSkinIndex;

        int skin = BitConverter.ToInt32(payload, 0);
        return Mathf.Clamp(skin, 0, SkinCatalog.SkinCount - 1);
    }

    public static string ParseNameFromPayload(byte[] payload)
    {
        if (payload == null || payload.Length <= 4)
            return "Player";

        string name = System.Text.Encoding.UTF8.GetString(payload, 4, payload.Length - 4);
        return SanitizeName(name);
    }

    // Trim, cap length, filter profanity, and never return empty (the name tag
    // would vanish). Every name path routes through here, so the filter is
    // enforced in one place: settings input, menu display, payload, and name tags.
    public static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Player";
        string trimmed = raw.Trim();
        if (trimmed.Length > 16) trimmed = trimmed.Substring(0, 16);
        return ProfanityFilter.Filter(trimmed);
    }

    // The only guarded earn path - TrySpendCoins is the only guarded spend path, but every
    // earn site used to write Coins directly with no clamp. Route match rewards, ad rewards,
    // and doubler bonuses through here so a bad value can't push the balance negative or
    // overflow the underlying 32-bit PlayerPrefs int.
    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        long total = (long)Coins + amount;
        Coins = total > int.MaxValue ? int.MaxValue : (int)total;
    }

    public static bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        return true;
    }

    public static bool TrySpendGems(int amount)
    {
        if (amount <= 0 || Gems < amount) return false;
        Gems -= amount;
        return true;
    }
}
