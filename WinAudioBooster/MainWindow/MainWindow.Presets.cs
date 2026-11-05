namespace WinAudioBooster;

public partial class MainWindow
{
    private void RefreshPresetsList(string? selectPresetName = null)
    {
        _isApplyingPreset = true;
        var presets = _presetManager.LoadAllPresets();
        PresetsComboBox.ItemsSource = presets;

        if (!string.IsNullOrEmpty(selectPresetName))
        {
            var target = presets.FirstOrDefault(p => p.Name.Equals(selectPresetName, StringComparison.OrdinalIgnoreCase));
            PresetsComboBox.SelectedItem = target ?? presets.FirstOrDefault();
        }
        else if (PresetsComboBox.SelectedIndex < 0 && presets.Count > 0)
        {
            PresetsComboBox.SelectedIndex = 0;
        }

        UpdatePresetButtonsState();

        // Синхронізуємо ім'я пресета у ViewModel (джерело правди для збереження)
        _vm.SelectedPresetName = (PresetsComboBox.SelectedItem as Preset)?.Name ?? "Light Boost";

        _isApplyingPreset = false;
    }

    /// <summary>
    /// Спільний метод застосування пресета за назвою (використовується пресетами,
    /// авто-правилами та профілями пристроїв): виставляє слайдери та синхронізує DSP.
    /// </summary>
    internal void ApplyPresetByName(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName)) return;

        var preset = _presetManager.LoadAllPresets().FirstOrDefault(p =>
            p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
        if (preset == null) return;

        _isApplyingPreset = true;
        try
        {
            // Вибір у ComboBox блокується прапорцем _isApplyingPreset, тому застосовуємо значення вручну
            PresetsComboBox.SelectedItem = preset;
            _vm.SelectedPresetName = preset.Name;

            // Слайдери оновляться бідингом; ValueChanged самі синхронізують DSP
            _vm.GainPercent = preset.GainDb > 0 ? preset.GainDb : 100;
            _vm.CompressorThresholdDb = preset.CompressorThresholdDb;
            _vm.CompressorRatio = preset.CompressorRatio;
            _vm.LimiterCeilingDb = preset.LimiterCeilingDb;

            // Еквалайзер: пишемо в ViewModel — слайдери оновить бідинг
            ApplyEqGainsToSliders(preset.EqGainsDb);
        }
        finally
        {
            _isApplyingPreset = false;
        }

        _ = SyncDspParametersAsync();
        UpdatePresetButtonsState();
    }

    private void PresetsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreset || PresetsComboBox.SelectedItem is not Preset selectedPreset) return;

        _isApplyingPreset = true;
        _vm.SelectedPresetName = selectedPreset.Name;

        // Слайдери оновляться бідингом; ValueChanged самі синхронізують DSP
        _vm.GainPercent = selectedPreset.GainDb > 0 ? selectedPreset.GainDb : 100;
        _vm.CompressorThresholdDb = selectedPreset.CompressorThresholdDb;
        _vm.CompressorRatio = selectedPreset.CompressorRatio;
        _vm.LimiterCeilingDb = selectedPreset.LimiterCeilingDb;

        // Еквалайзер: пишемо в ViewModel — слайдери оновить бідинг
        ApplyEqGainsToSliders(selectedPreset.EqGainsDb);

        _ = SyncDspParametersAsync();
        UpdatePresetButtonsState();

        _isApplyingPreset = false;
        SaveCurrentSettings();
    }

    private void UpdatePresetButtonsState()
    {
        if (PresetsComboBox.SelectedItem is Preset current)
        {
            bool isCustom = !current.IsBuiltIn;
            DeletePresetButton.IsEnabled = isCustom;
            UpdatePresetButton.IsEnabled = isCustom;
            RenamePresetButton.IsEnabled = isCustom;
        }
    }

    private void SaveAsPresetButton_OnClick(object sender, RoutedEventArgs e)
    {
        string promptTitle = TryFindResource("Preset_PromptName") as string ?? "Введіть назву пресету:";
        string presetName = Microsoft.VisualBasic.Interaction.InputBox(promptTitle, "WinAudioBooster", "My Custom Preset");

        if (string.IsNullOrWhiteSpace(presetName)) return;

        var newPreset = new Preset
        {
            Name = presetName.Trim(),
            GainDb = (float)_vm.GainPercent,
            CompressorThresholdDb = (float)_vm.CompressorThresholdDb,
            CompressorRatio = (float)_vm.CompressorRatio,
            LimiterCeilingDb = (float)_vm.LimiterCeilingDb,
            EqGainsDb = GetEqGainsFromSliders()
        };

        _presetManager.SaveCustomPreset(newPreset);
        RefreshPresetsList(newPreset.Name);
        SaveCurrentSettings();
    }

    private void RenamePresetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
        {
            string oldName = current.Name;
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Введіть нову назву для пресету:", "WinAudioBooster", oldName);

            if (string.IsNullOrWhiteSpace(newName) || newName.Trim().Equals(oldName, StringComparison.OrdinalIgnoreCase)) return;

            _presetManager.RenameCustomPreset(oldName, newName.Trim());
            RefreshPresetsList(newName.Trim());
            SaveCurrentSettings();
        }
    }

    private void UpdatePresetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
        {
            current.GainDb = (float)_vm.GainPercent;
            current.CompressorThresholdDb = (float)_vm.CompressorThresholdDb;
            current.CompressorRatio = (float)_vm.CompressorRatio;
            current.LimiterCeilingDb = (float)_vm.LimiterCeilingDb;
            current.EqGainsDb = GetEqGainsFromSliders();

            _presetManager.SaveCustomPreset(current);
            SaveCurrentSettings();
        }
    }

    private void DeletePresetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetsComboBox.SelectedItem is Preset current && !current.IsBuiltIn)
        {
            _presetManager.DeleteCustomPreset(current.Name);
            RefreshPresetsList();
            SaveCurrentSettings();
        }
    }
}