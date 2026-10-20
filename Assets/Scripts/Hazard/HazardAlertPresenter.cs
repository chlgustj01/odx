using TMPro;
using UnityEngine;

[DefaultExecutionOrder(250)]
public sealed class HazardAlertPresenter : MonoBehaviour
{
    [SerializeField] private HazardZoneEvaluator evaluator;
    [SerializeField] private TMP_Text summaryHazardText;
    [SerializeField] private TMP_Text worldWarningText;
    [SerializeField] private AudioSource warningAudio;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.12f;
    [field: SerializeField] public int BeepPlayCount { get; private set; }
    private AudioClip beep;
    private int lastDangerEntryCount;
    private string lastSummary;

    private void OnEnable() { lastDangerEntryCount = 0; BeepPlayCount = 0; }
    private void LateUpdate()
    {
        var state = evaluator != null && evaluator.isActiveAndEnabled ? evaluator.CurrentHazardState : HazardZoneEvaluator.HazardState.NONE;
        bool danger = state == HazardZoneEvaluator.HazardState.DANGER;
        string text = danger ? "WORKER IN PREDICTED LOAD PATH\nWorker ID: " + evaluator.HazardWorkerId :
            state == HazardZoneEvaluator.HazardState.SAFE ? "PREDICTED PATH CLEAR" : "NO PREDICTION";
        if (summaryHazardText != null && lastSummary != text)
        {
            lastSummary = text; summaryHazardText.text = text;
            summaryHazardText.color = danger ? new Color(1f,0.25f,0.25f) :
                state == HazardZoneEvaluator.HazardState.SAFE ? Color.cyan : new Color(0.55f,0.67f,0.75f);
        }
        if (worldWarningText != null)
        {
            worldWarningText.gameObject.SetActive(danger);
            if (danger)
            {
                worldWarningText.text = "Hazard: Worker in Predicted Load Path";
                worldWarningText.transform.position = evaluator.HazardFootPosition + Vector3.up * 2.4f;
                var camera = Camera.main;
                if (camera != null) worldWarningText.transform.rotation = camera.transform.rotation;
            }
        }
        if (evaluator != null && lastDangerEntryCount != evaluator.DangerEntryCount)
        {
            lastDangerEntryCount = evaluator.DangerEntryCount;
            if (danger) PlayWarning();
        }
        if (!danger && warningAudio != null && warningAudio.isPlaying) warningAudio.Stop();
    }
    private void PlayWarning()
    {
        if (warningAudio == null) return;
        if (beep == null)
        {
            const int sampleRate = 22050;
            int count = Mathf.RoundToInt(sampleRate * 0.18f);
            var samples = new float[count];
            for (int i=0; i<count; i++)
            {
                float t = (float)i/sampleRate;
                float envelope = Mathf.Min(1f, t/0.01f) * Mathf.Min(1f, (0.18f-t)/0.025f);
                samples[i] = Mathf.Sin(2f*Mathf.PI*880f*t) * envelope * 0.5f;
            }
            beep = AudioClip.Create("D3 Procedural Warning Beep", count, 1, sampleRate, false);
            beep.SetData(samples, 0);
        }
        warningAudio.PlayOneShot(beep, warningVolume);
        BeepPlayCount++;
    }
    private void OnDisable()
    {
        if (warningAudio != null) warningAudio.Stop();
        if (worldWarningText != null) worldWarningText.gameObject.SetActive(false);
        if (summaryHazardText != null) { summaryHazardText.text = "NO PREDICTION"; summaryHazardText.color = Color.gray; }
        lastSummary = null;
    }
    private void OnDestroy() { if (beep != null) Destroy(beep); }
}
