using System;
using System.Reflection;
using UnityEngine;

public static class FriendInviteBridge
{
    private const string RoomCodeArgumentPrefix = "+darkus_room=";
    private const string RoomCodeLongArgumentPrefix = "--darkus-room=";

    public static bool TryGetLaunchRoomCode(out string roomCode)
    {
        roomCode = string.Empty;

        string[] args;
        try
        {
            args = Environment.GetCommandLineArgs();
        }
        catch
        {
            return false;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (TryExtractRoomCode(arg, out roomCode))
            {
                return true;
            }

            if ((arg == "+darkus_room" || arg == "--darkus-room") &&
                i + 1 < args.Length &&
                IsValidRoomCode(args[i + 1]))
            {
                roomCode = args[i + 1];
                return true;
            }
        }

        return false;
    }

    public static string BuildSteamConnectString(string roomCode)
    {
        return RoomCodeArgumentPrefix + roomCode;
    }

    public static string BuildInviteText(string roomCode)
    {
        return "DARK US room code: " + roomCode;
    }

    public static void CopyInviteText(string roomCode)
    {
        if (!IsValidRoomCode(roomCode))
        {
            return;
        }

        GUIUtility.systemCopyBuffer = BuildInviteText(roomCode);
    }

    public static bool PrepareSteamInvite(string roomCode)
    {
        if (!IsValidRoomCode(roomCode))
        {
            return false;
        }

        CopyInviteText(roomCode);
        SetRoomRichPresence(roomCode);

        string connectString = BuildSteamConnectString(roomCode);
        if (TryInvokeSteamFriendsString("ActivateGameOverlayInviteDialogConnectString", connectString))
        {
            return true;
        }

        if (TryInvokeSteamFriendsString("ActivateGameOverlay", "Friends"))
        {
            return true;
        }

        Application.OpenURL("steam://open/friends");
        return false;
    }

    public static void SetRoomRichPresence(string roomCode)
    {
        if (!IsValidRoomCode(roomCode))
        {
            return;
        }

        TrySetSteamRichPresence("connect", BuildSteamConnectString(roomCode));
        TrySetSteamRichPresence("status", "Room " + roomCode);
    }

    public static void ClearRichPresence()
    {
        TrySetSteamRichPresence("connect", string.Empty);
        TrySetSteamRichPresence("status", string.Empty);
    }

    private static bool TryExtractRoomCode(string arg, out string roomCode)
    {
        roomCode = string.Empty;

        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        if (arg.StartsWith(RoomCodeArgumentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            roomCode = arg.Substring(RoomCodeArgumentPrefix.Length);
            return IsValidRoomCode(roomCode);
        }

        if (arg.StartsWith(RoomCodeLongArgumentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            roomCode = arg.Substring(RoomCodeLongArgumentPrefix.Length);
            return IsValidRoomCode(roomCode);
        }

        return false;
    }

    private static bool IsValidRoomCode(string roomCode)
    {
        if (roomCode == null || roomCode.Length != 4)
        {
            return false;
        }

        for (int i = 0; i < roomCode.Length; i++)
        {
            if (!char.IsDigit(roomCode[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySetSteamRichPresence(string key, string value)
    {
        Type steamFriendsType = FindSteamFriendsType();
        if (steamFriendsType == null)
        {
            return false;
        }

        MethodInfo method = steamFriendsType.GetMethod(
            "SetRichPresence",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string) },
            null);

        if (method == null)
        {
            return false;
        }

        try
        {
            object result = method.Invoke(null, new object[] { key, value });
            return !(result is bool boolResult) || boolResult;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInvokeSteamFriendsString(string methodName, string value)
    {
        Type steamFriendsType = FindSteamFriendsType();
        if (steamFriendsType == null)
        {
            return false;
        }

        MethodInfo method = steamFriendsType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

        if (method == null)
        {
            return false;
        }

        try
        {
            object result = method.Invoke(null, new object[] { value });
            return !(result is bool boolResult) || boolResult;
        }
        catch
        {
            return false;
        }
    }

    private static Type FindSteamFriendsType()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType("Steamworks.SteamFriends");
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
