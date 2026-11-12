using System;
using UnityEngine;

public sealed class ClockTimeController : MonoBehaviour
{
    [SerializeField] private Transform hourHandPivot;
    [SerializeField] private Transform minuteHandPivot;

    private static readonly TimeZoneInfo CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
#if UNITY_IOS || UNITY_STANDALONE_OSX
        "America/Chicago"
#else
        "Central Standard Time"
#endif
    );

    private void Update()
    {
        var time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CentralTimeZone);
        var minuteAngle = (time.Minute + time.Second / 60f) * 6f;
        var hourAngle = ((time.Hour % 12) + time.Minute / 60f) * 30f;

        if (hourHandPivot)
            hourHandPivot.localRotation = Quaternion.Euler(-hourAngle, 0f, 0f);

        if (minuteHandPivot)
            minuteHandPivot.localRotation = Quaternion.Euler(-minuteAngle, 0f, 0f);
    }
}