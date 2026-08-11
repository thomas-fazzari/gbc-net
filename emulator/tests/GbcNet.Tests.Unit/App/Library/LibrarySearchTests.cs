// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Library;

namespace GbcNet.Tests.Unit.App.Library;

public sealed class LibrarySearchTests
{
    [Fact]
    public async Task RefreshAsync_CancelsPreviousQueryDuringDebounce()
    {
        var secondResults = new List<LibraryEntry>();
        var searchedQueries = new List<LibraryQuery>();
        var loadedResults = new List<IReadOnlyList<LibraryEntry>>();
        using var search = new LibrarySearch(
            (query, _) =>
            {
                searchedQueries.Add(query);
                return Task.FromResult<IReadOnlyList<LibraryEntry>>(secondResults);
            },
            loadedResults.Add,
            Timeout.InfiniteTimeSpan
        );

        var first = search.RefreshAsync(new LibraryQuery(SearchText: "first"), debounce: true);
        var secondQuery = new LibraryQuery(SearchText: "second");
        var second = search.RefreshAsync(secondQuery, debounce: false);

        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        searchedQueries.Should().Equal(secondQuery);
        loadedResults.Should().ContainSingle().Which.Should().BeSameAs(secondResults);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotLoadCanceledInFlightResults()
    {
        var firstQuery = new LibraryQuery(SearchText: "first");
        var secondQuery = new LibraryQuery(SearchText: "second");
        var firstResults = new List<LibraryEntry>();
        var secondResults = new List<LibraryEntry>();
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var loadedResults = new List<IReadOnlyList<LibraryEntry>>();
        using var search = new LibrarySearch(SearchAsync, loadedResults.Add, TimeSpan.Zero);

        var first = search.RefreshAsync(firstQuery, debounce: false);
        await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var second = search.RefreshAsync(secondQuery, debounce: false);
        await second.WaitAsync(TestContext.Current.CancellationToken);
        releaseFirst.SetResult();
        await first.WaitAsync(TestContext.Current.CancellationToken);

        loadedResults.Should().ContainSingle().Which.Should().BeSameAs(secondResults);

        async Task<IReadOnlyList<LibraryEntry>> SearchAsync(
            LibraryQuery query,
            CancellationToken cancellationToken
        )
        {
            if (query == firstQuery)
            {
                firstStarted.SetResult();
                await releaseFirst.Task;
                return firstResults;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return secondResults;
        }
    }

    [Fact]
    public async Task Dispose_CancelsPendingDebounce()
    {
        var searched = false;
        var search = new LibrarySearch(
            (_, _) =>
            {
                searched = true;
                return Task.FromResult<IReadOnlyList<LibraryEntry>>([]);
            },
            _ => { },
            Timeout.InfiniteTimeSpan
        );
        var pending = search.RefreshAsync(default, debounce: true);

        search.Dispose();
        await pending.WaitAsync(TestContext.Current.CancellationToken);

        searched.Should().BeFalse();
    }
}
