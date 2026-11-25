using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Store.Application.Auth;
using Store.Application.Common.Errors;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Security;
using Store.Infrastructure.Tests.Fixtures;

namespace Store.Infrastructure.Tests.Auth;

/// <summary>
/// Research R5: a registration that loses the race for an email (the other one is saved between
/// the existence check and the save) returns 409, never a database error.
/// </summary>
public class RegisterRaceTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(Seeders.SeedTime);

    public async Task InitializeAsync()
    {
        await using var context = _db.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task EmailTakenBetweenCheckAndSave_ThrowsEmailAlreadyExists()
    {
        const string email = "race@store.local";
        await using var context = CreateContext(new InsertSameEmailFirst(_db, email));

        var error = await Should.ThrowAsync<ConflictException>(() => CreateService(context).RegisterAsync(Request(email)));

        error.Code.ShouldBe(ErrorCodes.EmailAlreadyExists);
        await using var check = _db.CreateContext();
        (await check.Users.SingleAsync()).FullName.ShouldBe("Race Winner");
        (await check.RefreshTokens.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task OtherSaveFailure_IsRethrown()
    {
        await using var context = CreateContext(new FailUserSave());

        await Should.ThrowAsync<DbUpdateException>(() => CreateService(context).RegisterAsync(Request("fails@store.local")));
    }

    private AppDbContext CreateContext(IInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_db.ConnectionString)
            .AddInterceptors(interceptor)
            .Options);

    private AuthService CreateService(AppDbContext context) =>
        new(
            context,
            new PasswordHasher(),
            new TokenService(
                Options.Create(new JwtOptions { Key = "race-test-signing-key-0123456789-abcdef", Issuer = "i", Audience = "a" }),
                _time),
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            _time,
            NullLogger<AuthService>.Instance);

    private static RegisterRequest Request(string email) => new("Race Loser", email, "+201000000000", "Valid#Pass1");

    private static bool AddsUser(DbContextEventData eventData) =>
        eventData.Context is { } context
        && context.ChangeTracker.Entries<User>().Any(entry => entry.State == EntityState.Added);

    /// <summary>Saves the same email through another context right before the service's save.</summary>
    private sealed class InsertSameEmailFirst(TestDatabase db, string email) : SaveChangesInterceptor
    {
        private bool _inserted;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_inserted || !AddsUser(eventData))
            {
                return result;
            }

            _inserted = true;
            await using var other = db.CreateContext();
            other.Users.Add(new User("Race Winner", email, "+201000000001", "winner-hash", Role.Customer, DateTime.UtcNow));
            await other.SaveChangesAsync(cancellationToken);
            return result;
        }
    }

    /// <summary>A save failure that has nothing to do with the email.</summary>
    private sealed class FailUserSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            AddsUser(eventData)
                ? throw new DbUpdateException("Simulated failure unrelated to the email.")
                : ValueTask.FromResult(result);
    }
}
