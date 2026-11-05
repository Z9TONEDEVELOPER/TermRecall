# TermRecall

Мобильное и настольное приложение для интервального повторения профессиональной терминологии. Первая библиотека — термины C# и .NET. Срок следующей карточки считает алгоритм SM-2, данные лежат в локальном SQLite и не требуют сети.

Интерфейс на русском. Стек: C#, .NET 10, Avalonia 11.3.9, MVVM, SQLite.

## Запуск на компьютере

Нужен SDK .NET 10.

```bash
dotnet run --project src/TermRecall.Desktop/TermRecall.Desktop.csproj
```

Окно открывается в пропорциях телефона, чтобы так же выглядеть на Android и iOS. База создаётся в каталоге данных пользователя: `~/Library/Application Support/TermRecall/termrecall.db` на macOS.

## Тесты

```bash
dotnet test tests/TermRecall.Domain.Tests/TermRecall.Domain.Tests.csproj
dotnet test tests/TermRecall.Application.Tests/TermRecall.Application.Tests.csproj
dotnet test tests/TermRecall.Infrastructure.Tests/TermRecall.Infrastructure.Tests.csproj
```

## Сборка Android

Нужен workload `android` (на системном SDK — с правами администратора):

```bash
sudo dotnet workload install android
dotnet build src/TermRecall.Android/TermRecall.Android.csproj -f net10.0-android
```

Пакет — APK с идентификатором `com.termrecall.app`. Минимальная версия Android — 5.0 (API 21). Напоминание — ежедневное неточное уведомление, на Android 13 и новее приложение запрашивает разрешение на уведомления.

На машине, где собирался этот репозиторий, workload в `/usr/local/share/dotnet` без прав администратора не ставится. Проект Android в решении есть и ссылается на общий UI.

## Сборка iOS

Нужны macOS, Xcode и workload `ios`.

```bash
dotnet build src/TermRecall.iOS/TermRecall.iOS.csproj -f net10.0-ios -r iossimulator-arm64
```

Сборка симулятора на этой машине проходит так: workload `ios` установлен, проверка номера Xcode отключена, потому что пакет .NET for iOS 26.2 рассчитан на Xcode 26.2, а установлен Xcode 26.6. Отдельный launch storyboard не подключён: `ibtool` этой пары версий на нём падает. Для симулятора подпись не требуется (`EnableCodeSigning=false`).

Запуск на симуляторе после успешной сборки:

```bash
dotnet build src/TermRecall.iOS/TermRecall.iOS.csproj -t:Run -f net10.0-ios -r iossimulator-arm64
```

## Как устроено

| Проект | Роль |
|---|---|
| `src/TermRecall.Domain` | SM-2, очередь, показатели |
| `src/TermRecall.Application` | сценарии и проверки |
| `src/TermRecall.Infrastructure` | SQLite, импорт, копия |
| `src/TermRecall.App` | экраны Avalonia |
| `src/TermRecall.Desktop` | запуск на компьютере |
| `src/TermRecall.Android`, `src/TermRecall.iOS` | мобильные точки входа |

Документы для курсовой:

- `docs/Mathematical_Model.md` — модель и ограничения
- `docs/Algorithm_Examples.md` — те же числа, что в тестах и в журнале приложения
- `docs/Architecture.md`, `docs/Database.md`, `docs/Algorithms.md`, `docs/API.md`

## Учебный сценарий

1. На главной нажмите «Начать повторение».
2. Откройте определение и поставьте оценку от 0 до 5. На компьютере работают клавиши 0–5.
3. Под следующей карточкой видна подстановка формулы. Полный текст лежит в истории термина.
4. Просроченные карточки остаются в очереди, пока их не повторят.

Оценки 0–2 начинают серию заново и ставят интервал в 1 день. Оценки 3–5 дают 1 день, затем 6 дней, затем прежний интервал, умноженный на новый коэффициент лёгкости.
