using UnityEngine;

public static class RoundTimer
{
    public const float RoundDurationSeconds = 20f * 60f;

    private static float startedAt = -1f;
    private static readonly KillTimeWindow[] killTimeWindows =
    {
        new KillTimeWindow(18f * 60f, 15f * 60f),
        new KillTimeWindow(13f * 60f, 10f * 60f),
        new KillTimeWindow(8f * 60f, 5f * 60f),
        new KillTimeWindow(3f * 60f, 0f)
    };

    public readonly struct KillTimeWindow
    {
        public readonly float startsAtRemaining;
        public readonly float endsAtRemaining;

        public KillTimeWindow(float startsAtRemaining, float endsAtRemaining)
        {
            this.startsAtRemaining = startsAtRemaining;
            this.endsAtRemaining = endsAtRemaining;
        }
    }

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

    public static int CurrentKillTimeWindowIndex
    {
        get
        {
            float remaining = RemainingSeconds;

            for (int i = 0; i < killTimeWindows.Length; i++)
            {
                KillTimeWindow window = killTimeWindows[i];

                if (remaining <= window.startsAtRemaining && remaining > window.endsAtRemaining)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public static bool IsKillTimeActive => CurrentKillTimeWindowIndex >= 0;

    public static float CurrentKillTimeRemainingSeconds
    {
        get
        {
            int index = CurrentKillTimeWindowIndex;

            if (index < 0)
            {
                return 0f;
            }

            return Mathf.Max(0f, RemainingSeconds - killTimeWindows[index].endsAtRemaining);
        }
    }
}
