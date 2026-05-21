using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public static class PlayerColorPalette
{
    public const string PlayerColorPropertyKey = "playerColorIndex";
    public const int ColorCount = 12;
    public const int FirstScanColorGroupIndex = 19;

    private static readonly Color[] colors =
    {
        new Color(0.95f, 0.24f, 0.20f, 1f),
        new Color(0.18f, 0.55f, 1f, 1f),
        new Color(0.20f, 0.90f, 0.42f, 1f),
        new Color(1f, 0.78f, 0.18f, 1f),
        new Color(0.75f, 0.34f, 1f, 1f),
        new Color(0.12f, 0.88f, 0.82f, 1f),
        new Color(1f, 0.48f, 0.16f, 1f),
        new Color(0.95f, 0.95f, 0.92f, 1f),
        new Color(1f, 0.36f, 0.68f, 1f),
        new Color(0.44f, 0.72f, 1f, 1f),
        new Color(0.70f, 1f, 0.22f, 1f),
        new Color(0.86f, 0.58f, 0.34f, 1f)
    };

    public static Color GetColor(int colorIndex)
    {
        return colors[Mathf.Clamp(colorIndex, 0, ColorCount - 1)];
    }

    public static int GetPlayerColorIndex(Player player, int fallback)
    {
        if (player == null || player.CustomProperties == null)
        {
            return fallback;
        }

        if (!player.CustomProperties.TryGetValue(PlayerColorPropertyKey, out object value))
        {
            return fallback;
        }

        if (value is int intValue)
        {
            return Mathf.Clamp(intValue, 0, ColorCount - 1);
        }

        if (value is byte byteValue)
        {
            return Mathf.Clamp(byteValue, 0, ColorCount - 1);
        }

        if (value is short shortValue)
        {
            return Mathf.Clamp(shortValue, 0, ColorCount - 1);
        }

        return fallback;
    }

    public static int GetColorIndexForActor(int actorNumber, int fallback = 0)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList == null)
        {
            return fallback;
        }

        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            Player player = PhotonNetwork.PlayerList[i];
            if (player != null && player.ActorNumber == actorNumber)
            {
                return GetPlayerColorIndex(player, fallback);
            }
        }

        return fallback;
    }

    public static ScanDotColorGroup GetScanColorGroupForActor(int actorNumber)
    {
        int colorIndex = GetColorIndexForActor(actorNumber, 0);
        return (ScanDotColorGroup)(FirstScanColorGroupIndex + Mathf.Clamp(colorIndex, 0, ColorCount - 1));
    }
}
