using System.Runtime.InteropServices;

namespace QPrintBridge;

/// <summary>
/// Provee acceso de bajo nivel a las APIs de impresión de Windows (winspool.drv).
/// Permite enviar un arreglo de bytes (como comandos ESC/POS) directamente a la cola de impresión.
/// </summary>
public static class RawPrinterHelper
{
    // Estructura requerida por OpenPrinter
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDatatype;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>
    /// Envía un arreglo de bytes RAW a la impresora especificada.
    /// </summary>
    public static bool SendBytesToPrinter(string szPrinterName, byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return false;
        }

        IntPtr pUnmanagedBytes = new IntPtr(0);
        int dwCount = data.Length;

        // Allocate unmanaged memory para los bytes
        pUnmanagedBytes = Marshal.AllocCoTaskMem(dwCount);
        Marshal.Copy(data, 0, pUnmanagedBytes, dwCount);

        bool success = SendBytesToPrinter(szPrinterName, pUnmanagedBytes, dwCount);

        // Limpiar memoria unmanaged
        Marshal.FreeCoTaskMem(pUnmanagedBytes);

        return success;
    }

    private static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, int dwCount)
    {
        int dwError = 0, dwWritten = 0;
        IntPtr hPrinter = new IntPtr(0);
        DOCINFOA di = new DOCINFOA();
        bool success = false;

        di.pDocName = "QPrintBridge RAW Document";
        di.pDatatype = "RAW";

        if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
        {
            if (StartDocPrinter(hPrinter, 1, di))
            {
                if (StartPagePrinter(hPrinter))
                {
                    success = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }

        if (!success)
        {
            dwError = Marshal.GetLastWin32Error();
            SimpleLogger.LogError($"Falló SendBytesToPrinter para '{szPrinterName}'. Error Win32: {dwError}");
        }

        return success;
    }
}
