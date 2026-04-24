using System.Windows;
using System.Windows.Input;
using StartApps.Models;
using Wpf.Ui.Controls;

namespace StartApps.Views.Dialogs;

public partial class ShortcutCaptureWindow : FluentWindow
{
    public ShortcutCaptureWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public AppHotkey? ResultHotkey { get; private set; }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Keyboard.Focus(CaptureRoot);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            DialogResult = false;
            return;
        }

        if (IsModifierKey(key))
        {
            CapturedShortcutText.Text = "입력 대기 중";
            HelpText.Text = "Ctrl, Alt, Shift, Win과 일반 키를 함께 누르거나 F1~F24를 단독으로 눌러주세요.";
            return;
        }

        if (!AppHotkey.TryCreate(Keyboard.Modifiers, key, out var hotkey, out var errorMessage))
        {
            CapturedShortcutText.Text = "입력 대기 중";
            HelpText.Text = errorMessage;
            return;
        }

        CapturedShortcutText.Text = hotkey.ToDisplayString();
        ResultHotkey = hotkey;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;
}
