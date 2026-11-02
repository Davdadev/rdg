public static class GameSessionState
{
    public static Team? LocalTeam;
    public static Team? Winner;
    public static int RedScore;
    public static int BlueScore;
    public static int LastCoinReward;
    public static bool PendingResults;

    // Lives here rather than as an instance field on ResultsController: LastCoinReward is
    // also static, and ShowFromSession can run again (Start() re-triggers it whenever
    // PendingResults is still set) before Clear() wipes the session. An instance-field
    // guard would reset on every such re-entry and let the doubler compound against an
    // already-doubled reward.
    public static bool RewardDoubled;

    // winner is null for a draw (clock expired with scores level).
    public static void SetMatchResult(Team? winner, int redScore, int blueScore, int coinReward, Team? localTeam)
    {
        Winner = winner;
        RedScore = redScore;
        BlueScore = blueScore;
        LastCoinReward = coinReward;
        LocalTeam = localTeam;
        PendingResults = true;
        RewardDoubled = false;
    }

    public static void Clear()
    {
        Winner = null;
        RedScore = 0;
        BlueScore = 0;
        LastCoinReward = 0;
        LocalTeam = null;
        PendingResults = false;
        RewardDoubled = false;
    }
}
