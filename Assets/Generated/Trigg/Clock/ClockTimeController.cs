using System;
using UnityEngine;

public sealed class ClockTimeController : MonoBehaviour
{
    [SerializeField] private Transform hourHandPivot;
    [SerializeField] private Transform minuteHandPivot;

    private void Update()
    {
        var time = DateTime.Now;
        var minuteAngle = (time.Minute + time.Second / 60f) * 6f;
        var hourAngle = ((time.Hour % 12) + time.Minute / 60f) * 30f;

        if (hourHandPivot)
            hourHandPivot.localRotation = Quaternion.Euler(-hourAngle, 0f, 0f);

        if (minuteHandPivot)
            minuteHandPivot.localRotation = Quaternion.Euler(-minuteAngle, 0f, 0f);
    }
}
