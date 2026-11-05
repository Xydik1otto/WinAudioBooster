# APO-інтеграція WinAudioBooster: архітектурний план

> Статус: **плановий напрямок / стартовий пакет**. Нічого з наявного коду, CI чи
> дистрибуції WinAudioBooster не змінено — усе нижче є адитивною підготовкою.
> Скелет APO лежить у `native\WinAudioApo\`, план дистрибуції — у `packaging\apo\README-uk.md`.

## 1. Що таке APO в Windows аудіо-стеку

**APO (Audio Processing Object)** — користувацький (user-mode) COM-компонент, який
аудіорушій Windows (*audiodg.exe*) вставляє безпосередньо в граф обробки сигналу
кінцевого аудіопристрою. Впроваджений у стек разом із Windows Vista у складі
«system effects infrastructure».

Типи APO за позицією в графі:

| Тип | Де стоїть | Стосунок до нас |
|-----|-----------|-----------------|
| **SFX** (stream effect) | до мікшера, окремо на кожен потік | потенційно (per-app processing) |
| **MFX** (mode effect) | після мікшера режиму (raw/default) | можливий другий етап |
| **LFX** (local effect) / **GFX** (global effect) | класична Vista-пара: LFX перед мікшером, GFX після | основний шлях (як в Equalizer APO) |

Обов'язкові COM-інтерфейси (див. [Implementing Audio Processing Objects][msdn-apo]):
`IAudioProcessingObject` (ініціалізація, узгодження формату),
`IAudioProcessingObjectConfiguration` (`LockForProcess`/`UnlockForProcess`),
`IAudioProcessingObjectRT` (`APOProcess` — реальний DSP на RT-потоці аудіорушія),
плюс `IAudioSystemEffects(2|3)`, без яких аудіорушій не розпізнає DLL як system
effects APO. У Windows 11 (build 22000+) є новіший набір API
([Windows 11 APIs for APOs][msdn-apo-win11]) — ми його свідомо **не** використовуємо
на старті, бо він вимагає поставки APO разом із драйвером.

## 2. Чому це прибирає VB-Cable і латентність

Зараз WinAudioBooster працює так:

```
app → [VB-Cable віртуальний пристрій] → (ядро) → наш WPF-процес:
      NAudio WASAPI loopback → DspProcessor (gain→EQ→shelves→widen→
      crossfeed→balance/mono→компресор→AGC→лімітер) → WPF-пристрій → гучномовці
```

Це дві повноцінні передачі потоку через ядро + WASAPI-цикли в нашому процесі.
Кожен хоп додає латентність (розмір періоду буфера, scheduling jitter), а
VB-Cable — ще й фіксований буфер власного драйвера. Підсумково ми платимо
десятки мілісекунд і додатковим CPU на копіювання семплів туди-сюди.

З APO:

```
app → audiodg.exe: мікшер → [WinAudioApo: той самий DSP-ланцюжок у C++] → драйвер → пристрій
```

- **Нуль додаткових віртуальних пристроїв**: APO живе всередині штатного графу
  аудіорушія для конкретного endpoint'а.
- **Мінімальна латентність**: `GetLatency()` може чесно повертати 0 —
  обробка потокова, без перебуферизації (це ж рівень, на якому живе Equalizer APO).
- **Системний рівень**: обробляються ВСІ програми одразу — те, заради чого
  зараз і розгорнуто VB-Cable.
- **VB-Cable повністю знімається з ланцюга** ( uninstall), WPF-застосунок
  залишається лише конфігуратором і перестає бути критичним для звуку: якщо
  WPF не запущено, звук іде з останнім застосованим конфігом або повз нього.

## 3. Як працює ін'єкція APO (реєстрова модель)

Зверніть увагу на уточнення фактів: універсального інструменту «RegApo» в
Equalizer APO **немає** — його Configurator виконує реєстрові записи сам (див.
`DeviceAPOInfo.cpp`/`Configurator` у [їхньому джерелі][eqapo-src]); у WDK для
того самого є функції `RegisterAPO`/`UnregisterAPO`, оголошені в
`audioenginebaseapo.h`. Терміни «RegApo-стиль реєстрації» нижче означають саме
цей підхід. Наш `DllRegisterServer` робить частину записів сам (скелет —
`native\WinAudioApo\WinApoReg.cpp`).

Щоб аудіорушій завантажив наш APO, потрібно (рецепт — [Developer documentation
Equalizer APO][eqapo-devdoc] + [MSDN][msdn-apo]):

1. **COM-реєстрація класу** — `HKLM\SOFTWARE\Classes\CLSID\{GUID}`
   (видно як `HKCR\CLSID\...`): дефолтне значення = ім'я класу, підключ
   `InprocServer32` = шлях до DLL + `ThreadingModel` = `Both`.
2. **Реєстрація як Audio Processing Object** — ключ
   `HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{GUID}`
   (дружнє ім'я, копірайт, CreatedOn, версія, flags). Це «нормально робить
   функція RegisterAPO з DDK» — у нашому скелеті позначено TODO.
3. **Кріплення до пристрою** — у
   `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{endpoint GUID}\FxProperties`
   значення `{…}\{…},0` типу REG_SZ містять CLSID ефекту (класичні значення
   `Lfx`/`Gfx` + `APO effect fallthrough`-пари). **Існуючі значення треба
   зберегти в бекап** (Equalizer APO зберігає під `HKLM\SOFTWARE\EqualizerAPO\Child APOs`)
   і чіпляти наш APO ланцюжком поверх — інакше губляться штатні ефекти драйвера.
4. **Перезапуск аудіосервісу** (`net stop audiosrv / net start audiosrv` або
   перезапуск `audiodg` — реально простіше перезавантаження) — аудіорушій
   перечитує граф лише тоді.

## 4. Обмеження (читати до спроби встановити)

- **Тільки Shared mode.** APO працює в shared-режимі аудіорушія. У exclusive
  mode (ASIO-подібний, WASAPI exclusive) наші ефекти не застосовуються взагалі —
  так само як і Equalizer APO. Для бустера це прийнятний компроміс.
- **Підписання.** APO завантажується в захищений процес *audiodg.exe*, тож
  Windows перевіряє підпис. Для розробки/тестування ставимо
  `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio` → DWORD
  `DisableProtectedAudioDG = 1` (самепідписаний/непідписаний APO тоді
  завантажується). Ризик: додатки з secure audio path (деякі DRM-плеєри,
  захищені відеоконтенти) можуть відмовитись від виводу звуку. Для
  публічної дистрибуції потрібен підписаний DLL (EV/OV-сертифікат + HLK-підхід
  для повноцінної драйверної поставки — див. [Windows 11 APO APIs][msdn-apo-win11]).
- **Архітектури.** APO завантажується в *audiodg.exe* тієї ж бітності, що й
  система: на x64 Windows — **тільки x64** DLL (x86 знадобиться лише якщо
  колись захочемо APO для 32-бітного Windows 10; на Windows 11 x86 немає),
  на Windows on ARM — ARM64. `WinAudioApo.vcxproj` зараз містить Win32|x64;
  ARM64 — TODO (додати платформу в vcxproj, збірка на ARM64-агенті).
- **Кожне оновлення конфігу ≠ перезавантаження APO**: APO читає конфіг з файлу
  (наш підхід), тож WPF-конфігуратор пише файл — і звук змінюється без
  перереєстрації. Але зміна **реєстрових** налаштувань (кріплення до іншого
  пристрою) вимагає рестарту аудіосервісу.
- **Тестування складніше за звичайний exe**: крах APO = крах аудіо всіх
  застосунків. Перші версії тримати «простими і глупими», тестувати на
  віртуалці/другій машині.

## 5. Міграційний план WinAudioBooster

Цільова архітектура:

```
┌────────────────────────┐        конфіг (INI)         ┌──────────────────────────┐
│ WinAudioBooster (WPF)  │ ── %APPDATA%\WinAudioBooster │ audiodg.exe              │
│  - конфігуратор UI     │    \apo-config.ini           │  мікшер → WinAudioApo:   │
│  - пресети, EQ-криві   │ ◄── (TODO: Named pipe для    │  gain→10-band EQ→        │
│  - діагностика APO     │     миттєвого push, як в     │  limiter (RT-потік)      │
│  - інсталятор APO      │     Equalizer APO)           │                          │
└────────────────────────┘                              └──────────────────────────┘
```

Розподіл обов'язків: **WinAudioApo (C++)** виконує DSP на RT-потоці аудіорушія;
**WPF** — конфігуратор (той самий UI: слайдери gain/EQ/лімітера, пресети),
який більше не проганяє крізь себе аудіо.

### Фаза 0 — скелет (готово у цьому пакеті)
- `native\WinAudioApo\`: DLL з класом APO, фабрикою, реєстрацією CLSID,
  ланцюжком gain→10-band biquad EQ→tanh-лімітер, парсером
  `%APPDATA%\WinAudioBooster\apo-config.ini` з перевіркою timestamp на кожному
  чанку. Імена параметрів ідеологічно ті самі, що в `Models/AppSettings`:
  `GainDb`, `EqGainsDb[10]` (смуги 31/62/125/250/500/1k/2k/4k/8k/16k, Q=1.2,
  ±12 дБ), `LimiterCeilingDb`.
- Документація (цей файл + README в native та packaging).

### Фаза 1 — довести до звуку (потрібні VS 2022 + WDK)
- Заповнити всі `TODO:` у native-коді: виклик `RegisterAPO` з DDK, форматні
  перевірки, обробка silent-фреймів, ARM64.
- Зібрати, зареєструвати (`regsvr32` + кріплення через FxProperties або
  RegApo-стиль скрипт з `packaging\apo`), перевірити на дев-машині з
  `DisableProtectedAudioDG=1`.
- Паритетність звуку: звірити behavior з C# `DspProcessor` (slew 60 дБ/с,
  knee-лімітер 0.9·ceiling, формули RBJ peaking EQ збігаються з NAudio).

### Фаза 2 — синхронізація конфігуратора
- WPF пише `apo-config.ini` при кожній зміні (атомарний write+rename).
- APO перечитує за timestamp (вже в скелеті). Далі — Named pipe або
  вікно-повідомлення для миттєвого push без stat-на кожен чанк.
- Спільний експорт/імпорт пресетів (приблизно `presets.json` → INI-секції).

### Фаза 3 — дистрибуція та вихід на «вимкнути VB-Cable»
- Інсталятор з `packaging\apo` (копіювання DLL + реєстрація під адміном +
  бекап/відкат, див. `packaging\apo\README-uk.md`).
- UI: майстер «увімкнути системний режим» (перевіряє архітектуру, ставить
  `DisableProtectedAudioDG` для дев-збірок, чіпляє APO, перезапускає аудіо).
- Після стабільності — прибрати VB-Cable-шлях із дефолтного flow, залишити
  як legacy-режим.

## 6. Посилання

- [Implementing Audio Processing Objects — Microsoft Learn][msdn-apo]
- [Audio Processing Object Architecture — Microsoft Learn][msdn-arch]
- [Windows 11 APIs for Audio Processing Objects][msdn-apo-win11]
- SYSVAD APO зразки: <https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad/APO>
  (MIT; у нас адаптовано *патерн*, не вихідний текст)
- [Equalizer APO — Developer documentation (SourceForge wiki)][eqapo-devdoc] —
  найдокладніший опис реєстрової ін'єкції стороннього APO
- [Equalizer APO джерело (дзеркало, GPL-2.0)][eqapo-src]
- Microsoft Audio (приклади APO від команди Windows Audio):
  <https://github.com/microsoft/audio>

[msdn-apo]: https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects
[msdn-arch]: https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/audio-processing-object-architecture
[msdn-apo-win11]: https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/windows-11-apis-for-audio-processing-objects
[eqapo-devdoc]: https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/
[eqapo-src]: https://github.com/mirror/equalizerapo
