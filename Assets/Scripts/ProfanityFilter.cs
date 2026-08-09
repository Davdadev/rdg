using System.Collections.Generic;
using System.Text;

// Username profanity guard. Kept deliberately simple and self-contained (no
// external service) so it works offline and adds zero dependencies. Called from
// PlayerDataService.SanitizeName, so every name path — settings input, menu
// display, network payload, and name tags — is filtered in one place.
//
// Note on the "Scunthorpe problem": substring matching can flag innocent names
// that happen to contain a bad word. The list below is intentionally short and
// targets whole-token slurs/obscenities; expand it cautiously.
public static class ProfanityFilter
{
    // Lowercase, leet-normalized forms. Matching happens against a normalized copy
    // of the input so "sh1t", "$h!t", "a55" etc. are caught too.
    static readonly string[] Blocklist =
    {
        "fuck", "shit", "bitch", "cunt", "asshole", "dick", "pussy",
        "bastard", "nigger", "nigga", "faggot", "fag", "retard",
        "whore", "slut", "rape", "nazi", "cock", "wank", "twat",
    };

    // Leetspeak / lookalike substitutions applied before matching.
    static readonly Dictionary<char, char> LeetMap = new Dictionary<char, char>
    {
        ['0'] = 'o', ['1'] = 'i', ['3'] = 'e', ['4'] = 'a', ['5'] = 's',
        ['6'] = 'g', ['7'] = 't', ['8'] = 'b', ['9'] = 'g',
        ['@'] = 'a', ['$'] = 's', ['!'] = 'i', ['+'] = 't', ['('] = 'c',
    };

    /// <summary>True if the name contains a blocked word (leet-aware).</summary>
    public static bool Contains(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        string normalized = Normalize(input);
        foreach (var bad in Blocklist)
            if (normalized.Contains(bad)) return true;
        return false;
    }

    /// <summary>
    /// Returns the name if clean, otherwise a safe fallback. Usernames are short
    /// and public, so we reject rather than star-out a partial match — a masked
    /// "****head" still reads as an insult.
    /// </summary>
    public static string Filter(string input, string fallback = "Player")
    {
        return Contains(input) ? fallback : input;
    }

    // Lowercase, apply leet substitutions, and drop everything that isn't a-z so
    // separators ("f.u.c.k", "f u c k") can't be used to slip a word through.
    static string Normalize(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char raw in input.ToLowerInvariant())
        {
            char c = LeetMap.TryGetValue(raw, out char mapped) ? mapped : raw;
            if (c >= 'a' && c <= 'z') sb.Append(c);
        }
        return sb.ToString();
    }
}
