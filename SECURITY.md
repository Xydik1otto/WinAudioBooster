# Security Policy / Політика безпеки

## Підтримувані версії

Безпекові виправлення випускаються для останньої версії з GitHub Releases.

| Версія | Підтримка |
| ------ | --------- |
| 1.1.x  | ✅ підтримується |
| 1.0.x  | ❌ (оновлюйтесь до актуальної) |

## Як повідомити про вразливість

**Будь ласка, не публікуйте вразливості у відкритих Issues.**

1. **Рекомендовано — GitHub Security Advisories:** скористайтеся вкладкою *Security → Report a vulnerability* у репозиторії [Xydik1otto/WinAudioBooster](https://github.com/Xydik1otto/WinAudioBooster/security/advisories/new). Діалог приватний, і ви зможете узгодити з мейнтейнером розкриття та кредити.

2. **Альтернатива — email:** напишіть на **`security@example.com`** *(плейсхолдер — замініть на реальну адресу мейнтейнера)* з темою `[security] WinAudioBooster: <короткий опис>`.

## Що додати до звіту

- версію додатка (з GitHub Releases або `dotnet publish`);
- кроки відтворення / PoC;
- оцінку впливу (що саме може зробити зловмисник);
- ваші контактні дані для зворотного зв'язку.

## Що очікувати

- Підтвердження отримання — протягом **72 годин**.
- Оцінка та план виправлення — протягом 7 днів.
- Публікація виправлення та координоване розкриття за домовленістю (зазвичай після виходу патч-релізу).

## Сфера охоплення (scope)

- Код репозиторію (WPF-додаток, інсталяційні скрипти, CI).
- Обробка локальних файлів налаштувань (`appsettings.json`, `presets.json` у `%APPDATA%\WinAudioBooster`).
- Перевірка оновлень через GitHub Releases (цілісність завантажень).

Позаскоупові питання (VB-Audio CABLE драйвер, сам drivers pack від VB-Audio) не є частиною цього проекта — повідомляйте про них напряму виробнику.

---

## English (Summary)

Please **do not open public issues** for security vulnerabilities. Report via **GitHub Security Advisories** (Security → Report a vulnerability) or email **`security@example.com`** *(placeholder — replace with the maintainer's real address)*. Include the app version, reproduction steps, and impact assessment. Acknowledgement within 72 hours; fixes are released and disclosed in a coordinated manner.
