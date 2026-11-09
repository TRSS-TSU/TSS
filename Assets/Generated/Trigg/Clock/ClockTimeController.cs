using System;
using UnityEngine;

public sealed class ClockTimeController : MonoBehaviour
{
    [SerializeField] private Transform hourHandPivot;
    [SerializeField] private Transform minuteHandPivot;

    private void OnEnable()
    {
        UpdateHands(DateTime.Now);
    }

    private void Update()
    {
        UpdateHands(DateTime.Now);
    }

    public void UpdateHands(DateTime time)
    {
        var minuteAngle = (time.Minute + time.Second / 60f + time.Millisecond / 60000f) * 6f;
        var hourAngle = ((time.Hour % 12) + time.Minute / 60f + time.Second / 3600f) * 30f;

        if (hourHandPivot)
            hourHandPivot.localRotation = Quaternion.Euler(-hourAngle, 0f, 0f);

        if (minuteHandPivot)
            minuteHandPivot.localRotation = Quaternion.Euler(-minuteAngle, 0f, 0f);
    }
}