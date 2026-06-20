using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace GbcNet.App.Emulation;

internal static class RomFileFilter
{
    public const string UnsupportedDroppedFileMessage = "Drop a .gb or .gbc ROM file.";

    public static IStorageFile? GetFirstDroppedRom(IEnumerable<IStorageItem>? items) =>
        items?.OfType<IStorageFile>().FirstOrDefault(static file => IsRomFileName(file.Name));

    public static bool IsRomFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".gb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase);
    }

    public static DragDropEffects GetDragEffects(IEnumerable<DataFormat> formats) =>
        formats.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
}
