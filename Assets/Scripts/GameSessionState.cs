public static class GameSessionState
{
    public static Team? LocalTeam;
    public static Team? Winner;
    public static int RedScore;
    public static int BlueScore;
    public static int LastCoinReward;
    public static bool PendingResults;

    // Lives here, not as an instance flag on ResultsController: LastCoinReward is also
    // static, so a guard on the controller resets on every ShowFromSession() call while the
    // reward it is guarding stays doubled - a second call before Clear() would double the
    // already-doubled value again.
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
