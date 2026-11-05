# Дистрибуція APO (packaging\apo) — план

> **Статус: план.** Цей пакет поки не змінює жодного інсталятора WinAudioBooster
> (winget/scoop/InnoSetup не торкані). Тут — рецепт, як майбутня збірка з
> `native\WinAudioApo\` має розповсюджуватись і відкочуватись.

## Як це робить Equalizer APO (орієнтир)

Equalizer APO інсталюється під **адміністратором** і робить три речі:

1. Копіює `EqualizerAPO.dll` (+32/64-бітні варіанти) у `Program Files`.
2. Реєструє APO-класи (regsvr32-еквівалент + запис ключів
   `CLSID\{GUID}` і `AudioEngine\AudioProcessingObjects\{GUID}` — у їхньому
   коді це робить Configurator; «RegApo»-стиль).
3. Чіпляє APO до обраних користувачем пристроїв: пише CLSID у
   `HKLM\...\MMDevices\Audio\Render\{endpoint}\FxProperties`, **зберігаючи
   попередні значення** під `HKLM\SOFTWARE\EqualizerAPO\Child APOs`, і
   пропонує перезавантаження/рестарт аудіо.

Джерело рецепту: [Developer documentation (SourceForge wiki)](https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/).

## Наш план дистрибуції (фаза 3 з docs\apo\README-uk.md)

### Що ставимо

```
<ProgramFiles>\WinAudioBooster\APO\
    WinAudioApo.x64.dll      ← Release|x64 (основний для 64-бітного Windows)
    WinAudioApo.x86.dll      ← для 32-бітного Windows 10 (опційно)
    WinAudioApo.ARM64.dll    ← TODO, коли з'явиться платформа у vcxproj
    UninstallApo.exe / .cmd  ← відкат (нижче)
```

### Кроки інсталятора (адмін-права обов'язкові)

1. Копіювання DLL у `Program Files\WinAudioBooster\APO\`.
2. `regsvr32 /s WinAudioApo.x64.dll` (реєстрація COM-класу; наш DLL уміє
   `DllRegisterServer` сам) — плюс до-запис значень
   `AudioEngine\AudioProcessingObjects\{GUID}` (TODO `RegisterAPO` у
   `WinApoReg.cpp` або окремий RegApo-стиль скрипт).
3. Для кожного обраного endpoint'а:
   - прочитати поточні значення `FxProperties` (ефекти Lfx/Gfx-пари) →
     зберегти бекап під `HKLM\SOFTWARE\WinAudioBooster\APO Backup\{endpoint GUID}`
     (наш аналог Child APOs);
   - вписати наш CLSID (`{FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}`);
   - якщо APO непідписаний (dev/бета-збірки) — поставити
     `HKLM\...\CurrentVersion\Audio → DisableProtectedAudioDG = 1` **лише після
     явної згоди користувача** і попередження про DRM-застосунки.
4. Пропозиція рестарту аудіосервісу / перезавантаження.
5. Запис у uninstall-журнал (HKLM Uninstall або InnoSetup `[UninstallRun]`).

### Відкат (критично)

- Uninstaller: `regsvr32 /u`, відновлення бекап-значень `FxProperties` з
  `HKLM\SOFTWARE\WinAudioBooster\APO Backup\`, видалення
  `DisableProtectedAudioDG` (якщо ставили), видалення файлів.
- Кожен реєстровий запис мусить бути **ідемпотентним і зворотним**; перед
  записом завжди спершу читати та зберігати те, що було.
- Аварійний режим: якщо після інсталяції звук «пропав» — користувач запускає
  `UninstallApo.cmd`, який відновлює бекап і рестартує аудіо. Це має працювати
  навіть якщо наш WPF-застосунок не запускається взагалі.

### Ризики

| Ризик | Мітигація |
|-------|-----------|
| Крах APO = крах аудіо системи | Ставити тільки тестовані Release-збірки; error-лог; відкат одним cmd |
| Secure audio path / DRM ламається від `DisableProtectedAudioDG` | Опція за замовчуванням ВИКЛ; документувати; для релізу — підписаний DLL |
| Перезапис штатних ефектів драйвера | Обов'язковий бекап FxProperties + ланцюжок «наш APO поверх child APO» (патерн Equalizer APO) |
| Оновлення Windows скидає ефекти endpoint'а | Re-apply під час запуску WPF-конфігуратора (перевірка реєстру + авто-реставрація) |
| Розбіжність архітектур (ARM64, x86) | Інсталятор обирає DLL під архітектуру ОС; на ARM64 поки що відмовлятись з поясненням |
| Підписання | Реліз: Authenticode-підпис DLL (як і основний застосунок); dev: DisableProtectedAudioDG |

## Зв'язок із WPF-конфігуратором

- Конфігуратор пише `%APPDATA%\WinAudioBooster\apo-config.ini`
  (`GainDb`, `EqGainsDb[10]`, `LimiterCeilingDb`) — той самий формат, що
  `native\WinAudioApo\apo-config.ini.example`; APO підхоплює зміни за
  LastWriteTime без рестарту.
- UI-план: розділ «Системний режим (APO)» — статус інсталяції/кріплення,
  кнопка «Застосувати до пристрою», лог останніх дій.
- TODO: канал миттєвого push (Named pipe) замість polling'у — див.
  docs\apo\README-uk.md, фаза 2.
- Інсталяційний спрощений шлях для користувача: WPF-застосунок викликає
  elevation (UAC) на маленький helper, який виконує кроки 1–4 вище; уся
  реєстрова логіка — в helper'і, не в самому WPF (щоб можна було перевикликати
  з CLI для відкату).
