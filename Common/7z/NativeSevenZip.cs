namespace Patch.Core.Services;

// Managed archive reader on every platform; no Windows DLL initialization required.
public static class NativeSevenZip
{
    public static void EnsureInitialized() { }
}
