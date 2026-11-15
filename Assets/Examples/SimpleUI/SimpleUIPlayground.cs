using UnityEngine;
using ModuleFactory.SimpleUI;

public sealed class SimpleUIPlayground : MonoBehaviour
{
    public UIManager UI;
    public UIPanel Menu, Settings, Confirm, Game, Pause, Result;
    public void OpenSettings() { UI.Open(Settings); }
    public void AskReset() { UI.Open(Confirm); }
    public void CloseConfirm() { UI.Close(Confirm); }
    public void Back() { UI.Back(); }
    public void StartGame() { UI.CloseAll(); UI.Open(Game); }
    public void PauseGame() { Time.timeScale = 0; UI.Open(Pause); }
    public void ResumeGame() { UI.Close(Pause); }
    public void RestoreTime() { Time.timeScale = 1; }
    public void FinishGame() { UI.Close(Pause); UI.Open(Result); }
    public void MainMenu() { UI.CloseAll(); UI.Open(Menu); }
    private void OnDisable() { Time.timeScale = 1; }
}
