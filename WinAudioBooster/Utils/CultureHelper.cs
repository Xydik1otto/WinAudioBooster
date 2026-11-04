namespace WinAudioBooster.Utils;

public static class CultureHelper
{
    public static CultureInfo DetectSystemCulture()
    {
        var uiCulture = CultureInfo.CurrentUICulture;
        var lang = uiCulture.TwoLetterISOLanguageName.ToLowerInvariant();

        if (lang == "uk" || lang == "ru" || lang == "be" || lang == "kk")
        {
            return new CultureInfo("uk-UA");
        }

        return new CultureInfo("en-US");
    }
}