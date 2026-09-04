using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ConferenceHallBooking.Domain;

/// <summary>
/// Generates GUIDs that insert near the end of a SQL Server clustered uniqueidentifier index,
/// reducing page splits compared to <see cref="Guid.NewGuid"/>.
/// </summary>
public static class SequentialGuid
{
    public static Guid Create()
    {
        if (OperatingSystem.IsWindows())
            return CreateSqlServerSequentialGuid();

        // Non-Windows: time-ordered UUID v7 is the closest built-in alternative.
        return Guid.CreateVersion7();
    }

    [SupportedOSPlatform("windows")]
    private static Guid CreateSqlServerSequentialGuid()
    {
        var status = UuidCreateSequential(out var guid);
        if (status != 0)
            return Guid.CreateVersion7();

        var bytes = guid.ToByteArray();

        // SQL Server orders uniqueidentifier differently from .NET Guid byte layout.
        Swap(bytes, 0, 3);
        Swap(bytes, 1, 2);
        Swap(bytes, 4, 5);
        Swap(bytes, 6, 7);

        return new Guid(bytes);
    }

    private static void Swap(byte[] bytes, int left, int right) =>
        (bytes[left], bytes[right]) = (bytes[right], bytes[left]);

    [DllImport("rpcrt4.dll", SetLastError = true)]
    private static extern int UuidCreateSequential(out Guid guid);
}
