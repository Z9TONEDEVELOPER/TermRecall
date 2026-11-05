using TermRecall.Domain.Entities;
using TermRecall.Infrastructure.Database;

namespace TermRecall.Infrastructure.Seeding;

internal static class TerminologySeeder
{
    public static async Task SeedIfEmptyAsync(SqliteDatabase database, CancellationToken cancellationToken)
    {
        var hasDecks = await database.QueryAsync(async (connection, transaction, token) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM decks;";
            var value = await command.ExecuteScalarAsync(token);
            return Convert.ToInt32(value) > 0;
        }, cancellationToken);
        if (hasDecks)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var categories = new (string Name, string Color)[]
        {
            ("Язык", "#6558E8"),
            ("Типы", "#8B7CF6"),
            ("ООП", "#22A06B"),
            ("Память", "#E5A13B"),
            ("Коллекции", "#3D8BFD"),
            ("Асинхронность", "#E45D67"),
            ("Платформа", "#5C6BC0")
        };
        var categoryIds = categories.ToDictionary(item => item.Name, _ => Guid.NewGuid());
        var decks = new (string Name, string Description)[]
        {
            ("Основы C#", "Синтаксис, типы и базовые конструкции языка."),
            ("ООП и типы", "Объекты, наследование, интерфейсы и современные типы C#."),
            ("Коллекции и LINQ", "Хранение наборов данных и запросы к ним."),
            ("Асинхронность и .NET", "Task, async/await и устройство платформы.")
        };
        var deckIds = decks.ToDictionary(item => item.Name, _ => Guid.NewGuid());

        await database.QueryAsync(async (connection, transaction, token) =>
        {
            foreach (var category in categories)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO categories(id, name, color) VALUES ($id, $name, $color);";
                command.Parameters.AddWithValue("$id", Sqlio.GuidText(categoryIds[category.Name]));
                command.Parameters.AddWithValue("$name", category.Name);
                command.Parameters.AddWithValue("$color", category.Color);
                await command.ExecuteNonQueryAsync(token);
            }

            foreach (var deck in decks)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO decks(id, name, description, created_at, updated_at) VALUES ($id, $name, $description, $at, $at);";
                command.Parameters.AddWithValue("$id", Sqlio.GuidText(deckIds[deck.Name]));
                command.Parameters.AddWithValue("$name", deck.Name);
                command.Parameters.AddWithValue("$description", deck.Description);
                command.Parameters.AddWithValue("$at", Sqlio.Time(now));
                await command.ExecuteNonQueryAsync(token);
            }

            foreach (var term in Terms(deckIds, categoryIds, now))
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO terms(
                        id, title, definition, example, explanation, category_id, deck_id, created_at,
                        interval_days, ease_factor, repetition_count, status, lapse_count)
                    VALUES ($id, $title, $definition, $example, $explanation, $category, $deck, $created, 0, 2.5, 0, 0, 0);
                    """;
                command.Parameters.AddWithValue("$id", Sqlio.GuidText(term.Id));
                command.Parameters.AddWithValue("$title", term.Title);
                command.Parameters.AddWithValue("$definition", term.Definition);
                command.Parameters.AddWithValue("$example", term.Example);
                command.Parameters.AddWithValue("$explanation", term.Explanation);
                command.Parameters.AddWithValue("$category", Sqlio.Db(term.CategoryId));
                command.Parameters.AddWithValue("$deck", Sqlio.GuidText(term.DeckId));
                command.Parameters.AddWithValue("$created", Sqlio.Time(term.CreatedAtUtc));
                await command.ExecuteNonQueryAsync(token);
            }

            await using var settings = connection.CreateCommand();
            settings.Transaction = transaction;
            settings.CommandText = """
                INSERT INTO user_settings(
                    id, daily_new_terms_limit, daily_review_limit, reminder_enabled, reminder_time, theme,
                    include_new_terms, mastery_target_days, weight_success, weight_regularity, weight_mastery,
                    display_name, difficult_ease_threshold)
                VALUES (1, 10, 40, 1, '19:00', 0, 1, 21, 0.5, 0.2, 0.3, 'Максим', 1.6);
                """;
            await settings.ExecuteNonQueryAsync(token);
            return 0;
        }, cancellationToken);
    }

    private static IEnumerable<Term> Terms(Dictionary<string, Guid> decks, Dictionary<string, Guid> categories, DateTime now)
    {
        Term T(string deck, string category, string title, string definition, string example, string explanation) => new()
        {
            Id = Guid.NewGuid(),
            DeckId = decks[deck],
            CategoryId = categories[category],
            Title = title,
            Definition = definition,
            Example = example,
            Explanation = explanation,
            CreatedAtUtc = now
        };

        return
        [
            T("Основы C#", "Платформа", "CLR",
                "Common Language Runtime — исполняющая среда .NET, которая загружает сборки, управляет памятью и выполняет IL-код.",
                "Исключение возникает уже во время работы CLR, а не компилятора.",
                "Компилятор C# создаёт IL. CLR превращает его в машинный код и следит за типами."),
            T("Основы C#", "Платформа", "Сборка",
                "Единица развёртывания .NET: исполняемый файл или библиотека с манифестом, метаданными и IL-кодом.",
                "Проект создаёт сборку TermRecall.Domain.dll.",
                "Сборка может содержать несколько модулей, но обычно это один файл."),
            T("Основы C#", "Язык", "Пространство имён",
                "Именованная область, которая группирует типы и помогает избегать конфликтов имён.",
                "namespace TermRecall.Domain.Algorithms;",
                "Пространство имён не создаёт отдельную сборку и не ограничивает доступ само по себе."),
            T("Основы C#", "Типы", "Значимый тип",
                "Тип, значение которого хранится непосредственно в переменной. К ним относятся структуры, перечисления и простые числовые типы.",
                "int count = 3; копирует само число, а не ссылку.",
                "При передаче в метод копируется значение, если параметр не помечен ref или in."),
            T("Основы C#", "Типы", "Ссылочный тип",
                "Тип, переменная которого хранит ссылку на объект в управляемой куче. Классы, интерфейсы, делегаты и массивы — ссылочные типы.",
                "Две переменные одного класса могут указывать на один объект.",
                "Присваивание копирует ссылку. Сам объект остаётся общим."),
            T("Основы C#", "Язык", "Nullable-тип",
                "Тип, который помимо обычных значений может содержать null. Для ссылок это ссылочные nullable-аннотации, для структур — Nullable<T>.",
                "int? score = null;",
                "Оператор ?. и ?? помогают не разыменовывать пустое значение."),
            T("Основы C#", "Язык", "Интерполяция строк",
                "Способ собрать строку из литерала и выражений, записанных внутри фигурных скобок.",
                "var text = $\"Интервал: {days} дн.\";",
                "Интерполированная строка компилируется в вызов string.Create или string.Format в зависимости от версии."),
            T("Основы C#", "Язык", "var",
                "Ключевое слово, которое просит компилятор вывести тип локальной переменной из инициализатора.",
                "var terms = new List<Term>();",
                "var не делает переменную динамической: тип фиксируется при компиляции."),
            T("ООП и типы", "ООП", "Инкапсуляция",
                "Сокрытие внутреннего состояния объекта и предоставление доступа к нему через явно заданный контракт.",
                "Поле _easeFactor закрыто, а наружу доступен только результат повторения.",
                "Инкапсуляция уменьшает число мест, которые нужно менять при смене представления данных."),
            T("ООП и типы", "ООП", "Наследование",
                "Механизм, при котором тип получает члены базового типа и может расширять или переопределять их поведение.",
                "class SqliteDatabase : IDatabaseInitializer",
                "В C# у класса может быть только один базовый класс и сколько угодно интерфейсов."),
            T("ООП и типы", "ООП", "Полиморфизм",
                "Возможность работать с объектами разных типов через общий контракт, не зная конкретный тип во время компиляции.",
                "IReminderScheduler scheduler вызывает Android- или пустую реализацию.",
                "В C# полиморфизм методов класса включается через virtual и override."),
            T("ООП и типы", "ООП", "Интерфейс",
                "Контракт членов без собственной реализации состояния. Тип может реализовать несколько интерфейсов.",
                "interface ITermRepository { Task<Term?> GetAsync(...); }",
                "Начиная с C# 8 интерфейс может содержать реализацию по умолчанию, но состояние по-прежнему хранит класс."),
            T("ООП и типы", "ООП", "Абстрактный класс",
                "Класс, от которого нельзя создать экземпляр. Он задаёт общую базу и может содержать реализованные и абстрактные члены.",
                "abstract class ViewModelBase : ObservableObject",
                "Абстрактный член обязан быть реализован в конкретном наследнике."),
            T("ООП и типы", "ООП", "Виртуальный метод",
                "Метод, который наследник может заменить собственной реализацией. Вызов выбирается по фактическому типу объекта.",
                "public virtual AppBuilder CustomizeAppBuilder(AppBuilder builder)",
                "Без virtual метод скрывается через new, а не переопределяется."),
            T("ООП и типы", "ООП", "Делегат",
                "Типобезопасная ссылка на метод с заданной сигнатурой.",
                "Func<Task<int>> work = () => repository.CountAsync();",
                "Событие в C# хранит список делегатов-подписчиков."),
            T("ООП и типы", "ООП", "record",
                "Ссылочный или значимый тип, для которого компилятор создаёт сравнение по значению, метод ToString и поддержку неразрушающего копирования.",
                "public readonly record struct Sm2State(double EaseFactor, int IntervalDays, int RepetitionCount);",
                "Обычный класс сравнивается по ссылке, если равенство не переопределено."),
            T("ООП и типы", "Память", "Сборщик мусора",
                "Компонент CLR, который находит недостижимые объекты в управляемой куче и освобождает занятую ими память.",
                "Временный список внутри метода станет мусором после выхода из метода, если ссылка не сохранилась.",
                "Сборщик не заменяет Dispose для неуправляемых ресурсов: файлы и соединения закрывают явно."),
            T("ООП и типы", "Память", "using",
                "Конструкция, которая гарантирует вызов Dispose даже при исключении.",
                "await using var command = connection.CreateCommand();",
                "Для типа нужен IDisposable или IAsyncDisposable."),
            T("Коллекции и LINQ", "Коллекции", "IEnumerable<T>",
                "Контракт последовательности, которую можно перечислить по одному элементу.",
                "foreach (var term in terms) { }",
                "IEnumerable не обещает, что данные уже материализованы. Запрос LINQ может выполниться при перечислении."),
            T("Коллекции и LINQ", "Коллекции", "List<T>",
                "Динамический массив: последовательность с индексом, которая сама увеличивает внутренний буфер.",
                "var queue = new List<Term>();",
                "Добавление в заполненный буфер стоит дороже обычного, потому что массив копируется в новый."),
            T("Коллекции и LINQ", "Коллекции", "Dictionary<TKey, TValue>",
                "Коллекция пар ключ-значение с поиском по ключу в среднем за постоянное время.",
                "categories.ToDictionary(item => item.Name, item => item.Id);",
                "Ключ должен иметь устойчивые Equals и GetHashCode."),
            T("Коллекции и LINQ", "Коллекции", "LINQ",
                "Language Integrated Query — набор методов и синтаксиса для запросов к последовательностям и другим источникам.",
                "terms.Where(term => term.DeckId == deckId).OrderBy(term => term.Title);",
                "Большинство операторов отложены: они строят запрос, а не сразу проходят коллекцию."),
            T("Коллекции и LINQ", "Коллекции", "Отложенное выполнение",
                "Запрос LINQ не обращается к источнику, пока результат кто-нибудь не перечислит.",
                "var query = items.Where(x => x > 0); // источник ещё не прочитан",
                "Повторное перечисление выполнит запрос снова. ToList материализует результат."),
            T("Коллекции и LINQ", "Язык", "yield",
                "Оператор, который превращает метод в итератор и возвращает элементы по мере перечисления.",
                "IEnumerable<int> Positive(IEnumerable<int> source) { foreach (var x in source) if (x > 0) yield return x; }",
                "Код до первого yield выполнится только при старте перечисления."),
            T("Асинхронность и .NET", "Асинхронность", "async/await",
                "Пара ключевых слов: async разрешает await в методе, await освобождает поток на время ожидания задачи и продолжает метод после её завершения.",
                "var term = await terms.GetAsync(id, cancellationToken);",
                "async не запускает новый поток сам по себе. Он организует продолжение вокруг Task."),
            T("Асинхронность и .NET", "Асинхронность", "Task",
                "Объект, который представляет асинхронную операцию и её будущий результат или ошибку.",
                "Task<Term?> GetAsync(Guid id, CancellationToken cancellationToken);",
                "Исключение из async-метода помещается в Task и возникает снова в точке await."),
            T("Асинхронность и .NET", "Асинхронность", "CancellationToken",
                "Маркер кооперативной отмены. Долгая операция периодически проверяет его и прекращает работу по запросу.",
                "await command.ExecuteReaderAsync(cancellationToken);",
                "Отмена не прерывает поток насильно. Метод должен сам учитывать токен."),
            T("Асинхронность и .NET", "Платформа", "Dependency Injection",
                "Подход, при котором объект получает свои зависимости извне, а не создаёт их сам.",
                "public sealed class ReviewService(ITermRepository terms, IClock clock)",
                "В TermRecall граф объектов собирает Microsoft.Extensions.DependencyInjection при запуске."),
            T("Асинхронность и .NET", "Платформа", "SQLite",
                "Встраиваемая реляционная база данных, которая хранит всё приложение в одном файле и не требует сервера.",
                "Файл termrecall.db создаётся в локальном каталоге приложения.",
                "Для TermRecall это позволяет работать без интернета и не терять историю повторений между запусками.")
        ];
    }
}
