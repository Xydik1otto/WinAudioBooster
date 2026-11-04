namespace WinAudioBooster;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CultureInfo initialCulture = CultureHelper.DetectSystemCulture();
        SetCulture(initialCulture);
    }

    /// <summary>
    /// Перемикає мовний словник ресурсів додатку у реальному часі.
    /// </summary>
    public static void SetCulture(CultureInfo culture)
    {
        var dict = new ResourceDictionary();

        if (culture.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase))
        {
            dict.Source = new Uri("Resources/Strings.uk.xaml", UriKind.Relative);
        }
        else
        {
            dict.Source = new Uri("Resources/Strings.en.xaml", UriKind.Relative);
        }

        Current.Resources.MergedDictionaries.Clear();
        Current.Resources.MergedDictionaries.Add(dict);

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}