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
        _isApplyingPreset = false;
    }

    private void PresetsComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreset || PresetsComboBox.SelectedItem is not Preset selectedPreset) return;

        _isApplyingPreset = true;

        GainSlider.Value = selectedPreset.GainDb > 0 ? selectedPreset.GainDb : 100;
        CompThresholdSlider.Value = selectedPreset.CompressorThresholdDb;
        CompRatioSlider.Value = selectedPreset.CompressorRatio;
        LimiterCeilingSlider.Value = selectedPreset.LimiterCeilingDb;

        // Еквалайзер: слайдери самі синхронізують DSP через EqSlider_OnValueChanged
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
            GainDb = (float)GainSlider.Value,
            CompressorThresholdDb = (float)CompThresholdSlider.Value,
            CompressorRatio = (float)CompRatioSlider.Value,
            LimiterCeilingDb = (float)LimiterCeilingSlider.Value,
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
            current.GainDb = (float)GainSlider.Value;
            current.CompressorThresholdDb = (float)CompThresholdSlider.Value;
            current.CompressorRatio = (float)CompRatioSlider.Value;
            current.LimiterCeilingDb = (float)LimiterCeilingSlider.Value;
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