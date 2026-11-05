# Контракты приложения

HTTP API нет: приложение локальное. Ниже сервисы, которые вызывает интерфейс.

## Библиотека

`IDeckService`

- `ListAsync`
- `SaveAsync(id?, name, description)` — пустое имя отвергается
- `DeleteAsync` — вместе с терминами набора

`ITermService`

- `SaveAsync(TermDraft)` — обязательны название, определение и набор; пример и пояснение можно не заполнять
- `DeleteAsync`
- `ReevaluateStatusesAsync` — после смены порога освоения

Новая категория создаётся, если в редакторе заполнено её имя.

## Повторение

`IReviewService.SubmitAsync(termId, quality, responseDurationMs)`

Возвращает `ReviewOutcome`: старые и новые EF и интервал, срок, статус, текст формулы.

`BuildQueueAsync(mode, deckId, ignoreDailyLimit)` строит очередь. `HistoryAsync(termId?)` читает журнал.

## Статистика и настройки

`IDashboardService.GetAsync` — числа главного экрана и семь дней активности.

`IStatisticsService.GetAsync(range)` — неделя, месяц или всё время. Неделя и месяц считаются по дням. Всё время длиннее 42 дней сжимается в недельные корзины.

`ISettingsService.SaveAsync` проверяет лимиты и сумму весов. `ResetStatisticsAsync` очищает прогресс, не удаляя термины.

## Обмен

`ILibraryExchange`

- CSV со столбцами `Title,Definition,Example,Explanation,Category,Deck`
- JSON формата `termrecall.library` версии 1

Повторный импорт пропускает термин, если в наборе уже есть такое название. Недостающий набор и категория создаются.

`IBackupService` отдаёт байты файла SQLite и принимает их обратно.

## Напоминание

`IReminderScheduler.ApplyAsync` на desktop ничего не планирует в системе: если время наступило и сегодня ещё не было ответов, главный экран показывает плашку. Android ставит неточный ежедневный `AlarmManager` и уведомление. iOS ставит календарное локальное уведомление, если пользователь разрешил их.
