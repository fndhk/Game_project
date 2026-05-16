using System;
using UnityEngine;

public static class GameInputBindings
{
    public const string MoveForwardKey = "setting_key_move_forward";
    public const string MoveBackwardKey = "setting_key_move_backward";
    public const string MoveLeftKey = "setting_key_move_left";
    public const string MoveRightKey = "setting_key_move_right";
    public const string SprintKey = "setting_key_sprint";
    public const string CrouchKey = "setting_key_crouch";
    public const string InteractKey = "setting_key_interact";
    public const string PickupKey = "setting_key_pickup";
    public const string ScanKey = "setting_key_scan";
    public const string UseItemKey = "setting_key_use_item";
    public const string DropItemKey = "setting_key_drop_item";
    public const string Slot1Key = "setting_key_slot_1";
    public const string Slot2Key = "setting_key_slot_2";
    public const string MicMuteKey = "setting_key_mic_mute";
    public const string KillKey = "setting_key_kill";
    public const string PauseKey = "setting_key_pause";

    private static readonly string[] AllPrefsKeys =
    {
        MoveForwardKey,
        MoveBackwardKey,
        MoveLeftKey,
        MoveRightKey,
        SprintKey,
        CrouchKey,
        InteractKey,
        PickupKey,
        ScanKey,
        UseItemKey,
        DropItemKey,
        Slot1Key,
        Slot2Key,
        MicMuteKey,
        KillKey,
        PauseKey
    };

    public static KeyCode MoveForward => GetKey(MoveForwardKey, KeyCode.W);
    public static KeyCode MoveBackward => GetKey(MoveBackwardKey, KeyCode.S);
    public static KeyCode MoveLeft => GetKey(MoveLeftKey, KeyCode.A);
    public static KeyCode MoveRight => GetKey(MoveRightKey, KeyCode.D);
    public static KeyCode Sprint => GetKey(SprintKey, KeyCode.LeftShift);
    public static KeyCode Crouch => GetKey(CrouchKey, KeyCode.LeftControl);
    public static KeyCode Interact => GetKey(InteractKey, KeyCode.E);
    public static KeyCode Pickup => GetKey(PickupKey, KeyCode.F);
    public static KeyCode Scan => GetKey(ScanKey, KeyCode.Mouse1);
    public static KeyCode UseItem => GetKey(UseItemKey, KeyCode.Mouse0);
    public static KeyCode DropItem => GetKey(DropItemKey, KeyCode.G);
    public static KeyCode Slot1 => GetKey(Slot1Key, KeyCode.Alpha1);
    public static KeyCode Slot2 => GetKey(Slot2Key, KeyCode.Alpha2);
    public static KeyCode MicMute => GetKey(MicMuteKey, KeyCode.B);
    public static KeyCode Kill => GetKey(KillKey, KeyCode.Q);
    public static KeyCode Pause => GetKey(PauseKey, KeyCode.Escape);

    public static KeyCode GetKey(string prefsKey, KeyCode fallback)
    {
        string raw = PlayerPrefs.GetString(prefsKey, fallback.ToString());
        return Enum.TryParse(raw, out KeyCode key) && IsBindableKey(key) ? key : fallback;
    }

    public static void SetKey(string prefsKey, KeyCode key)
    {
        if (!IsBindableKey(key))
        {
            return;
        }

        PlayerPrefs.SetString(prefsKey, key.ToString());
        PlayerPrefs.Save();
    }

    public static bool IsPressed(string prefsKey, KeyCode fallback)
    {
        return Input.GetKey(GetKey(prefsKey, fallback));
    }

    public static bool GetKeyDown(string prefsKey, KeyCode fallback)
    {
        return Input.GetKeyDown(GetKey(prefsKey, fallback));
    }

    public static bool GetKeyUp(string prefsKey, KeyCode fallback)
    {
        return Input.GetKeyUp(GetKey(prefsKey, fallback));
    }

    public static string GetLabel(string prefsKey, KeyCode fallback)
    {
        return FormatKey(GetKey(prefsKey, fallback));
    }

    public static string FormatKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "LMB";
            case KeyCode.Mouse1: return "RMB";
            case KeyCode.Mouse2: return "MMB";
            case KeyCode.Mouse3: return "MOUSE 4";
            case KeyCode.Mouse4: return "MOUSE 5";
            case KeyCode.Mouse5: return "MOUSE 6";
            case KeyCode.Mouse6: return "MOUSE 7";
            case KeyCode.LeftShift: return "L SHIFT";
            case KeyCode.RightShift: return "R SHIFT";
            case KeyCode.LeftControl: return "L CTRL";
            case KeyCode.RightControl: return "R CTRL";
            case KeyCode.LeftAlt: return "L ALT";
            case KeyCode.RightAlt: return "R ALT";
            case KeyCode.Escape: return "ESC";
            case KeyCode.Space: return "SPACE";
            case KeyCode.Return: return "ENTER";
            case KeyCode.Backspace: return "BACKSPACE";
            case KeyCode.Tab: return "TAB";
            case KeyCode.CapsLock: return "CAPS";
            case KeyCode.Alpha0: return "0";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            default:
                return key.ToString().ToUpperInvariant();
        }
    }

    public static bool TryGetPressedBindableKey(out KeyCode key)
    {
        Array values = Enum.GetValues(typeof(KeyCode));

        for (int i = 0; i < values.Length; i++)
        {
            KeyCode candidate = (KeyCode)values.GetValue(i);
            if (IsBindableKey(candidate) && Input.GetKeyDown(candidate))
            {
                key = candidate;
                return true;
            }
        }

        key = KeyCode.None;
        return false;
    }

    public static bool IsBindableKey(KeyCode key)
    {
        if (key == KeyCode.None)
        {
            return false;
        }

        string name = key.ToString();
        return !name.StartsWith("Joystick", StringComparison.Ordinal);
    }

    public static Vector2 GetMoveInput()
    {
        float x = GetAxis(MoveRight, MoveLeft);
        float y = GetAxis(MoveForward, MoveBackward);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    public static void ResetAll()
    {
        for (int i = 0; i < AllPrefsKeys.Length; i++)
        {
            PlayerPrefs.DeleteKey(AllPrefsKeys[i]);
        }

        PlayerPrefs.Save();
    }

    private static float GetAxis(KeyCode positive, KeyCode negative)
    {
        float value = 0f;

        if (Input.GetKey(positive))
        {
            value += 1f;
        }

        if (Input.GetKey(negative))
        {
            value -= 1f;
        }

        return value;
    }
}
