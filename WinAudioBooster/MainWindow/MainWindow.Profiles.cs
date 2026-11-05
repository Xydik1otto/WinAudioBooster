using System.Collections.ObjectModel;

namespace WinAudioBooster;

public partial class MainWindow
{
    // Поточний список прив'язок «пристрій виводу → пресет» (редагований у UI, збережений у налаштуваннях)
    private ObservableCollection<DevicePresetBinding> _deviceBindings = new();

    // Прапорець: у цей момент застосовуємо пресет за профілем пристрою (захист від рекурсії)
    private bool _isApplyingDeviceProfile = false;

    /// <summary>Ініціалізація блоку «Профілі пристроїв»: завантаження прив'язок, списки та моніторинг пристроїв.</summary>
    private void InitializeDeviceProfiles()
    {
        _deviceBindings = new ObservableCollection<DevicePresetBinding>();
        DeviceProfilesItemsControl.ItemsSource = _deviceBindings;

        // Завантаження збережених прив'язок при старті
        var settings = _settingsManager.LoadSettings();
        foreach (var binding in settings.DevicePresetBindings)
        {
            _deviceBindings.Add(binding);
        }

        RefreshDeviceProfileDeviceList();
        RefreshDeviceProfilePresetList();

        // Підписка на зміни пристроїв: оновлюємо список і перевіряємо дефолтний пристрій
        _deviceMonitor.DevicesOrStateChanged += () =>
        {
            Dispatcher.Invoke(() =>
            {
                RefreshDeviceProfileDeviceList();
                CheckDeviceProfileForDefaultDevice();
            });
        };
    }

    /// <summary>Заповнює ComboBox пристроїв (те саме джерело, що й для OutputDeviceComboBox).</summary>
    private void RefreshDeviceProfileDeviceList()
    {
        var devices = _deviceManager.GetPhysicalOutputDevices();
        ProfilesDeviceComboBox.ItemsSource = devices;
        if (devices.Count > 0 && ProfilesDeviceComboBox.SelectedIndex < 0)
        {
            ProfilesDeviceComboBox.SelectedIndex = 0;
        }
    }

    /// <summary>Заповнює ComboBox пресетів (джерело — _presetManager.LoadAllPresets()).</summary>
    private void RefreshDeviceProfilePresetList()
    {
        var presets = _presetManager.LoadAllPresets();
        ProfilesPresetComboBox.ItemsSource = presets;
        ProfilesPresetComboBox.DisplayMemberPath = nameof(Preset.Name);
        if (presets.Count > 0 && ProfilesPresetComboBox.SelectedIndex < 0)
        {
            ProfilesPresetComboBox.SelectedIndex = 0;
        }
    }

    /// <summary>Кнопка «Додати прив'язку»: одна прив'язка на пристрій, повторне додавання замінює пресет.</summary>
    private void AddDeviceProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesDeviceComboBox.SelectedItem is not AudioDeviceInfo device) return;
        if (ProfilesPresetComboBox.SelectedItem is not Preset preset) return;

        var existing = _deviceBindings.FirstOrDefault(b => b.DeviceId == device.Id);
        if (existing != null)
        {
            _deviceBindings.Remove(existing);
        }

        _deviceBindings.Add(new DevicePresetBinding
        {
            DeviceId = device.Id,
            DeviceName = device.Name,
            PresetName = preset.Name
        });

        SaveDeviceBindings();
    }

    /// <summary>Кнопка видалення біля кожної прив'язки.</summary>
    private void DeleteDeviceProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string deviceId }) return;

        var binding = _deviceBindings.FirstOrDefault(b => b.DeviceId == deviceId);
        if (binding == null) return;

        _deviceBindings.Remove(binding);
        SaveDeviceBindings();
    }

    /// <summary>Синхронне збереження прив'язок у налаштування (load-modify-save, як для інших правил).</summary>
    private void SaveDeviceBindings()
    {
        var settings = _settingsManager.LoadSettings();
        settings.DevicePresetBindings = _deviceBindings.ToList();
        _settingsManager.SaveSettings(settings);
    }

    /// <summary>
    /// Перевіряє дефолтний render-пристрій: якщо для нього є прив'язка — застосовує пресет.
    /// Викликається при зміні пристроїв (DevicesOrStateChanged).
    /// </summary>
    private void CheckDeviceProfileForDefaultDevice()
    {
        if (_isApplyingDeviceProfile) return;

        try
        {
            using var defaultDevice = _deviceManager.GetDefaultRenderDevice();
            if (defaultDevice == null) return;

            string defaultId = defaultDevice.ID;
            string defaultName;
            try { defaultName = defaultDevice.FriendlyName; } catch { defaultName = string.Empty; }

            var binding = _deviceBindings.FirstOrDefault(b => b.DeviceId == defaultId)
                ?? _deviceBindings.FirstOrDefault(b => !string.IsNullOrEmpty(b.DeviceName) && b.DeviceName == defaultName);
            if (binding == null) return;

            var preset = _presetManager.LoadAllPresets().FirstOrDefault(p =>
                p.Name.Equals(binding.PresetName, StringComparison.OrdinalIgnoreCase));
            if (preset == null) return;

            _isApplyingDeviceProfile = true;
            try
            {
                ApplyPresetByName(preset.Name);
                SaveCurrentSettings();
            }
            finally
            {
                _isApplyingDeviceProfile = false;
            }
        }
        catch
        {
            // Зміна пристрою не критична: просто не застосовуємо профіль
        }
    }
}
