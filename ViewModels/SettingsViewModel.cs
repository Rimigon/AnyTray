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
    private bool _middleClickEnabled;
    private bool _middleClickDirectHide;
    private HotkeyDefinition _hotkey;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }

    /// <summary>true = сохранить, false = отмена.</summary>
    public event Action<bool>? CloseRequested;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var s = settings.Current.Clone();

        _autostartEnabled = s.AutostartEnabled;
        _middleClickEnabled = s.TitleBarMiddleClickEnabled;
        _middleClickDirectHide = s.TitleBarMiddleClickDirectHide;
        _hotkey = s.GetHotkeyDefinition();

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set => SetProperty(ref _autostartEnabled, value);
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

    private void Save()
    {
        // Клонируем текущие настройки, чтобы сохранить неизвестные поля (от будущих версий).
        var updated = _settings.Current.Clone();
        updated.AutostartEnabled = _autostartEnabled;
        updated.Hotkey = _hotkey.ToString();
        updated.TitleBarMiddleClickEnabled = _middleClickEnabled;
        updated.TitleBarMiddleClickDirectHide = _middleClickDirectHide;

        _settings.Save(updated);
        CloseRequested?.Invoke(true);
    }
}
