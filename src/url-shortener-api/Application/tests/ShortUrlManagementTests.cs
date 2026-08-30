using UrlShortener.Application.Exceptions;
using UrlShortener.Application.Handlers;
using UrlShortener.Application.Handlers.Commands;
using UrlShortener.Application.Tests;
using UrlShortener.Domain.Entities;
using Xunit;

namespace Application.Tests;

public sealed class ShortUrlManagementTests
{
    [Fact]
    public async Task Create_inserts_an_independent_resource_and_returns_its_representation()
    {
        var repository = new TestShortUrlRepository();
        var handler = CreateHandler(repository);

        var result = await handler.HandleAsync(new CreateShortUrlCommand
        {
            OwnerId = "owner",
            Url = "https://example.com"
        });

        Assert.Equal("code00", result.ShortCode);
        Assert.Equal("http://localhost/code00", result.ShortUrl);
        Assert.Single(repository.Inserted);
    }

    [Fact]
    public async Task Create_retries_conflicts_with_one_nonce_and_incrementing_counters()
    {
        var repository = new TestShortUrlRepository { Conflicts = 2 };
        var generator = new TestCodeGenerator();
        var handler = CreateHandler(repository, generator);

        var result = await handler.HandleAsync(new CreateShortUrlCommand
        {
            OwnerId = "owner",
            Url = "https://example.com"
        });

        Assert.Equal("code02", result.ShortCode);
        Assert.Equal([0, 1, 2], generator.Counters);
        Assert.Single(generator.Nonces.Distinct());
        Assert.Single(repository.Inserted);
    }

    [Fact]
    public async Task Create_exhausts_five_conflicts_and_propagates_other_failures()
    {
        var repository = new TestShortUrlRepository { Conflicts = 5 };
        var generator = new TestCodeGenerator();
        var handler = CreateHandler(repository, generator);

        await Assert.ThrowsAsync<ShortCodeAttemptsExhaustedException>(() =>
            handler.HandleAsync(new CreateShortUrlCommand
            {
                OwnerId = "owner",
                Url = "https://example.com"
            }));
        Assert.Equal([0, 1, 2, 3, 4], generator.Counters);

        repository.Error = new IOException("unexpected persistence failure");
        await Assert.ThrowsAsync<IOException>(() =>
            handler.HandleAsync(new CreateShortUrlCommand
            {
                OwnerId = "owner",
                Url = "https://example.com"
            }));
    }

    [Fact]
    public async Task Update_preserves_code_and_uses_owner_scoped_lookup()
    {
        var entity = ShortUrl.Create(
            "abc123",
            "https://old.example",
            "owner",
            DateTimeOffset.UtcNow);
        var repository = new TestShortUrlRepository { Existing = entity };
        var handler = new UpdateShortUrlCommandHandler(
            repository,
            new TestClock(),
            new TestUrlBuilder());

        var result = await handler.HandleAsync(new UpdateShortUrlCommand
        {
            OwnerId = "owner",
            ShortCode = "abc123",
            Url = "https://new.example"
        });

        Assert.NotNull(result);
        Assert.Equal("abc123", result.ShortCode);
        Assert.Equal("owner", repository.LastOwner);
        Assert.Equal("https://new.example", entity.LongUrl);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Update_and_delete_return_not_found_without_saving_when_lookup_misses()
    {
        var repository = new TestShortUrlRepository();
        var update = new UpdateShortUrlCommandHandler(
            repository,
            new TestClock(),
            new TestUrlBuilder());
        var delete = new DeleteShortUrlCommandHandler(repository, new TestClock());

        var updateResult = await update.HandleAsync(new UpdateShortUrlCommand
        {
            OwnerId = "owner",
            ShortCode = "abc123",
            Url = "https://new.example"
        });
        var deleteResult = await delete.HandleAsync(new DeleteShortUrlCommand
        {
            OwnerId = "owner",
            ShortCode = "abc123"
        });

        Assert.Null(updateResult);
        Assert.False(deleteResult);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Delete_marks_the_owned_active_resource()
    {
        var entity = ShortUrl.Create(
            "abc123",
            "https://example.com",
            "owner",
            DateTimeOffset.UtcNow);
        var repository = new TestShortUrlRepository { Existing = entity };
        var handler = new DeleteShortUrlCommandHandler(repository, new TestClock());

        var deleted = await handler.HandleAsync(new DeleteShortUrlCommand
        {
            OwnerId = "owner",
            ShortCode = "abc123"
        });

        Assert.True(deleted);
        Assert.True(entity.IsDeleted);
        Assert.Equal(1, repository.SaveCount);
    }

    private static CreateShortUrlCommandHandler CreateHandler(
        TestShortUrlRepository repository,
        TestCodeGenerator? generator = null) =>
        new(
            repository,
            generator ?? new TestCodeGenerator(),
            new TestClock(),
            new TestUrlBuilder());
}
