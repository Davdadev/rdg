public static class GameSessionState
{
    public static Team? LocalTeam;
    public static Team? Winner;
    public static int RedScore;
    public static int BlueScore;
    public static int LastCoinReward;
    public static bool PendingResults;

    // Lives here, not on ResultsController, because ShowFromSession() can legitimately run
    // more than once against the same pending result (e.g. Start() re-showing after a scene
    // reload) - an instance flag would reset each time and re-arm the doubler against a
    // LastCoinReward that is already doubled.
    public static bool RewardDoubled;

    // winner is null for a draw (clock expired with scores level).
    public static void SetMatchResult(Team? winner, int redScore, int blueScore, int coinReward, Team? localTeam)
    {
        Winner = winner;
        RedScore = redScore;
        BlueScore = blueScore;
        LastCoinReward = coinReward;
        LocalTeam = localTeam;
        RewardDoubled = false;
        PendingResults = true;
    }

    public static void Clear()
    {
        Winner = null;
        RedScore = 0;
        BlueScore = 0;
        LastCoinReward = 0;
        LocalTeam = null;
        RewardDoubled = false;
        PendingResults = false;
    }
}
