using System.Globalization;
using TMPro;
using UnityEngine;

// Presentation only; the original HUD continues to own all verification text.
[DefaultExecutionOrder(350)]
public sealed class CommandTimelineDashboard : MonoBehaviour
{
    [SerializeField] private CommandTimelineTracker tracker;
    [SerializeField] private HazardZoneEvaluator hazard;
    [SerializeField] private TMP_Text warningTimingText;
    [SerializeField] private TMP_Text summaryStatus;
    [SerializeField] private TMP_Text summaryDescription;
    private bool captured, compact;
    private Vector2 statusSize, descriptionSize;
    private bool statusAuto, descriptionAuto;
    private float statusMin, descriptionMin;
    private float statusFontSize, descriptionFontSize;
    private string previousText;

    private void LateUpdate()
    {
        if (!captured && summaryStatus != null && summaryDescription != null)
        {
            statusSize = summaryStatus.rectTransform.sizeDelta; descriptionSize = summaryDescription.rectTransform.sizeDelta;
            statusAuto = summaryStatus.enableAutoSizing; descriptionAuto = summaryDescription.enableAutoSizing;
            statusMin = summaryStatus.fontSizeMin; descriptionMin = summaryDescription.fontSizeMin; captured = true;
            statusFontSize = summaryStatus.fontSize; descriptionFontSize = summaryDescription.fontSize;
        }
        bool show = tracker != null && tracker.isActiveAndEnabled && tracker.CurrentTimelineKind == CommandTimelineTracker.TimelineKind.MOVEMENT &&
            !tracker.DuplicateIdPayloadMismatch && tracker.HasHazardTime && hazard != null && hazard.isActiveAndEnabled &&
            hazard.CurrentHazardState == HazardZoneEvaluator.HazardState.DANGER && hazard.HazardCommandId == tracker.CurrentCommandId;
        SetCompact(show);
        if (warningTimingText == null) return;
        warningTimingText.gameObject.SetActive(show);
        if (!show) return;
        string text = "EARLY WARNING\nBefore Operator: " + Value(tracker.HasSignalWarningLeadToOperator,tracker.SignalWarningLeadToOperatorSec) +
            "\nBefore Motion: " + Value(tracker.HasSignalWarningLeadToMotion,tracker.SignalWarningLeadToMotionSec);
        if (text != previousText) { warningTimingText.text = text; previousText = text; }
    }
    private static string Value(bool has, float value) => has ? value.ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture)+" s" : "WAITING";
    private void SetCompact(bool value)
    {
        if (!captured || compact == value) return;
        compact = value;
        summaryStatus.rectTransform.sizeDelta = value ? new Vector2(350,statusSize.y) : statusSize;
        summaryDescription.rectTransform.sizeDelta = value ? new Vector2(350,descriptionSize.y) : descriptionSize;
        summaryStatus.enableAutoSizing = value || statusAuto; summaryDescription.enableAutoSizing = value || descriptionAuto;
        summaryStatus.fontSizeMin = value ? 16f : statusMin; summaryDescription.fontSizeMin = value ? 12f : descriptionMin;
        if (!value) { summaryStatus.fontSize = statusFontSize; summaryDescription.fontSize = descriptionFontSize; }
    }
    private void OnDisable()
    { SetCompact(false); if (warningTimingText != null) warningTimingText.gameObject.SetActive(false); previousText = null; }
}
