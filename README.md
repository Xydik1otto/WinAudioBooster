# WinAudioBooster 🎚️

<div align="center">

[![Build](https://img.shields.io/github/actions/workflow/status/Xydik1otto/WinAudioBooster/build.yml?branch=main&label=build&logo=github)](https://github.com/Xydik1otto/WinAudioBooster/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/actions/workflow/status/Xydik1otto/WinAudioBooster/release.yml?label=release&logo=github)](https://github.com/Xydik1otto/WinAudioBooster/actions/workflows/release.yml)
[![GitHub Release](https://img.shields.io/github/v/release/Xydik1otto/WinAudioBooster?logo=github)](https://github.com/Xydik1otto/WinAudioBooster/releases)
[![License: MIT](https://img.shields.io/github/license/Xydik1otto/WinAudioBooster)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)

</div>

<p align="center">
  <img src="docs/screenshots/main-window.png" alt="Головне вікно WinAudioBooster" width="700">
</p>

**WinAudioBooster** — це програма для Windows, яка підсилює гучність системного звуку до **300%** та додає професійну DSP-обробку: 10-смуговий еквалайзер, компресор і м'який лімітер. Додаток працює через віртуальний аудіо-кабель VB-Audio CABLE: усі звуки Windows перенаправляються на віртуальний пристрій, захоплюються через WASAPI loopback, обробляються і виводяться на реальні динаміки.

## ✨ Можливості

- 🔊 **Бустер гучності до 300%** — підсилення понад 100% реалізується через програмний DSP-gain, а не стандартний мікшер Windows.
- 🎛️ **10-смуговий еквалайзер** — тонке налаштування тембру в реальному часі.
- 🗜️ **Компресор** — поріг, співвідношення, атака та відновлення (attack/release) для вирівнювання динаміки.
- 🛑 **М'який лімітер** — стеля (ceiling) в дБ, захищає від перевантаження та кліпінгу.
- 💾 **Пресети** — вбудовані (Light Boost, Max Boost, Headphones, Games, Calls, **Night Mode**) та власні, зі збереженням у файл.
- 📢 **Loudness normalization** — автоматичне вирівнювання гучності (RMS AGC): тихі джерела піднімаються, гучні не «вилітають».
- ↔️ **Stereo widening** — розширення стереопанорами (M/S-обробка) для відчуття «широкого» звуку.
- 🎧 **Crossfeed** — емуляція «спікерів із навушників»: зменшує втому при довгому прослуховуванні.
- ⚖️ **Balance / Mono** — регулювання балансу L/R та перемикання у моно (зручно для навушників з одним каналом).
- 📊 **Спектр-візуалізатор** — живий спектр з історією піків прямо у головному вікні.
- 🎸 **Bass Boost / Treble Boost** — 1-клік тумблери поверх еквалайзера.
- 🎚️ **Мікшер додатків** — індивідуальні правила гучності для окремих процесів, які **пам'ятаються між сесіями**.
- 🎯 **Автопрофілі пристроїв** — прив'язка «пристрій виводу → пресет»: підключили навушники — профіль увімкнувся сам.
- 🪟 **Міні-вікно** — компактний режим з основними регуляторами.
- 🔔 **Трей** — керування з системного лотка, мінімізація в трей.
- ⌨️ **Гарячі клавіші** — `Ctrl + Alt + B` бустер, `Ctrl + Alt + 1..5` пресети, `Ctrl + Alt + D` bypass DSP.
- ⏰ **Таймер сну** — вимкнення звуку через 15/30/60 хв з плавним fade-out.
- 🌗 **Авто-тема** — режим «Auto (Windows)» слідкує за системною темною/світлою темою.
- 🛡️ **Захист слуху** — попередження при підсиленні понад 150%.
- 🚀 **Автозапуск** — старт разом із Windows.
- 🎨 **4 теми** — Win11 Fluent, Arch Linux, Windows XP, StudioRack (+ світлий overlay).
- 🌐 **UA / EN локалізація** — українська та англійська мови інтерфейсу.
- 🔁 **Автоперемикання виводу** — Windows автоматично перемикається на VB-Cable при старті бустера і назад на фізичний пристрій при зупинці.
- ⬆️ **Автооновлення в один клік** — завантаження та встановлення нового exe прямо з програми (перевірка SHA256, бекап `.bak`, авто-перезапуск).
- 📝 **Файл-лог** — діагностика пишеться у `%APPDATA%\WinAudioBooster\log.txt`.

## 📋 Вимоги

- **Windows 10 / 11 (x64)**.
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — **не потрібен** для версії з GitHub Releases (single-file, self-contained).
- **VB-Audio CABLE** (віртуальний аудіо-кабель) — **додаток сам пропонує його встановити** при першому запуску; інсталятор вбудований (`Services/VbCableInstaller.cs`), архів драйвера може лежати поруч з exe або завантажуватися автоматично.

## 📦 Встановлення

### Варіант 1 — готовий білд (рекомендовано)

1. Відкрийте сторінку [Releases](https://github.com/Xydik1otto/WinAudioBooster/releases).
2. Завантажте `WinAudioBooster.exe` з останнього релізу.
3. Запустіть exe — інсталяція не потрібна, файл самодостатній (self-contained).
4. При першому запуску погодьтеся на встановлення **VB-Audio CABLE**, якщо він не знайдений у системі.
5. **Автооновлення**: додаток сам перевіряє оновлення через GitHub Releases. При наявності нової версії можна завантажити та встановити її прямо з програми — вона завантажить новий `WinAudioBooster.exe`, перевірить його SHA256, збереже старий файл як `WinAudioBooster.exe.bak`, замінить його і перезапуститься після закриття поточного процесу.

### Варіант 2 — збірка з сорців

Потрібен .NET 8 SDK:

```bash
git clone https://github.com/Xydik1otto/WinAudioBooster.git
cd WinAudioBooster
dotnet publish WinAudioBooster/WinAudioBooster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Готовий exe: `WinAudioBooster/bin/Release/net8.0-windows/win-x64/publish/WinAudioBooster.exe`.

## 🚀 Використання

1. Запустіть `WinAudioBooster.exe`.
2. Якщо VB-Cable не встановлено — погодьтеся на встановлення та перезавантажте систему за потреби.
3. Увімкніть **бустер** головним перемикачем — додаток перемкне системний вивід на VB-Cable і почне обробку.
4. Відрегулюйте **Gain** (до 300%), смуги **EQ**, компресор та лімітер — або оберіть готовий **пресет**.
5. Для тихого фонового режиму згорніть вікно в **трей**; міні-вікно доступне з головного інтерфейсу.
6. Гарячою клавішею `Ctrl + Alt + B` (за замовчуванням) можна швидко вмикати/вимикати бустер.

Зупинка бустера повертає системний вивід на попередній фізичний пристрій.

## ⚙️ Як це працює

Коротко: усі програми Windows виводять звук на віртуальний пристрій **VB-Cable** (тимчасово призначений системним виводом за замовчуванням). Додаток захоплює цей потік через **WASAPI loopback** (`WasapiLoopbackCapture`), буферизує його (`BufferedWaveProvider`), застосовує DSP-ланцюг — gain → EQ 10 смуг → boost shelves → stereo widening → crossfeed → balance/mono → компресор → loudness AGC → м'який лімітер — і виводить результат на реальні динаміки через `WasapiOut` (за потреби з ресемплінгом `MediaFoundationResampler` під формат вихідного пристрою).

Детальний опис архітектури та схеми потоків даних — у [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## 🔧 Усунення несправностей

| Проблема | Рішення |
|---|---|
| **Немає звуку після увімкнення бустера** | Перевірте, що бустер запущено (індикатор у вікні/треї). Переконайтеся, що в налаштуваннях обрано правильний фізичний пристрій виводу. Спробуйте перезапустити бустер — перемикач пристроїв іноді потребує повторної ініціалізації WASAPI. |
| **«Кабель не знайдено» / VB-Cable відсутній у списку пристроїв** | Установіть VB-Audio CABLE через запропонований додатком діалог або вручну з [vb-audio.com](https://vb-audio.com/Cable/). Після встановлення **перезавантажте Windows**. Переконайтеся, що пристрій «CABLE Input» з'явився у налаштуваннях звуку Windows. |
| **Звук є, але зависає/заїкається** | Збільште буфер: додаток використовує буфер 2 с з відкиданням при переповненні — перевірте навантаження CPU, закрийте важкі програми. |
| **Звук спотворений на максимумі гучності** | Зменшіть Gain або увімкніть лімітер/компресор — стеля лімітера за замовчуванням −0.3 дБ захищає від кліпінгу. |
| **Гарячі клавіші не працюють** | Комбінація може бути зайнята іншою програмою — змініть хоткей у налаштуваннях. |
| **Програма не запускається** | Версія з Releases самодостатня (.NET вбудований). Якщо збирали з сорців — переконайтеся, що встановлено .NET 8 Desktop Runtime. |

## 🗂️ Де зберігаються налаштування

`appsettings.json` (налаштування) та `presets.json` (власні пресети) зберігаються в **`%APPDATA%\WinAudioBooster\`**. Якщо файли ще лежать поруч із виконавчим файлом (стара версія додатка) — вони автоматично переносяться при першому запуску.

## ❓ FAQ

**Чому потрібен VB-Audio CABLE?**
Windows не дозволяє захоплювати системний звук напряму й одночасно виводити оброблений на ті самі динаміки. VB-Cable — віртуальний «передзвін»: усі програми грають у нього, а WinAudioBooster знімає цей потік через WASAPI loopback, обробляє і виводить на реальний пристрій. Додаток сам запропонує його встановити при першому запуску.

**Чому звук запізнюється або його немає після увімкнення бустера?**
Через віртуальний кабель + буферизацію додається невелика затримка (~250 мс) — для відео в браузері це майже непомітно, але в іграх може відчуватися. Якщо звуку немає зовсім — перевірте, що бустер запущено, у налаштуваннях обрано правильний фізичний пристрій виводу і «CABLE Input» з'явився у списку пристроїв Windows; допомагає перезапуск бустера.

**Як скинути налаштування?**
Видаліть папку `%APPDATA%\WinAudioBooster\` — при наступному запуску додаток створить все заново зі значеннями за замовчуванням (власні пресети теж видаляться).

**Де подивитися лог?**
Файл логу: `%APPDATA%\WinAudioBooster\log.txt`. Додайте його до Issue, якщо щось не працює — але спочатку видаліть персональні шляхи, якщо вони там є.

## 📄 Ліцензія

Проект поширюється за ліцензією [MIT](LICENSE).

---

## English (Summary)

**WinAudioBooster** is a Windows volume booster and audio enhancer built with WPF (.NET 8, NAudio + AudioSwitcher.AudioApi.CoreAudio). It boosts system volume up to **300%** via DSP and adds a 10-band equalizer, compressor and soft limiter. Audio is routed through a virtual **VB-Audio CABLE** device, captured via WASAPI loopback, processed, and played back to your physical speakers.

**Features:** volume boost up to 300%, 10-band EQ + bass/treble boost shelves, compressor, soft limiter, loudness normalization (RMS AGC), stereo widening, crossfeed for headphones, balance/mono, spectrum visualizer with peak history, presets (built-in + custom, incl. Night Mode), per-app mixer with persistent volume, device auto-profiles, sleep timer, auto theme, hearing-protection warning, mini window, system tray, global hotkeys (`Ctrl + Alt + B` booster, `Ctrl + Alt + 1..5` presets, `Ctrl + Alt + D` DSP bypass), autostart, 4 themes, Ukrainian/English localization, automatic default-device switching, one-click auto-update from GitHub Releases (SHA256-verified, `.bak` backup), file logging to `%APPDATA%\WinAudioBooster\log.txt`.

**Requirements:** Windows 10/11 x64. **VB-Audio CABLE** is required and is offered for installation right from the app.

**Install:** download the single-file `WinAudioBooster.exe` from [Releases](https://github.com/Xydik1otto/WinAudioBooster/releases), or build from source:

```bash
dotnet publish WinAudioBooster/WinAudioBooster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Troubleshooting:** no sound after enabling the booster → check the selected physical output device and restart the booster; "cable not found" → install VB-Cable (offered in-app) and reboot. DSP chain: gain → 10-band EQ → boost shelves → widening → crossfeed → balance/mono → compressor → loudness AGC → limiter. Settings live in `%APPDATA%\WinAudioBooster\` — delete that folder to reset; logs are in `log.txt` there.

Licensed under the [MIT License](LICENSE).
