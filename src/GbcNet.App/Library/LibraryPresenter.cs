using GbcNet.App.Common;
using GbcNet.App.Shell;

namespace GbcNet.App.Library;

internal sealed class LibraryPresenter
{
    private readonly LibraryService _libraryService;
    private readonly LibraryView _view;

    public LibraryPresenter(
        LibraryView view,
        LibraryService libraryService,
        ShellOperationRunner operationRunner,
        Func<string, Task> openRomAsync
    )
    {
        _libraryService = libraryService;
        _view = view;

        view.RomSelected = entry =>
            operationRunner.Run(async () =>
            {
                await openRomAsync(entry.LastKnownPath).ConfigureAwait(true);
                Refresh();
            });
    }

    public void Refresh()
    {
        var entries = _libraryService.GetRoms();
        if (entries.IsSuccess)
        {
            _view.Load(entries.Value);
        }
        else
        {
            _view.ShowError(ResultErrors.Format(entries.Errors));
        }
    }
}
