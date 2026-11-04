# Ідеї з дослідження конкурентів (хвиля 2) — 2026-11-05

Джерела: Equalizer APO+Peace, FxSound, Voicemeeter, EarTrumpet, Volume², Sound Lock, EBU R128-нормалізатори, AutoEq.

## Відібрано для хвилі 2 (цінність × простота на NAudio/WPF)

1. **Bass Boost / Treble toggle (1-клік ефекти, як FxSound)** — BiQuadFilter.MakeLowShelf(~90 Гц, +6..12 дБ) / MakeHighShelf(~9 кГц) як toggle поверх EQ. Реалізація: у DspProcessor два shelf-фільтри на канал + bool IsBassBoostEnabled/IsTrebleBoostEnabled; UI — два чекбокси біля EQ.
2. **Loudness normalization / нічний режим (компресор зі стелею, Sound Lock / Windows Loudness Eq)** — у нас вже є компресор+лімітер; додати пресет/тумблер «Нічний режим»: м'який поріг, стеля гучності. Мінімальна робота — пресет + майже без DSP-змін.
3. **Авто dark/light тема за Windows (EarTrumpet-стандарт)** — Registry HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize → AppsUseLightTheme; підписка через SystemEvents.UserPreferenceChanged; мапінг на існуючі скіни (Win11Fluent ↔ світлий варіант або лайт-ресурси).
4. **Глобальні hotkeys на пресети/bypass/mute** — RegisterHotKey P/Invoke (HotkeyManager вже існує — розширити): Ctrl+Alt+1..5 → пресет, Ctrl+Alt+D → bypass (DSP on/off), фідбек через tray balloon.
5. **Sleep timer + fade-out** — DispatcherTimer N хв → плавне зниження гейну до 0 → Stop. UI: ComboBox 15/30/60 хв у системних налаштуваннях.
6. **Spectrum visualizer (FxSound wow-ефект)** — тапок ISampleProvider після DSP → буфер 2048 семплів → NAudio.Dsp.Fft → 32 біна → Canvas-бари, оновлення через CompositionTarget.Rendering. Середня складність.
7. **Sub-bass HPF 30 Гц** (звільняє headroom при бусті) — BiQuadFilter.HighPass, toggle у EQ-секції.

## Відкладено (складніше/ніше пріоритет)
- EBU R128 автогейн у реальному часі (K-weighted LUFS) — після MVVM-рефакторингу.
- Crossfeed, stereo widening, AutoEq-імпорт CSV, баланс/моно, scheduler профілів, OSD.

## Прямі запозичення з конкурентів
- FxSound: bypass-тумблер A/B (уже є IsEnabled → зробити гарячою клавішею), прив'язка пресет→пристрій (вже в хвилі 1).
- Peace: preamp-компенсація при бусті (обмежити негативний makeup — лімітер уже покриває).
- Voicemeeter: limiter у кінці ланцюга (уже є tanh-лімітер).
- EarTrumpet: live-пікові індикатори (VU вже є).
- Volume²: OSD при зміні гучності хоткеєм (tray balloon достатньо).
