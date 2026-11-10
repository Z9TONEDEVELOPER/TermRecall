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

## Демонстрационная база

В Debug, если режим ещё не выбран, приложение открывает отдельный файл `termrecall-demo.db`: 125 терминов и история, посчитанная тем же SM-2. Пользовательский файл `termrecall.db` при этом не меняется. В Release без явного выбора открывается пустая пользовательская база.

Переключение, пересоздание и удаление демобазы — в настройках, раздел «Демонстрационные данные». Удаление стирает только демофайл. Медиа демобазы лежат в соседней папке `termrecall-demo-media`.

## Сборка Android

Нужен workload `android` (на системном SDK — с правами администратора):

```bash
sudo dotnet workload install android
dotnet build src/TermRecall.Android/TermRecall.Android.csproj -f net10.0-android
```

Пакет — APK с идентификатором `com.termrecall.app`. Минимальная версия Android — 5.0 (API 21). Напоминание — ежедневное неточное уведомление, на Android 13 и новее приложение запрашивает разрешение на уведомления.

На машине, где собирался этот репозиторий, workload в `/usr/local/share/dotnet` без прав администратора не ставится. Проект Android в решении есть и ссылается на общий UI.

## Сборка Android

Нужен workload `android`. На этой машине он установлен, APK собирается:

```bash
dotnet build src/TermRecall.Android/TermRecall.Android.csproj -f net10.0-android
```

Пакет — `src/TermRecall.Android/bin/Debug/net10.0-android/com.termrecall.app.apk`. Если `JAVA_HOME` указывает на несуществующий каталог Homebrew, задайте живой JDK 17 перед сборкой.

## Сборка iOS

Для Xcode 27.2 нужен workload `ios` **27.2.10000**. Он ставится набором `10.0.404` поверх SDK 10.0.4xx:

```bash
sudo /usr/local/share/dotnet/dotnet workload install ios --version 10.0.404
dotnet workload list   # ios 27.2.10000
```

Минимальная iOS для этого workload — 15.0. Сборка для симулятора:

```bash
dotnet build src/TermRecall.iOS/TermRecall.iOS.csproj -f net10.0-ios -r iossimulator-arm64
```

Debug-сборка после компиляции подписывается сертификатом Apple Development: симулятор Xcode 27 закрывает ad-hoc подпись. Среды симулятора 26.0 и 27.0 подходят, отдельный runtime 27.2 не обязателен.

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
