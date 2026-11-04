# Contributing /Як долучитися

Дякуємо за інтерес до **WinAudioBooster**! 🎚️ Цей документ описує, як зібрати проект, які правила оформлення коду діють у репозиторії та як надіслати pull request.

*(English summary at the bottom.)*

## 🛠️ Як зібрати проект

Потрібен **.NET 8 SDK** (версія фіксується у `global.json`).

```bash
# клонування
git clone https://github.com/Xydik1otto/WinAudioBooster.git
cd WinAudioBooster

# відновлення залежностей і збірка
dotnet restore WinAudioBooster/WinAudioBooster.csproj
dotnet build WinAudioBooster/WinAudioBooster.csproj --configuration Release

# запуск тестів
dotnet test WinAudioBooster.Tests/WinAudioBooster.Tests.csproj --configuration Release
```

Single-file публікація (те саме, що робить CI для релізів):

```bash
dotnet publish WinAudioBooster/WinAudioBooster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Готовий exe: `WinAudioBooster/bin/Release/net8.0-windows/win-x64/publish/WinAudioBooster.exe`.

Для локального запуску знадобиться **VB-Audio CABLE** — додаток сам запропонує його встановити при першому старті.

## 🎨 Стиль коду

- Мова інтерфейсу та **коментарі в коді — українська** (ідентифікатори — англійською).
- Дотримуйтесь стандартних конвенцій C# (.editorconfig / форматування за замовчуванням Visual Studio).
- Логіку DSP, роботи з WASAPI та перемиканням пристроїв змінюйте обережно й описуйте зміни в PR — це найчутливіша частина коду.
- Не додавайте нові залежності без вагомої причини; якщо додаєте — поясніть чому.
- Тестову папку `WinAudioBooster.Tests\` та основний проект змінюйте лише в межах вашої задачі.

## 🔀 Процес Pull Request

1. Створіть гілку від `main`: `feat/<назва>` або `fix/<назва>`.
2. Якщо змінюєте поведінку — додайте/оновіть тести у `WinAudioBooster.Tests`.
3. Переконайтеся, що `dotnet build` і `dotnet test` проходять локально (CI перевіряє це на кожен PR).
4. Оформіть PR за шаблоном (`.github/PULL_REQUEST_TEMPLATE.md`): що змінено, навіщо, як перевірено.
5. Не чіпайте файли релізів (`CHANGELOG.md` оновлюємо при релізі) — це робить мейнтейнер.
6. Очікуйте на рев'ю; дрібні правки прошу вносити окремими комітами, історію перед мержем можна не сквошити.

## 🐣 Ідеї для новачків (`good first issue`)

Шукаєте, з чого почати? Ось типові задачі рівня «перший PR»:

- 🎨 **Темі** — додати нову тему оформлення (див. існуючі Win11 Fluent / Arch / XP / StudioRack як зразок).
- 🌐 **Локалізація** — додати нову мову інтерфейсу або покращити існуючі переклади UA/EN.
- 🎚️ **Пресети** — додати вбудований пресет EQ/компресора або покращити збереження/імпорт власних пресетів.
- 📝 **Документація** — уточнити README, троблшутинг, переклади.
- 🐛 **Дрібні баги** — позначені в Issues міткою `good first issue`.

Перед початком роботи залиште коментар у відповідному Issue, щоб не дублювати роботу.

## 💬 Питання

Питання щодо використання — у Issues з міткою `question`; вразливості — дивіться [SECURITY.md](SECURITY.md) (не публікуйте їх у відкритих Issues).

---

## English (Summary)

**Building:** requires .NET 8 SDK — `dotnet restore` / `dotnet build` / `dotnet test WinAudioBooster.Tests/WinAudioBooster.Tests.csproj`. Single-file publish command is above.

**Code style:** standard C# conventions; UI strings and **code comments are in Ukrainian** (identifiers in English). Be careful with the DSP/WASAPI/device-switching logic and describe changes in the PR.

**PR process:** branch from `main` (`feat/...` or `fix/...`), keep builds green, fill in the PR template, wait for review. Avoid unrelated changes and new dependencies unless justified.

**Good first issues:** new themes, localization (UA/EN and beyond), EQ/compressor presets, docs. Comment on the issue before starting.
