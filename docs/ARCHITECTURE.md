# Архітектура WinAudioBooster

Технічний опис внутрішнього устрою проекту. Користувацька документація — [README.md](../README.md).

## Загальна картина

WinAudioBooster — WPF-додаток (.NET 8, C#) на бібліотеках **NAudio** (обробка аудіо) та **AudioSwitcher.AudioApi.CoreAudio** (керування пристроями CoreAudio). Додаток «перехоплює» системний звук через віртуальний аудіо-кабель, обробляє його DSP-ланцюгом і виводить на фізичний пристрій.

## Потік аудіо

```
 ┌─────────────────────┐
 │  Додатки Windows     │  (браузер, ігри, плеєр...)
 │  (системний мікшер)  │
 └──────────┬──────────┘
            │  Windows перемикає default output
            ▼
 ┌─────────────────────┐
 │  VB-Audio CABLE      │  віртуальний пристрій «CABLE Input»
 │  (default output)    │
 └──────────┬──────────┘
            │  WASAPI loopback capture (WasapiLoopbackCapture)
            ▼
 ┌─────────────────────┐
 │  BufferedWaveProvider│  буфер ~2 с, DiscardOnBufferOverflow
 └──────────┬──────────┘
            ▼
 ┌─────────────────────┐
 │  DspProcessor        │  ISampleProvider, обробка по семплах:
 │  1. Gain (до +300%)  │     gain → EQ 10 смуг → компресор →
 │  2. EQ 10 смуг       │     м'який лімітер (ceiling)
 │  3. Компресор        │     LevelMeter: вхідний/вихідний рівні
 │  4. М'який лімітер   │
 └──────────┬──────────┘
            │  ресемплінг під формат пристрою
            ▼
 ┌─────────────────────┐
 │  MediaFoundation     │  (якщо формат != MixFormat пристрою;
 │  Resampler           │   інакше прямий SampleToWaveProvider16)
 └──────────┬──────────┘
            ▼
 ┌─────────────────────┐
 │  WasapiOut (Shared)  │  вивід на фізичний пристрій
 └──────────┬──────────┘
            ▼
 ┌─────────────────────┐
 │  Динаміки / навушники│
 └─────────────────────┘
```

Компоненти ланцюга (клас `Audio.AudioEngine`):

1. **Автоперемикання** — `Utils.AudioDeviceSwitcher` через COM-інтерфейс `IPolicyConfig` призначає «CABLE Input» пристроєм за замовчуванням для ролей Console/Multimedia/Communications. При зупинці повертає попередній пристрій.
2. **Захоплення** — `WasapiLoopbackCapture(captureCableDevice)` знімає loopback-потік із віртуального кабелю.
3. **Буферизація** — `BufferedWaveProvider` згладжує нерівномірність надходження даних (буфер 2 секунди, при переповненні старі дані відкидаються).
4. **DSP** — `Audio.DspProcessor` реалізує `ISampleProvider` і прогоняє кожен семпл через ланцюг: підсилення (gain, до 300% → понад 0 дБFS), еквалайзер, динамічний компресор (порог/співвідношення/атака/відновлення) та м'який лімітер (обмежує піки на заданій стелі, за замовчуванням −0.3 дБ). Вбудовані `LevelMeter` віддають рівні для індикаторів UI.
5. **Вивід** — якщо формат після DSP не збігається з `MixFormat` фізичного пристрою, працює `MediaFoundationResampler`; далі `WasapiOut` у shared-режимі відтворює звук.

## Шари

| Шар / папка | Відповідальність |
|---|---|
| `Audio/` | Аудіо-ядро: рушій, DSP, керування пристроями, вимірювання рівнів |
| `Services/` | Служби застосунку: налаштування, пресети, теми, трей, хоткеї, автозапуск, інсталятор VB-Cable, перевірка оновлень, детектор/мікшер додатків |
| `Models/` | POCO-моделі даних: `AppSettings`, `Preset`, `ProcessRule`, `AppSessionInfo` |
| `Utils/` | Допоміжні утиліти: перемикач пристроїв (COM), JSON, культура/мова |
| `Converters/` | WPF-конвертери для UI (колір/видимість за рівнем gain) |
| `MainWindow/` | Головне вікно — **partial-клас**, розбитий за функціями |
| `Themes/Skins/` | XAML-ресурсні словники тем оформлення |
| `Resources/` | Локалізація (ResourceDictionary `Strings.uk.xaml`, `Strings.en.xaml`) |
| `Deployment/` | Інсталятор (Inno Setup `.iss`) та скрипт видалення |

### MainWindow як partial-клас

Головне вікно свідомо розбите на частини за доменами, щоб не тримати тисячі рядків в одному файлі:

| Файл | Зона відповідальності |
|---|---|
| `MainWindow.xaml.cs` | Ініціалізація, жизнений цикл, обробники базових елементів UI |
| `MainWindow.Audio.cs` | Керування `AudioEngine`: старт/стоп бустера, індикатори рівнів |
| `MainWindow.Settings.cs` | Панель налаштувань, мова, тема, автозапуск, хоткеї |
| `MainWindow.Presets.cs` | Список пресетів, створення/перейменування/видалення |
| `MainWindow.AppMixer.cs` | Мікшер додатків: перелік процесів, правила гучності |

## Схема файлів

```
WinAudioBooster/
├── WinAudioBooster.sln                  # Рішення (додаток + тести)
├── global.json                          # Фіксація версії .NET SDK 8
├── VBCABLE_Driver_Pack45.zip            # Архів драйвера VB-Cable для вбудованого інсталятора
├── .github/workflows/
│   ├── build.yml                        # CI: restore/build/test на push і PR
│   └── release.yml                      # Реліз на тег v*: publish + GitHub Release
├── docs/
│   └── ARCHITECTURE.md                  # Цей документ
├── LICENSE                              # MIT
└── WinAudioBooster/                     # Головний проект (WPF)
    ├── WinAudioBooster.csproj           # net8.0-windows; single-file, self-contained win-x64
    ├── App.xaml / App.xaml.cs           # Точка входу, глобальні ресурси, обробка помилок
    ├── GlobalUsings.cs                  # Глобальні using
    ├── MiniMixerWindow.xaml(.cs)        # Міні-вікно (компактний режим)
    ├── Audio/
    │   ├── AudioEngine.cs               # Оркестрація: capture → buffer → DSP → out
    │   ├── DspProcessor.cs              # Gain, EQ, компресор, лімітер (ISampleProvider)
    │   ├── DeviceManager.cs             # Пошук/перелік аудіопристроїв (MMDevice)
    │   ├── AudioDeviceMonitor.cs        # Відстеження підключення/відключення пристроїв
    │   ├── VolumeManager.cs             # Керування гучністю пристроїв
    │   └── LevelMeter.cs                # Розрахунок рівнів для індикаторів
    ├── Services/
    │   ├── SettingsManager.cs           # Завантаження/збереження appsettings.json
    │   ├── PresetManager.cs             # Вбудовані + власні пресети (presets.json)
    │   ├── ThemeManager.cs              # Застосування тем (merged dictionaries)
    │   ├── TrayService.cs               # Іконка та меню системного лотка
    │   ├── HotkeyManager.cs             # Глобальні гарячі клавіші (RegisterHotKey)
    │   ├── AutoStartService.cs          # Автозапуск з Windows
    │   ├── VbCableInstaller.cs          # Встановлення VB-Cable (локальний zip або завантаження)
    │   ├── UpdateService.cs             # Перевірка оновлень через GitHub Releases API
    │   ├── AppDetector.cs               # Виявлення запущених додатків (сесії аудіо)
    │   └── AppVolumeService.cs          # Індивідуальна гучність додатків (ISimpleAudioVolume)
    ├── Models/
    │   ├── AppSettings.cs               # Стан додатка (gain, компресор, лімітер, тема, мова, хоткей)
    │   ├── Preset.cs                    # Пресет: gain, поріг/співвідношення компресора, стеля лімітера
    │   ├── ProcessRule.cs               # Правило гучності для процесу (мікшер додатків)
    │   └── AppSessionInfo.cs            # Інформація про аудіо-сесію додатка
    ├── Utils/
    │   ├── AudioDeviceSwitcher.cs       # IPolicyConfig: set/restore default device
    │   ├── JsonHelper.cs                # Читання/запис JSON-файлів
    │   └── CultureHelper.cs             # Перемикання мови інтерфейсу (uk/en)
    ├── Converters/
    │   ├── GainToColorConverter.cs      # Колір індикатора залежно від gain
    │   └── GainToWarningVisibilityConverter.cs # Попередження при небезпечному gain
    ├── MainWindow/
    │   ├── MainWindow.xaml              # Розмітка головного вікна
    │   └── MainWindow.*.cs              # Partial-класи (див. таблицю вище)
    ├── Themes/Skins/
    │   ├── Win11Fluent.xaml             # Тема «Windows 11»
    │   ├── ArchLinux.xaml               # Тема «Arch Linux»
    │   ├── WindowsXP.xaml               # Тема «Windows XP»
    │   └── StudioRack.xaml              # Тема «StudioRack» (за замовчуванням)
    ├── Resources/
    │   ├── Strings.uk.xaml              # Українська локалізація
    │   └── Strings.en.xaml              # Англійська локалізація
    └── Deployment/
        ├── WinAudioBooster-Setup.iss    # Скрипт інсталятора (Inno Setup)
        └── Uninstall.ps1                # Скрипт видалення
```

## Потоки даних налаштувань

Конфігураційні файли зберігаються в **`%APPDATA%\WinAudioBooster\`** (а не в реєстрі). Міграція: якщо файл є поруч із exe (`AppDomain.CurrentDomain.BaseDirectory` — стара портативна схема), а в APPDATA його ще немає — він автоматично копіюється туди при першому запуску.

```
┌──────────────┐   LoadSettings()    ┌──────────────┐
│ appsettings. │ ──────────────────► │  Settings    │ ──► AppSettings (Models)
│ json         │ ◄────────────────── │  Manager     │     (gain, EQ, компресор, лімітер,
└──────────────┘   SaveSettings()    └──────────────┘      тема, мова, хоткей, пристрій,
                                                            правила мікшера додатків)
┌──────────────┐   LoadCustomPresets() ┌──────────────┐
│ presets.json │ ────────────────────► │   Preset     │ ──► List<Preset>
│ (власні      │ ◄──────────────────── │   Manager    │     вбудовані пресети зашиті в код
│  пресети)    │   SaveCustomPreset()  └──────────────┘
└──────────────┘
```

- `appsettings.json` — серіалізація `Models.AppSettings` через `System.Text.Json` (з відступами). При відсутності/пошкодженні файлу застосовуються значення за замовчуванням.
- `presets.json` — масив `Models.Preset` (тільки власні; вбудовані п'ять пресетів генерує `PresetManager.GetBuiltInPresets()`). Збереження перезаписує файл ціликом.
- UI читає/пише налаштування лише через менеджери; зміни застосовуються до `DspProcessor` на льоту (без перезапуску аудіо-ланцюга).

## Розширення

### Як додати вбудований пресет

У `Services/PresetManager.cs` додайте елемент у список `GetBuiltInPresets()`:

```csharp
new() {
    Name = "Night Mode",
    IsBuiltIn = true,
    GainDb = 110,
    CompressorThresholdDb = -24,
    CompressorRatio = 3.5f,
    LimiterCeilingDb = -0.4f,
    EqGainsDb = new double[] { 0, 0, 0, 0, 0, 0, 2, 2, 0, 0 } // опційно, 10 смуг
}
```

Поля відповідають `Models/Preset.cs`; `IsBuiltIn = true` означає, що пресет не пишеться у `presets.json` і його не можна видалити з UI.

### Як додати смугу EQ

1. У `Audio/DspProcessor.cs` додайте частоту в статичний масив `BandFrequencies` — фільтри `BiQuadFilter.PeakingEQ` створюються автоматично для кожної смуги й каналу.
2. Діапазон гейну визначають константи `MinBandGainDb` / `MaxBandGainDb` (зараз −12…+12 дБ).
3. У `MainWindow/MainWindow.xaml` додайте слайдер у `UniformGrid` секції «Еквалайзер» (зразок — сусідні 10 слайдерів; частота смуги передається через `Tag`).
4. За потреби додайте значення смуг у пресет (`Models/Preset.cs`, масив `EqGainsDb`).

### Як додати тему

1. Створіть XAML-словник у `Themes/Skins/`, наприклад `Themes/Skins/MyTheme.xaml`, з визначенням кольорів/стилів (зразок — `StudioRack.xaml`).
2. Зареєструйте ім'я в `Services/ThemeManager.cs` у switch-вираженні `ApplyTheme`:

```csharp
"MyTheme" => "Themes/Skins/MyTheme.xaml",
```

3. Додайте запис до списку тем у налаштуваннях UI (`MainWindow.Settings.cs`), щоб тему можна було обрати.
4. Додайте перекази назви теми в `Resources/Strings.uk.xaml` / `Strings.en.xaml`, якщо назва локалізується.

### Як додати мову

1. Створіть `Resources/Strings.<код>.xaml` за зразком `Strings.uk.xaml`.
2. Додайте культуру в `Utils/CultureHelper.cs`.
3. Додайте варіант до вибору мови у налаштуваннях UI.
