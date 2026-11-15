using System.Collections;
using ModuleFactory.SceneFlow;
using UnityEngine;
using ModuleFactory.SimpleUI;

public sealed class FadeDemoControls : MonoBehaviour
{
    public UIScreenFade Fade;
    public SceneFlowLoader Loader;
    public string TargetScene;
    private bool busy;
    public bool IsBusy => busy || SceneFlowLoader.IsBusy;
    public void LoadScene() { if (!IsBusy && Loader != null) Loader.TryLoad(TargetScene); }
    public void Blackout() { Begin(0); }
    public void PausedBlackout() { Begin(1); }
    public void Reverse() { Begin(2); }
    public void CancelAndRestore() { Begin(3); }
    private void Begin(int mode) { if (!IsBusy) { busy = true; StartCoroutine(Demo(mode)); } }
    private IEnumerator Demo(int mode)
    {
        float previousTimeScale = Time.timeScale;
        try
        {
            if (mode == 1) Time.timeScale = 0;
            var request = Fade.FadeOut(mode >= 2 ? 1 : .35f);
            if (mode >= 2)
            {
                yield return new WaitForSecondsRealtime(.2f);
                if (mode == 2)
                {
                    var reverse = Fade.FadeIn(.3f);
                    Debug.Log("Replaced fade: " + request.Status);
                    yield return reverse;
                }
                else
                {
                    Fade.Cancel(); Debug.Log("Cancelled fade keeps alpha: " + Fade.Alpha);
                    yield return new WaitForSecondsRealtime(.3f); Fade.SetImmediate(0);
                }
            }
            else
            {
                yield return request;
                if (request.Status != FadeStatus.Completed) yield break;
                yield return new WaitForSecondsRealtime(.35f); yield return Fade.FadeIn(.35f);
            }
        }
        finally { Time.timeScale = previousTimeScale; busy = false; if (Fade != null) Fade.SetImmediate(0); }
    }
    private void OnDisable()
    {
        // A paused demo must never leave the editor paused after stopping it.
        StopAllCoroutines();
        if (busy) { Time.timeScale = 1; if (Fade != null) Fade.SetImmediate(0); busy = false; }
    }
}
