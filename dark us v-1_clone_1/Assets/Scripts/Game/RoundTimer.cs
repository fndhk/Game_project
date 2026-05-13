using UnityEngine;

public static class RoundTimer
{
    public const float RoundDurationSeconds = 20f * 60f;

    private static float startedAt = -1f;

    public static void ResetTimer()
    {
        startedAt = Time.time;
    }

    public static float RemainingSeconds
    {
        get
        {
            if (startedAt < 0f)
            {
                ResetTimer();
            }

            return Mathf.Max(0f, RoundDurationSeconds - (Time.time - startedAt));
        }
    }
}
