public static class GameSessionState
{
    public static Team? LocalTeam;
    public static Team? Winner;
    public static int RedScore;
    public static int BlueScore;
    public static int LastCoinReward;
    public static bool PendingResults;

    // Lives here rather than as an instance flag on ResultsController: LastCoinReward is
    // already-doubled static state, so the "have we doubled it" guard has to be static too,
    // or a second ShowFromSession() before Clear() (e.g. Start() replaying pending results)
    // re-arms the doubler against an already-doubled value.
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
