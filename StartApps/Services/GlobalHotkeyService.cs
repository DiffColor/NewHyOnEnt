using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StartApps.Models;

namespace StartApps.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly object _gate = new();
    private readonly Dictionary<int, HotkeyRegistration> _registrationsById = new();
    private readonly Dictionary<Guid, HotkeyRegistration> _registrationsByAppId = new();
    private HwndSource? _source;
    private IntPtr _handle;
    private int _nextRegistrationId = 0x2000;
    private bool _disposed;

    public event EventHandler<Guid>? HotkeyPressed;

    public void Initialize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        lock (_gate)
        {
            ThrowIfDisposed();

            if (_source != null)
            {
                return;
            }

            var helper = new WindowInteropHelper(window);
            _handle = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_handle) ?? throw new InvalidOperationException("StartApps 단축키 윈도우 핸들을 만들지 못했습니다.");
            _source.AddHook(WndProc);
        }
    }

    public GlobalHotkeyAvailabilityResult CanRegister(AppHotkey hotkey, Guid? ignoreAppId = null)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureInitialized();

            if (!hotkey.HasValue)
            {
                return GlobalHotkeyAvailabilityResult.Available;
            }

            if (ignoreAppId.HasValue
                && _registrationsByAppId.TryGetValue(ignoreAppId.Value, out var currentRegistration)
                && currentRegistration.Hotkey == hotkey)
            {
                return GlobalHotkeyAvailabilityResult.Available;
            }

            if (_registrationsByAppId.Values.Any(x => x.AppId != ignoreAppId && x.Hotkey == hotkey))
            {
                return GlobalHotkeyAvailabilityResult.Unavailable("이미 다른 앱에 등록된 단축키입니다.");
            }

            var registrationId = GetNextRegistrationId();
            if (!RegisterHotKey(_handle, registrationId, hotkey.ToNativeModifiers() | ModNoRepeat, (uint)hotkey.ToVirtualKey()))
            {
                return GlobalHotkeyAvailabilityResult.Unavailable("이미 다른 프로그램에서 사용 중이거나 등록할 수 없습니다.");
            }

            UnregisterHotKey(_handle, registrationId);
            return GlobalHotkeyAvailabilityResult.Available;
        }
    }

    public GlobalHotkeySyncResult Synchronize(IEnumerable<AppDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        lock (_gate)
        {
            ThrowIfDisposed();
            EnsureInitialized();

            UnregisterAllCore();

            var failures = new List<GlobalHotkeySyncFailure>();
            var seenHotkeys = new Dictionary<AppHotkey, string>();

            foreach (var definition in definitions)
            {
                if (definition.ToggleShortcutKey == System.Windows.Input.Key.None)
                {
                    continue;
                }

                if (!AppHotkey.TryCreate(definition.ToggleShortcutModifiers, definition.ToggleShortcutKey, out var hotkey, out var errorMessage))
                {
                    failures.Add(new GlobalHotkeySyncFailure(definition.Id, GetDisplayName(definition), errorMessage));
                    continue;
                }

                if (seenHotkeys.TryGetValue(hotkey, out var duplicatedName))
                {
                    failures.Add(new GlobalHotkeySyncFailure(definition.Id, GetDisplayName(definition), $"{duplicatedName} 앱과 단축키가 중복됩니다."));
                    continue;
                }

                var registrationId = GetNextRegistrationId();
                if (!RegisterHotKey(_handle, registrationId, hotkey.ToNativeModifiers() | ModNoRepeat, (uint)hotkey.ToVirtualKey()))
                {
                    failures.Add(new GlobalHotkeySyncFailure(definition.Id, GetDisplayName(definition), "이미 다른 프로그램에서 사용 중이거나 등록할 수 없습니다."));
                    continue;
                }

                var registration = new HotkeyRegistration(registrationId, definition.Id, hotkey);
                _registrationsById[registrationId] = registration;
                _registrationsByAppId[definition.Id] = registration;
                seenHotkeys[hotkey] = GetDisplayName(definition);
            }

            return new GlobalHotkeySyncResult(failures);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            UnregisterAllCore();

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }

            _handle = IntPtr.Zero;
            _disposed = true;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotKey)
        {
            return IntPtr.Zero;
        }

        Guid? appId = null;

        lock (_gate)
        {
            if (_registrationsById.TryGetValue(wParam.ToInt32(), out var registration))
            {
                appId = registration.AppId;
            }
        }

        if (appId.HasValue)
        {
            HotkeyPressed?.Invoke(this, appId.Value);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void EnsureInitialized()
    {
        if (_source == null || _handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("전역 단축키 서비스가 아직 초기화되지 않았습니다.");
        }
    }

    private void UnregisterAllCore()
    {
        if (_handle != IntPtr.Zero)
        {
            foreach (var registration in _registrationsById.Values)
            {
                UnregisterHotKey(_handle, registration.RegistrationId);
            }
        }

        _registrationsById.Clear();
        _registrationsByAppId.Clear();
    }

    private int GetNextRegistrationId() => _nextRegistrationId++;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(GlobalHotkeyService));
        }
    }

    private static string GetDisplayName(AppDefinition definition) =>
        string.IsNullOrWhiteSpace(definition.Name) ? definition.Type.ToString() : definition.Name;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly record struct HotkeyRegistration(int RegistrationId, Guid AppId, AppHotkey Hotkey);
}

public sealed record GlobalHotkeyAvailabilityResult(bool IsAvailable, string? ErrorMessage)
{
    public static GlobalHotkeyAvailabilityResult Available { get; } = new(true, null);

    public static GlobalHotkeyAvailabilityResult Unavailable(string message) => new(false, message);
}

public sealed record GlobalHotkeySyncFailure(Guid AppId, string AppName, string ErrorMessage);

public sealed class GlobalHotkeySyncResult
{
    public GlobalHotkeySyncResult(IReadOnlyList<GlobalHotkeySyncFailure> failures)
    {
        Failures = failures;
    }

    public IReadOnlyList<GlobalHotkeySyncFailure> Failures { get; }

    public bool HasFailures => Failures.Count > 0;
}
