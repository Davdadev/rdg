using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LobbyController : MonoBehaviour
{
    [SerializeField] Text statusText;
    [SerializeField] Text detailText;
    [SerializeField] GameObject searchingPanel;
    [SerializeField] GameObject matchFoundPanel;
    [SerializeField] GameObject errorPanel;
    [SerializeField] Text errorText;

    void Start()
    {
        PlayerDataService.EnsureDefaults();

        if (MatchmakingService.Instance == null)
        {
            ShowError("Network bootstrap missing. Add NetworkBootstrap to the scene.");
            return;
        }

        MatchmakingService.Instance.OnStateChanged += HandleStateChanged;
        MatchmakingService.Instance.OnError += ShowError;
        MatchmakingService.Instance.BeginQuickMatch();
        Refresh(MatchmakingService.Instance.State, MatchmakingService.Instance.StatusMessage);
    }

    void OnDestroy()
    {
        if (MatchmakingService.Instance != null)
        {
            MatchmakingService.Instance.OnStateChanged -= HandleStateChanged;
            MatchmakingService.Instance.OnError -= ShowError;
        }
    }

    void HandleStateChanged(MatchmakingService.MatchmakingState state)
    {
        Refresh(state, MatchmakingService.Instance.StatusMessage);
    }

    void Refresh(MatchmakingService.MatchmakingState state, string message)
    {
        if (statusText != null) statusText.text = message;

        bool searching = state == MatchmakingService.MatchmakingState.Initializing
            || state == MatchmakingService.MatchmakingState.Searching
            || state == MatchmakingService.MatchmakingState.LoadingArena;

        if (searchingPanel != null) searchingPanel.SetActive(searching);
        if (matchFoundPanel != null) matchFoundPanel.SetActive(state == MatchmakingService.MatchmakingState.MatchFound);
        if (errorPanel != null) errorPanel.SetActive(state == MatchmakingService.MatchmakingState.Failed);

        if (detailText != null)
        {
            detailText.text = state switch
            {
                MatchmakingService.MatchmakingState.Searching => "Waiting for players...",
                MatchmakingService.MatchmakingState.LoadingArena => "Entering the arena...",
                _ => ""
            };
        }
    }

    void ShowError(string message)
    {
        if (errorText != null) errorText.text = message;
        if (errorPanel != null) errorPanel.SetActive(true);
        if (searchingPanel != null) searchingPanel.SetActive(false);
    }

    public void OnCancelPressed()
    {
        if (MatchmakingService.Instance != null)
            MatchmakingService.Instance.CancelMatchmaking();

        SceneLoader.LoadScene("MainMenu");
    }

    public void OnBackToMenuPressed()
    {
        SceneLoader.LoadScene("MainMenu");
    }
}
