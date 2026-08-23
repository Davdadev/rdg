public static class GameSessionState
{
    public static Team? LocalTeam;
    public static Team? Winner;
    public static int RedScore;
    public static int BlueScore;
    public static int LastCoinReward;
    public static bool PendingResults;

    // Lives here rather than on ResultsController: the results panel can be shown more than
    // once per match (e.g. ShowFromSession() re-running before Clear()), and the doubler must
    // stay spent across every one of those calls, not just the instance that first saw it.
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
