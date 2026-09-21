using System.Globalization;

namespace WinAudioBooster.Utils
{
    /// <summary>
    /// Допоміжний клас для визначення мови інтерфейсу за мовою операційної системи.
    /// Правила:
    /// - Якщо мова системи українська (uk) → українська.
    /// - Якщо мова системи російська або країни СНД (ru, be, kk тощо) → українська.
    /// - У всіх інших випадках → англійська.
    /// </summary>
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
}