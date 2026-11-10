using TermRecall.Domain.Entities;

namespace TermRecall.Application.Abstractions;

public interface IDeckRepository
{
    Task<IReadOnlyList<Deck>> ListAsync(CancellationToken cancellationToken);
    Task<Deck?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Deck deck, CancellationToken cancellationToken);
    Task UpdateAsync(Deck deck, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
    Task<Category?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> FindByNameAsync(string name, CancellationToken cancellationToken);
    Task AddAsync(Category category, CancellationToken cancellationToken);
}

public interface ITermRepository
{
    Task<IReadOnlyList<Term>> ListAsync(CancellationToken cancellationToken);
    Task<Term?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Term term, CancellationToken cancellationToken);
    Task UpdateAsync(Term term, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task ResetSchedulingAsync(CancellationToken cancellationToken);
}

public interface IReviewHistoryRepository
{
    Task AddAsync(ReviewRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewRecord>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewRecord>> ListByTermAsync(Guid termId, CancellationToken cancellationToken);
    Task DeleteAllAsync(CancellationToken cancellationToken);
}

public interface IWrittenAnswerRepository
{
    Task AddAsync(WrittenAnswer answer, CancellationToken cancellationToken);
    Task<IReadOnlyList<WrittenAnswer>> ListAsync(Guid? termId, CancellationToken cancellationToken);
    Task DeleteAllAsync(CancellationToken cancellationToken);
}

public interface IAssessmentRepository
{
    Task AddAsync(AssessmentAnswer answer, CancellationToken cancellationToken);
    Task DeleteAllAsync(CancellationToken cancellationToken);
}

public interface IFavoriteRepository
{
    Task<IReadOnlySet<Guid>> ListIdsAsync(CancellationToken cancellationToken);
    Task<bool> ContainsAsync(Guid termId, CancellationToken cancellationToken);
    Task AddAsync(Guid termId, DateTime createdAtUtc, CancellationToken cancellationToken);
    Task RemoveAsync(Guid termId, CancellationToken cancellationToken);
}

public interface IAchievementRepository
{
    Task<IReadOnlyDictionary<string, DateTime>> ListAsync(CancellationToken cancellationToken);
    Task AddAsync(string code, DateTime awardedAtUtc, CancellationToken cancellationToken);
    Task DeleteAllAsync(CancellationToken cancellationToken);
}

public interface ITermMediaStore
{
    Task<TermMedia> AddAsync(Guid termId, string fileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
    Task<IReadOnlyList<TermMedia>> ListAsync(Guid termId, CancellationToken cancellationToken);
    Task<byte[]?> ReadAsync(Guid mediaId, CancellationToken cancellationToken);
    Task DeleteForTermAsync(Guid termId, CancellationToken cancellationToken);
}

public interface ISettingsRepository
{
    Task<UserSettings> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(UserSettings settings, CancellationToken cancellationToken);
}

public interface ITransactionRunner
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
    string DatabasePath { get; }
}

public interface IReminderScheduler
{
    Task ApplyAsync(UserSettings settings, CancellationToken cancellationToken);
}
