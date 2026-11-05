# WinAudioApo — стартовий костяк системного APO для WinAudioBooster

> **Статус: стартовий костяк (starter skeleton).** Структурно коректний, але
> містить позначки `TODO` в місцях, які залежать від конкретної версії WDK/SDK
> або потребують дев-верифікації на реальній машині. Контекст і загальний план —
> у [`docs\apo\README-uk.md`](../../docs/apo/README-uk.md).

## Що це

User-mode COM DLL, яку аудіорушій Windows (*audiodg.exe*) завантажує як system
effects APO для кінцевого пристрою. Ланцюжок DSP: **gain (slew 60 дБ/с) →
10-смуговий peaking EQ (biquad, RBJ) → tanh-лімітер** — паритет із
`WinAudioBooster/Audio/DspProcessor.cs`. Параметри читаються з
`%APPDATA%\WinAudioBooster\apo-config.ini` (`GainDb`, `EqGainsDb` (10 значень,
через кому), `LimiterCeilingDb`), перечитування — за LastWriteTime на кожному
чанку (FileSystemWatcher-стиль, без додаткового потоку).

Ключові файли:

| Файл | Призначення |
|------|-------------|
| `WinApo.h/.cpp` | Клас `CWinApo` (IAudioProcessingObject / RT / SystemEffects2/3) + DSP |
| `ApoFact.h/.cpp` | Ручна `IClassFactory`, `DllGetClassObject`, `DllCanUnloadNow`, `DllRegisterServer/UnregisterServer`, `DllMain` |
| `WinApoReg.cpp` | Реєстрові записи COM-класу (CLSID/InprocServer32) + TODO на `RegisterAPO` |
| `ApoConfig.h/.cpp` | Парсер INI + перевірка timestamp |
| `WinAudioApo.def` | Експорти DLL |
| `WinAudioApo.vcxproj` | VS 2022 (v143), Win32/x64, WindowsTargetPlatformVersion 10.0 |
| `apo-config.ini.example` | Зразок конфігу |
| `LICENSE-NOTE.md` | Ліцензійна нота (BSD-2-Clause; Equalizer APO — GPL-2.0, код не копіювався) |

GUID-и (запечені, не генерувати заново):
- CLSID APO: `{FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}`
- Effect GUID (для `GetEffectsList`): `{52461AC9-574E-46E7-A0E8-77AB1E43827A}`

## Що потрібно для збірки

1. **Visual Studio 2022** (пакет «Desktop development with C++», toolset v143).
2. **Windows 10/11 SDK** (любий свіжий; `WindowsTargetPlatformVersion=10.0`
   означає «останній встановлений»).
3. **Windows Driver Kit (WDK)** — з нього беруться APO-хелпери
   (`BaseAudioProcessingObject.h`, бібліотеки `audiomediatypecrt.lib`,
   `audioeng.lib`, функція `RegisterAPO`). Сама DLL — чистий user-mode, але без
   WDK-шляхів лінкер не знайде APO-бібліотеки: додай у властивості проекту
   (або через `.props`) `$(WDK_ContentRoot)`-шляхи до lib, як це роблять зразки
   sysvad. У SDK ≥ 10.0.22000 заголовки `audioenginebaseapo.h` /
   `BaseAudioProcessingObject.h` є і в самому SDK.

Збірка з командного рядка (Developer Command Prompt):

```bat
msbuild native\WinAudioApo\WinAudioApo.vcxproj /p:Configuration=Release /p:Platform=x64
```

Конфігурації: Debug/Release × Win32/x64. Для 64-бітного Windows **тільки x64**
DLL реально завантажиться в audiodg (Win32 — для 32-бітної Windows 10; ARM64 —
TODO, див. vcxproj).

## Реєстрація / дереєстрація

Скелет реєструє **COM-клас** самостійно:

```bat
regsvr32 /s WinAudioApo.dll      :: реєстрація (від адміна)
regsvr32 /u WinAudioApo.dll      :: дереєстрація
```

Це пише `HKLM\SOFTWARE\Classes\CLSID\{FA1C7572-...}` (InprocServer32,
ThreadingModel=Both) та базовий ключ
`HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{FA1C7572-...}`.

**Але цього недостатньо** — перед першим запуском закрий TODO у
`WinApoReg.cpp`:

- повний набір значень `AudioProcessingObjects\{GUID}` роблять функції
  `RegisterAPO`/`UnregisterAPO` з `audioenginebaseapo.h` (DDK) — додай виклик
  з `APO_REG_PROPERTIES` для `CWinApo`; або
- використай RegApo-стиль зовнішній скрипт/тул з `packaging\apo` (як робить
  Configurator Equalizer APO, див. [їх dev-док](https://sourceforge.net/p/equalizerapo/wiki/Developer%20documentation/)).

## Кріплення до пристрою (FxProperties)

1. Умова для дев-машини: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Audio`
   → DWORD `DisableProtectedAudioDG = 1` (дозволити непідписаний APO).
2. У `HKLM\...\MMDevices\Audio\Render\{endpoint GUID}\FxProperties` заміни
   значення ефекту на CLSID нашого APO, **зберігши попередні значення в бекап**
   (Equalizer APO тримає копії під `HKLM\SOFTWARE\EqualizerAPO\Child APOs` —
   ми робитимемо так само, див. `packaging\apo\README-uk.md`).
3. Перезапусти аудіо: `net stop audiosrv && net start audiosrv` (або
   перезавантаження).
4. Відкат = відновлення бекап-значень + рестарт аудіосервісу.

## Тестування

- **AudioEng.exe** з WDK (`<WDK>\Tools\<arch>\audiodg`-супутні тули):
  `AudioEng.exe -a -apo {FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}` — офіційна
  перевірка APO-інтерфейсів (форматні виклики, Lock/Unlock, APOProcess).
  Альтернатива з новіших WDK: `apointer`/тест-харнеси з WDK "Audio APO tests".
- Ручний smoke-test: зареєструй, прикрути до пристрою, грай аудіо, редагуй
  `apo-config.ini` — зміна гейну/ЕК стосується звуку без перезапуску.
- Слідкуй за `OutputDebugString` у налагоджувачі на audiodg.exe (або
  DebugView) — TODO: додати логгування першого завантаження/версії конфігу.

## Відомі обмеження костяка

- Реалізовано лише `gain → 10-band EQ → limiter`; решта ланцюжка DspProcessor
  (shelves, widen, crossfeed, mono/balance, компресор, AGC) — TODO (див.
  коментарі у `WinApo.h`).
- Перечитування конфігу відбувається на RT-потоці (один stat на чанк —
  дешево; сам парс лише при зміні). TODO: парс у фоновому потоці + атомарна
  публікація (Equalizer APO так і робить).
- Форматна нотація спрощена (приймаємо float32 як є); перевірки форматів — TODO.
- Немає обробки silent-фреймів (`APO_CONNECTION_PROPERTY_FLAG_SILENCE`) — TODO.
- Реєстрація APO-значень (`AudioProcessingObjects`) неповна без `RegisterAPO` — TODO.
- Немає підписаного бінаря: для публічної поставки потрібне підписання ( див.
  `packaging\apo\README-uk.md`).
