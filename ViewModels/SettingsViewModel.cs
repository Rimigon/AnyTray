using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnyTray.Models;
using AnyTray.Services;

namespace AnyTray.ViewModels;

/// <summary>VM окна настроек. Работает с копией настроек; применяет их через SettingsService.Save.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    private bool _autostartEnabled;
    private bool _overlayEnabled;
    private bool _overlayHoverOnly;
    private bool _middleClickEnabled;
    private bool _middleClickDirectHide;
    private HotkeyDefinition _hotkey;
    private string _blacklistText;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    /// <summary>true = сохранить, false = отмена.</summary>
    public event Action<bool>? CloseRequested;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var s = settings.Current.Clone();

        _autostartEnabled = s.AutostartEnabled;
        _overlayEnabled = s.OverlayEnabled;
        _overlayHoverOnly = s.OverlayMode == OverlayDisplayMode.OnHoverOnly;
        _middleClickEnabled = s.TitleBarMiddleClickEnabled;
        _middleClickDirectHide = s.TitleBarMiddleClickDirectHide;
        _hotkey = s.GetHotkeyDefinition();
        _blacklistText = string.Join(Environment.NewLine, s.OverlayBlacklist);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set => SetProperty(ref _autostartEnabled, value);
    }

    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set => SetProperty(ref _overlayEnabled, value);
    }

    public bool OverlayHoverOnly
    {
        get => _overlayHoverOnly;
        set => SetProperty(ref _overlayHoverOnly, value);
    }

    public bool MiddleClickEnabled
    {
        get => _middleClickEnabled;
        set => SetProperty(ref _middleClickEnabled, value);
    }

    public bool MiddleClickDirectHide
    {
        get => _middleClickDirectHide;
        set => SetProperty(ref _middleClickDirectHide, value);
    }

    public HotkeyDefinition Hotkey
    {
        get => _hotkey;
        set
        {
            if (SetProperty(ref _hotkey, value))
                OnPropertyChanged(nameof(HotkeyDisplay));
        }
    }

    public string HotkeyDisplay => _hotkey.ToString();

    public string BlacklistText
    {
        get => _blacklistText;
        set => SetProperty(ref _blacklistText, value);
    }

    private void Save()
    {
        var blacklist = _blacklistText
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updated = new AppSettings
        {
            AutostartEnabled = _autostartEnabled,
            OverlayEnabled = _overlayEnabled,
            OverlayMode = _overlayHoverOnly ? OverlayDisplayMode.OnHoverOnly : OverlayDisplayMode.AlwaysWhenForeground,
            Hotkey = _hotkey.ToString(),
            OverlayBlacklist = blacklist,
            TitleBarMiddleClickEnabled = _middleClickEnabled,
            TitleBarMiddleClickDirectHide = _middleClickDirectHide,
            SchemaVersion = _settings.Current.SchemaVersion
        };

        _settings.Save(updated);
        CloseRequested?.Invoke(true);
    }
}
