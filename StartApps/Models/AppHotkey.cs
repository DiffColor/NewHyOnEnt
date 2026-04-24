using System.Windows.Input;

namespace StartApps.Models;

public readonly record struct AppHotkey(ModifierKeys Modifiers, Key PrimaryKey)
{
    public static AppHotkey Empty => new(ModifierKeys.None, Key.None);

    public bool HasValue => PrimaryKey != Key.None;

    public static bool TryCreate(ModifierKeys modifiers, Key primaryKey, out AppHotkey hotkey, out string errorMessage)
    {
        hotkey = Empty;

        var normalizedModifiers = NormalizeModifiers(modifiers);
        var normalizedKey = primaryKey;

        if (IsModifierKey(normalizedKey) || normalizedKey == Key.None)
        {
            errorMessage = "단축키에 사용할 일반 키를 함께 눌러주세요.";
            return false;
        }

        if (normalizedModifiers == ModifierKeys.None && !IsStandaloneFunctionKey(normalizedKey))
        {
            errorMessage = "단축키에는 Ctrl, Alt, Shift, Win 중 하나 이상이 필요합니다. 단, F1~F24는 단독으로 등록할 수 있습니다.";
            return false;
        }

        if (KeyInterop.VirtualKeyFromKey(normalizedKey) <= 0)
        {
            errorMessage = "지원하지 않는 키 조합입니다.";
            return false;
        }

        hotkey = new AppHotkey(normalizedModifiers, normalizedKey);
        errorMessage = string.Empty;
        return true;
    }

    public uint ToNativeModifiers()
    {
        var modifiers = 0u;

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= 0x0001;
        }

        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= 0x0002;
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= 0x0004;
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= 0x0008;
        }

        return modifiers;
    }

    public int ToVirtualKey() => KeyInterop.VirtualKeyFromKey(PrimaryKey);

    public string ToDisplayString()
    {
        if (!HasValue)
        {
            return "등록 안 됨";
        }

        var parts = new List<string>(5);

        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(GetKeyDisplay(PrimaryKey));
        return string.Join("+", parts);
    }

    private static ModifierKeys NormalizeModifiers(ModifierKeys modifiers) =>
        modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows);

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;

    private static bool IsStandaloneFunctionKey(Key key) =>
        key >= Key.F1 && key <= Key.F24;

    private static string GetKeyDisplay(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString().ToUpperInvariant();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return (((int)key) - (int)Key.D0).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return $"Num{((int)key) - (int)Key.NumPad0}";
        }

        return key switch
        {
            Key.Prior => "PageUp",
            Key.Next => "PageDown",
            Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Space => "Space",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Tab => "Tab",
            Key.Capital => "CapsLock",
            Key.Snapshot => "PrintScreen",
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            _ => key.ToString()
        };
    }
}
