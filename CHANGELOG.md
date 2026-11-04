# Changelog

Усі значущі зміни цього проекта документуються тут.

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.1.0/),
версіонування — [Semantic Versioning](https://semver.org/lang/uk/).

## [1.1.0] - Unreleased

### Added
- ⚡ **Зниження латентності**: аудіо-буфер 2 с → 250 мс, WasapiOut 50 мс → 30 мс.
- 🌊 **Плавний гейн** (slew limiting) — рух слайдера гучності більше не клацає.
- 🎧 **Автопрофілі пристроїв** — прив'язка «пристрій виводу → пресет»: підключив навушники — увімкнувся профіль навушників.
- 🎚️ **Персистентна гучність per-app** — мікшер пам'ятає налаштовані рівні додатків між сесіями.
- 🛡️ **Захист слуху** — попередження при підсиленні >150% (один раз на сесію, відключається в налаштуваннях).
- 🎸 **Bass Boost / Treble Boost / Cut sub-bass (30 Гц)** — 1-клік тумблери поверх EQ (ідея з FxSound).
- 🌙 **Пресет Night Mode** — м'який компресор зі стелею −6 дБ (ідея з Sound Lock / Windows Loudness Equalization).
- 🌗 **Авто-тема за Windows** — режим «Auto (Windows)» слідкує за системною темою (EarTrumpet-патерн) + світлий overlay.
- ⌨️ **Нові гарячі клавіші**: Ctrl+Alt+1..5 → пресети 1–5, Ctrl+Alt+D → bypass DSP (ідея з Peace/FxSound).
- ⏰ **Таймер сну** — вимкнення через 15/30/60 хв з плавним fade-out 10 с (ідея з Volume²).
- 🏗️ **Open-source-інфраструктура**: CONTRIBUTING.md, SECURITY.md, шаблони Issues/PR, CHANGELOG, бейджі README, маніфести winget та Scoop, збірка Inno Setup інсталятора в CI.

## [1.0.1] - 2026-11-05

### Fixed
- Виправлено збірку тестового проекта та стабільність CI-воркфлоу (`build.yml`).
- Уточнено поведінку **лімітера** (коректна стеля −0.3 дБ без залишкового кліпінгу).

### Added
- ✅ Покриття **юніт-тестами** (`WinAudioBooster.Tests`).

## [1.0.0] - 2026-11-05

### Added
- 🔊 Бустер гучності до 300% через програмний DSP-gain.
- 🎛️ 10-смуговий еквалайзер із пресетами (Light Boost, Max Boost, Headphones, Games, Calls) та власними пресетами.
- 🗜️ Компресор (поріг, співвідношення, attack/release) і 🛑 м'який лімітер.
- 🎚️ Мікшер гучності окремих додатків (per-app).
- 🪟 Міні-вікно, 🔒 керування з трею, ⌨️ глобальні гарячі клавіші (`Ctrl + Alt + B`), 🚀 автозапуск.
- 🎨 4 теми (Win11 Fluent, Arch Linux, Windows XP, StudioRack) та 🌐 UA/EN локалізація.
- 🔁 Автоперемикання системного виводу на VB-Cable і назад.
- ⬆️ Перевірка оновлень через GitHub Releases; single-file self-contained збірка в CI.

[Unreleased]: https://github.com/Xydik1otto/WinAudioBooster/compare/v1.0.1...HEAD
[1.1.0]: https://github.com/Xydik1otto/WinAudioBooster/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Xydik1otto/WinAudioBooster/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Xydik1otto/WinAudioBooster/releases/tag/v1.0.0
