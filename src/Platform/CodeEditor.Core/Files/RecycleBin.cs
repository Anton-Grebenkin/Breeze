using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using CodeEditor.Core.Resources;

namespace CodeEditor.Core.Files;

/// <summary>
/// Deletes to the Windows Recycle Bin via <c>SHFileOperation</c> with the undo flag, without shell dialogs.
/// </summary>
internal static class RecycleBin
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    public static void Delete(string path)
    {
        // The path list ends with two null characters.
        var operation = new ShFileOperation
        {
            Function = FoDelete,
            From = Path.GetFullPath(path) + "\0\0",
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi,
        };

        var result = SHFileOperation(ref operation);
        if (result != 0 || operation.AnyOperationsAborted)
        {
            throw new IOException(string.Format(CultureInfo.CurrentCulture, Strings.RecycleBinDeleteFailed, path), new Win32Exception(result));
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHFileOperation(ref ShFileOperation operation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOperation
    {
        public nint Window;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public nint NameMappings;
        public string? ProgressTitle;
    }
}
