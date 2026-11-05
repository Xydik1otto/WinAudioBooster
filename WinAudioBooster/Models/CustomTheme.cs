namespace WinAudioBooster.Models;

/// <summary>
/// Користувацька колірна гама (custom color palette), створена користувачем
/// з поточної теми кнопкою «Створити з поточної...».
/// Усі поля — hex-рядки формату #RRGGBB (може бути з альфа-каналом #AARRGGBB).
/// Відповідність пензлям застосунку:
///   Background   → RazerBg
///   Card         → RazerCardBg
///   CardHeader   → RazerCardHeaderBg
///   Border       → RazerBorder
///   BorderActive → RazerBorderActive
///   TextMain     → RazerTextMain
///   TextMuted    → RazerTextMuted
///   Accent       → RazerGreen (головний акцент)
/// </summary>
public class CustomTheme
{
    /// <summary>Назва теми (відображається у ComboBox тем). Унікальність перевіряється при створенні.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Фон вікна (RazerBg).</summary>
    public string Background { get; set; } = string.Empty;

    /// <summary>Фон карток/GroupBox (RazerCardBg).</summary>
    public string Card { get; set; } = string.Empty;

    /// <summary>Фон шапки картки (RazerCardHeaderBg).</summary>
    public string CardHeader { get; set; } = string.Empty;

    /// <summary>Межа карток і контролів (RazerBorder).</summary>
    public string Border { get; set; } = string.Empty;

    /// <summary>Активна (виділена) межа (RazerBorderActive).</summary>
    public string BorderActive { get; set; } = string.Empty;

    /// <summary>Основний текст (RazerTextMain).</summary>
    public string TextMain { get; set; } = string.Empty;

    /// <summary>Приглушений текст (RazerTextMuted).</summary>
    public string TextMuted { get; set; } = string.Empty;

    /// <summary>Головний акцентний колір (RazerGreen).</summary>
    public string Accent { get; set; } = string.Empty;

    /// <summary>
    /// Істина, якщо всі кольорові поля заповнені непорожніми рядками.
    /// Порожні поля при застосуванні пропускаються (пензель лишається від базового скіна).
    /// </summary>
    public bool HasAnyColor =>
        !string.IsNullOrWhiteSpace(Background) || !string.IsNullOrWhiteSpace(Card) ||
        !string.IsNullOrWhiteSpace(CardHeader) || !string.IsNullOrWhiteSpace(Border) ||
        !string.IsNullOrWhiteSpace(BorderActive) || !string.IsNullOrWhiteSpace(TextMain) ||
        !string.IsNullOrWhiteSpace(TextMuted) || !string.IsNullOrWhiteSpace(Accent);
}
