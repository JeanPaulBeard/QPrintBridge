using System.ComponentModel;
using System.Runtime.InteropServices;

namespace QPrintBridge;

public enum ServiceState
{
    Unknown = -1,
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7
}

public static class ServiceManager
{
    public const string ServiceName = "QPrintBridge Service";
    private const string ServiceDisplayName = "QPrintBridge Service";
    private const string ServiceDescription = "Provee un puente HTTP local al puerto 19100 a impresoras de Windows.";

    private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    private const uint SC_MANAGER_CONNECT = 0x0001;
    private const uint SERVICE_ALL_ACCESS = 0xF01FF;
    private const uint SERVICE_QUERY_STATUS = 0x0004;
    private const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    private const uint SERVICE_AUTO_START = 0x00000002;
    private const uint SERVICE_ERROR_NORMAL = 0x00000001;
    private const uint SERVICE_CONTROL_STOP = 0x00000001;

    private const int ERROR_SERVICE_EXISTS = 1073;
    private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
    private const int ERROR_SERVICE_ALREADY_RUNNING = 1056;
    private const int ERROR_SERVICE_NOT_ACTIVE = 1062;
    private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SERVICE_DESCRIPTION
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpDescription;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, uint dwDesiredAccess, uint dwServiceType, uint dwStartType, uint dwErrorControl, string lpBinaryPathName, string? lpLoadOrderGroup, IntPtr lpdwTagId, string? lpDependencies, string? lpServiceStartName, string? lpPassword);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(IntPtr hService);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(IntPtr hService, uint dwNumServiceArgs, IntPtr lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatus(IntPtr hService, ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, IntPtr lpInfo);

    public static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public static void RestartElevated()
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            UseShellExecute = true,
            Verb = "runas"
        };
        System.Diagnostics.Process.Start(startInfo);
    }

    public static ServiceState GetStatus()
    {
        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
            return ServiceState.Unknown;

        try
        {
            IntPtr svc = OpenService(scm, ServiceName, SERVICE_QUERY_STATUS);
            if (svc == IntPtr.Zero)
                return ServiceState.Unknown;

            try
            {
                SERVICE_STATUS status = default;
                if (!QueryServiceStatus(svc, ref status))
                    return ServiceState.Unknown;
                return (ServiceState)status.dwCurrentState;
            }
            finally
            {
                CloseServiceHandle(svc);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static void Install()
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable.");

        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo abrir el Administrador de Control de Servicios (¿permisos de administrador?).");

        try
        {
            IntPtr svc = CreateService(scm, ServiceName, ServiceDisplayName, SERVICE_ALL_ACCESS, SERVICE_WIN32_OWN_PROCESS, SERVICE_AUTO_START, SERVICE_ERROR_NORMAL, $"\"{exePath}\"", null, IntPtr.Zero, null, null, null);
            if (svc == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_SERVICE_EXISTS)
                    throw new InvalidOperationException("El servicio ya está instalado.");
                throw new Win32Exception(error, "No se pudo crear el servicio.");
            }

            try
            {
                SetDescription(svc);
            }
            finally
            {
                CloseServiceHandle(svc);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static void Uninstall()
    {
        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo abrir el Administrador de Control de Servicios (¿permisos de administrador?).");

        try
        {
            IntPtr svc = OpenService(scm, ServiceName, SERVICE_ALL_ACCESS);
            if (svc == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_SERVICE_DOES_NOT_EXIST)
                    throw new InvalidOperationException("El servicio no está instalado.");
                throw new Win32Exception(error, "No se pudo abrir el servicio.");
            }

            try
            {
                StopInternal(svc);
                if (!DeleteService(svc))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == ERROR_SERVICE_MARKED_FOR_DELETE)
                        throw new InvalidOperationException("El servicio ya está marcado para eliminar.");
                    throw new Win32Exception(error, "No se pudo eliminar el servicio.");
                }
            }
            finally
            {
                CloseServiceHandle(svc);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static void Start()
    {
        WithService(SERVICE_ALL_ACCESS, svc =>
        {
            if (!StartService(svc, 0, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_SERVICE_ALREADY_RUNNING)
                    return;
                throw new Win32Exception(error, "No se pudo iniciar el servicio.");
            }
        });
    }

    public static void Stop()
    {
        WithService(SERVICE_ALL_ACCESS, svc => StopInternal(svc));
    }

    private static void StopInternal(IntPtr svc)
    {
        SERVICE_STATUS status = default;
        if (ControlService(svc, SERVICE_CONTROL_STOP, ref status))
        {
            WaitForState(svc, ServiceState.Stopped);
        }
        else
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ERROR_SERVICE_NOT_ACTIVE)
                return;
            throw new Win32Exception(error, "No se pudo detener el servicio.");
        }
    }

    private static void WaitForState(IntPtr svc, ServiceState target)
    {
        for (int i = 0; i < 30; i++)
        {
            SERVICE_STATUS status = default;
            if (QueryServiceStatus(svc, ref status) && (ServiceState)status.dwCurrentState == target)
                return;
            Thread.Sleep(1000);
        }
        throw new TimeoutException("El servicio no respondió a tiempo.");
    }

    private static void WithService(uint desiredAccess, Action<IntPtr> action)
    {
        IntPtr scm = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scm == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "No se pudo abrir el Administrador de Control de Servicios (¿permisos de administrador?).");

        try
        {
            IntPtr svc = OpenService(scm, ServiceName, desiredAccess);
            if (svc == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_SERVICE_DOES_NOT_EXIST)
                    throw new InvalidOperationException("El servicio no está instalado.");
                throw new Win32Exception(error, "No se pudo abrir el servicio.");
            }

            try
            {
                action(svc);
            }
            finally
            {
                CloseServiceHandle(svc);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    private static void SetDescription(IntPtr svc)
    {
        var desc = new SERVICE_DESCRIPTION { lpDescription = ServiceDescription };
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<SERVICE_DESCRIPTION>());
        try
        {
            Marshal.StructureToPtr(desc, ptr, false);
            ChangeServiceConfig2(svc, 1, ptr); // SERVICE_CONFIG_DESCRIPTION = 1
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
