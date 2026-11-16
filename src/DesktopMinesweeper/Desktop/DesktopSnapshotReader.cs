using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace DesktopMinesweeper.Desktop;

public readonly record struct MonitorRect(int Left, int Top, int Width, int Height);

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed record DesktopIconInfo(
    int Index,
    int ClientX,
    int ClientY,
    int ScreenX,
    int ScreenY,
    string Text);

public sealed record DesktopSnapshot(
    IntPtr ListViewHandle,
    PixelRect ListViewRect,
    int Count,
    int SpacingX,
    int SpacingY,
    IReadOnlyList<DesktopIconInfo> Icons,
    string? Warning)
{
    public static DesktopSnapshot Empty(string warning) => new(
        IntPtr.Zero,
        default,
        0,
        0,
        0,
        Array.Empty<DesktopIconInfo>(),
        warning);
}

/// <summary>
/// Reads desktop icon metadata without ever changing it.
/// Only LVM_GET* messages are sent; the remote buffer is used solely so Explorer
/// can fill in position/text results for us.
/// </summary>
public static class DesktopSnapshotReader
{
    private const int MaxItems = 500;
    private const int RemoteBufferSize = 4096;
    private const int LvItemOffset = 0;
    private const int PointOffset = 512;
    private const int TextOffset = 1024;
    private const int TextCapacityChars = 512;
    private const uint MessageTimeoutMs = 1000;

    public static DesktopSnapshot Read(int maxItems = MaxItems)
    {
        var listView = FindDesktopListView(out var findError);
        if (listView == IntPtr.Zero)
        {
            return DesktopSnapshot.Empty(findError ?? "Desktop icon list was not found.");
        }

        NativeMethods.GetWindowRect(listView, out var listRect);

        var count = SendGetCount(listView);
        if (count < 0)
        {
            return DesktopSnapshot.Empty("LVM_GETITEMCOUNT failed or timed out.");
        }

        var dpi = (int)NativeMethods.GetDpiForWindow(listView);
        if (dpi <= 0)
        {
            dpi = (int)NativeMethods.GetDpiForSystem();
        }

        var (spacingX, spacingY) = GetSpacing(listView, dpi);

        var icons = new List<DesktopIconInfo>();
        string? warning = null;

        NativeMethods.GetWindowThreadProcessId(listView, out var processId);
        var process = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_OPERATION |
            NativeMethods.PROCESS_VM_READ |
            NativeMethods.PROCESS_VM_WRITE |
            NativeMethods.PROCESS_QUERY_INFORMATION,
            false,
            processId);

        if (process == IntPtr.Zero)
        {
            return new DesktopSnapshot(
                listView,
                new PixelRect(listRect.Left, listRect.Top, listRect.Right, listRect.Bottom),
                count,
                spacingX,
                spacingY,
                icons,
                $"OpenProcess failed (Win32 error {Marshal.GetLastWin32Error()}); falling back to a fixed grid.");
        }

        var remote = IntPtr.Zero;
        try
        {
            remote = NativeMethods.VirtualAllocEx(
                process,
                IntPtr.Zero,
                new IntPtr(RemoteBufferSize),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);

            if (remote == IntPtr.Zero)
            {
                return new DesktopSnapshot(
                    listView,
                    new PixelRect(listRect.Left, listRect.Top, listRect.Right, listRect.Bottom),
                    count,
                    spacingX,
                    spacingY,
                    icons,
                    $"VirtualAllocEx failed (Win32 error {Marshal.GetLastWin32Error()}); falling back to a fixed grid.");
            }

            var limit = Math.Min(Math.Min(count, maxItems), MaxItems);
            for (var i = 0; i < limit; i++)
            {
                if (!TryReadPosition(process, remote, listView, i, out var point))
                {
                    warning = $"Could not read position of icon #{i}.";
                    continue;
                }

                var text = TryReadText(process, remote, listView, i);

                icons.Add(new DesktopIconInfo(
                    i,
                    point.X,
                    point.Y,
                    listRect.Left + point.X,
                    listRect.Top + point.Y,
                    text));
            }

            if (count > limit)
            {
                warning = $"Only the first {limit} icons were read (desktop has {count}).";
            }
        }
        catch (Exception ex)
        {
            warning = $"Desktop icon read failed: {ex.Message}";
        }
        finally
        {
            if (remote != IntPtr.Zero)
            {
                NativeMethods.VirtualFreeEx(process, remote, IntPtr.Zero, NativeMethods.MEM_RELEASE);
            }

            NativeMethods.CloseHandle(process);
        }

        return new DesktopSnapshot(
            listView,
            new PixelRect(listRect.Left, listRect.Top, listRect.Right, listRect.Bottom),
            count,
            spacingX,
            spacingY,
            icons,
            warning);
    }

    public static MonitorRect PrimaryMonitor
    {
        get
        {
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };

            var monitor = NativeMethods.MonitorFromPoint(
                new NativeMethods.POINT { X = 0, Y = 0 },
                NativeMethods.MONITOR_DEFAULTTOPRIMARY);

            if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfoW(monitor, ref info))
            {
                return new MonitorRect(
                    info.rcMonitor.Left,
                    info.rcMonitor.Top,
                    info.rcMonitor.Right - info.rcMonitor.Left,
                    info.rcMonitor.Bottom - info.rcMonitor.Top);
            }

            return new MonitorRect(
                0,
                0,
                (int)SystemParameters.PrimaryScreenWidth,
                (int)SystemParameters.PrimaryScreenHeight);
        }
    }

    public static uint SystemDpi => NativeMethods.GetDpiForSystem();

    private static int SendGetCount(IntPtr listView)
    {
        if (!Send(listView, NativeMethods.LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero, out var result))
        {
            return -1;
        }

        return (int)result.ToInt64();
    }

    private static (int X, int Y) GetSpacing(IntPtr listView, int dpi)
    {
        // Spacing is requested through a remote POINT because the list view runs in
        // another process. If that fails we use the classic 75 DIP slot size.
        var fallback = Math.Max(1, 75 * dpi / 96);
        NativeMethods.GetWindowThreadProcessId(listView, out var processId);
        var process = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_QUERY_INFORMATION,
            false,
            processId);
        if (process == IntPtr.Zero)
        {
            return (fallback, fallback);
        }

        var remote = IntPtr.Zero;
        try
        {
            remote = NativeMethods.VirtualAllocEx(
                process,
                IntPtr.Zero,
                new IntPtr(64),
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);
            if (remote == IntPtr.Zero)
            {
                return (fallback, fallback);
            }

            if (!Send(listView, NativeMethods.LVM_GETITEMSPACING, IntPtr.Zero, remote, out _))
            {
                return (fallback, fallback);
            }

            if (!ReadStruct(process, remote, out NativeMethods.POINT spacing) || spacing.X <= 0 || spacing.Y <= 0)
            {
                return (fallback, fallback);
            }

            return (spacing.X, spacing.Y);
        }
        finally
        {
            if (remote != IntPtr.Zero)
            {
                NativeMethods.VirtualFreeEx(process, remote, IntPtr.Zero, NativeMethods.MEM_RELEASE);
            }

            NativeMethods.CloseHandle(process);
        }
    }

    private static bool TryReadPosition(IntPtr process, IntPtr remote, IntPtr listView, int index, out NativeMethods.POINT point)
    {
        point = default;
        var pointAddress = IntPtr.Add(remote, PointOffset);
        if (!Send(listView, NativeMethods.LVM_GETITEMPOSITION, new IntPtr(index), pointAddress, out _))
        {
            return false;
        }

        return ReadStruct(process, pointAddress, out point);
    }

    private static string TryReadText(IntPtr process, IntPtr remote, IntPtr listView, int index)
    {
        var itemAddress = IntPtr.Add(remote, LvItemOffset);
        var textAddress = IntPtr.Add(remote, TextOffset);

        var item = new NativeMethods.LVITEMW
        {
            mask = NativeMethods.LVIF_TEXT,
            iItem = index,
            iSubItem = 0,
            pszText = textAddress,
            cchTextMax = TextCapacityChars
        };

        if (!WriteStruct(process, itemAddress, item))
        {
            return string.Empty;
        }

        if (!Send(listView, NativeMethods.LVM_GETITEMTEXTW, new IntPtr(index), itemAddress, out var result))
        {
            return string.Empty;
        }

        var bytes = new byte[TextCapacityChars * 2];
        var ok = NativeMethods.ReadProcessMemory(
            process,
            textAddress,
            bytes,
            new IntPtr(bytes.Length),
            out var read);
        if (!ok || read.ToInt64() <= 0)
        {
            return string.Empty;
        }

        var length = (int)Math.Min(read.ToInt64() / 2, TextCapacityChars);
        var reported = (int)result.ToInt64();
        if (reported > 0)
        {
            length = Math.Min(length, reported);
        }

        var text = Encoding.Unicode.GetString(bytes, 0, length * 2);
        var terminator = text.IndexOf('\0');
        return (terminator >= 0 ? text[..terminator] : text).Trim();
    }

    private static bool Send(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, out IntPtr result)
    {
        var ok = NativeMethods.SendMessageTimeoutW(
            window,
            message,
            wParam,
            lParam,
            NativeMethods.SMTO_BLOCK | NativeMethods.SMTO_ABORTIFHUNG,
            MessageTimeoutMs,
            out result);

        return ok != IntPtr.Zero;
    }

    private static bool ReadStruct<T>(IntPtr process, IntPtr address, out T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        var ok = NativeMethods.ReadProcessMemory(process, address, bytes, new IntPtr(size), out var read);
        if (!ok || read.ToInt64() < size)
        {
            value = default;
            return false;
        }

        value = Read<T>(bytes);
        return true;
    }

    private static bool WriteStruct<T>(IntPtr process, IntPtr address, T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        return NativeMethods.WriteProcessMemory(process, address, bytes, new IntPtr(size), out _);
    }

    private static T Read<T>(byte[] bytes) where T : struct
    {
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private static IntPtr FindDesktopListView(out string? error)
    {
        error = null;

        var progman = NativeMethods.FindWindowW("Progman", null);
        var listView = FindListViewUnder(progman);
        if (listView != IntPtr.Zero)
        {
            return listView;
        }

        // On Windows 10 the desktop view can be re-parented under a visible WorkerW
        // window (wallpaper slideshows, dynamic wallpapers, some shell extensions).
        var found = IntPtr.Zero;
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            var className = GetClassName(hwnd);
            if (className == "WorkerW")
            {
                var candidate = FindListViewUnder(hwnd);
                if (candidate != IntPtr.Zero)
                {
                    found = candidate;
                    return false;
                }
            }

            return true;
        }, IntPtr.Zero);

        if (found == IntPtr.Zero)
        {
            error = "SHELLDLL_DefView / SysListView32 not found (Explorer may be restarting).";
        }

        return found;
    }

    private static IntPtr FindListViewUnder(IntPtr parent)
    {
        if (parent == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var defView = NativeMethods.FindWindowExW(parent, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView != IntPtr.Zero)
        {
            var listView = NativeMethods.FindWindowExW(defView, IntPtr.Zero, "SysListView32", null);
            if (listView != IntPtr.Zero)
            {
                return listView;
            }
        }

        return NativeMethods.FindWindowExW(parent, IntPtr.Zero, "SysListView32", null);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        NativeMethods.GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
