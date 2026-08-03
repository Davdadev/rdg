using UnityEngine;
using UnityEngine.UI;

public class SettingsController : MonoBehaviour
{
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider musicSlider;
    [SerializeField] Toggle hapticsToggle;
    [SerializeField] InputField playerNameInput;
    [SerializeField] GameObject panel;

    void OnEnable()
    {
        Refresh();
        AudioSettingsService.OnSettingsChanged += Refresh;
    }

    void OnDisable()
    {
        AudioSettingsService.OnSettingsChanged -= Refresh;
    }

    void Refresh()
    {
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioSettingsService.SfxVolume);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(AudioSettingsService.MusicVolume);
        if (hapticsToggle != null) hapticsToggle.SetIsOnWithoutNotify(AudioSettingsService.HapticsEnabled);
        if (playerNameInput != null) playerNameInput.SetTextWithoutNotify(PlayerDataService.PlayerName);
    }

    public void Show() { if (panel != null) panel.SetActive(true); }
    public void Hide() { if (panel != null) panel.SetActive(false); }

    public void OnSfxChanged(float value) => AudioSettingsService.SfxVolume = value;
    public void OnMusicChanged(float value) => AudioSettingsService.MusicVolume = value;
    public void OnHapticsChanged(bool value) => AudioSettingsService.HapticsEnabled = value;

    public void OnPlayerNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            PlayerDataService.PlayerName = value.Trim();
    }

    public void OnClosePressed() => Hide();
}
