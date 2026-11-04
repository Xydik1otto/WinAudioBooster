namespace WinAudioBooster.Services;

public static class ThemeManager
{
    public static void ApplyTheme(string themeName)
    {
        string sourcePath = themeName switch
        {
            "Arch Linux" => "Themes/Skins/ArchLinux.xaml",
            "Windows XP" => "Themes/Skins/WindowsXP.xaml",
            "Windows 11" => "Themes/Skins/Win11Fluent.xaml",
            "Minimalism" => "Themes/Skins/Minimalism.xaml",
            "Medieval RPG" => "Themes/Skins/Medieval.xaml",
            _ => "Themes/Skins/StudioRack.xaml"
        };

        try
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            var existingSkin = mergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/Skins/"));
            if (existingSkin != null)
            {
                mergedDictionaries.Remove(existingSkin);
            }

            var dict = new ResourceDictionary
            {
                Source = new Uri(sourcePath, UriKind.Relative)
            };

            mergedDictionaries.Add(dict);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Theme error: {ex.Message}");
        }
    }
}