using FluentResults;

namespace GbcNet.App.Shell;

internal static class ResultErrors
{
    public static string Format(IEnumerable<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(static error => error.Message));
}
