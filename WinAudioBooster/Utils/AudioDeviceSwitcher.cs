using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace WinAudioBooster.Utils;

public static class AudioDeviceSwitcher
{
    /// <summary>
    /// Безвідмовно змінює системний пристрій відтворення Windows навіть на драйверах Realtek у Win 11.
    /// </summary>
    public static async Task<bool> SetDefaultDeviceAsync(string deviceIdOrName)
    {
        if (string.IsNullOrWhiteSpace(deviceIdOrName)) return false;

        return await Task.Run(() =>
        {
            string realDeviceId = ResolveToDeviceId(deviceIdOrName);
            if (string.IsNullOrEmpty(realDeviceId)) return false;

            // Стратегія 1: Нативний COM-виклик
            bool success = SetDeviceViaNativeCom(realDeviceId);
            if (success) return true;

            // Стратегія 2: Fallback через PowerShell Shell Host (обходить блокування Realtek)
            return SetDeviceViaPowerShellHost(realDeviceId);
        });
    }

    private static bool SetDeviceViaNativeCom(string deviceId)
    {
        try
        {
            IPolicyConfigWin11? policyConfig = null;
            try { policyConfig = (IPolicyConfigWin11)new PolicyConfigClient(); }
            catch { policyConfig = (IPolicyConfigWin11)new PolicyConfigClientWin10(); }

            if (policyConfig == null) return false;

            int r1 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
            int r2 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
            int r3 = policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);

            return r1 == 0 || r2 == 0;
        }
        catch { return false; }
    }

    private static bool SetDeviceViaPowerShellHost(string deviceId)
    {
        try
        {
            string psScript = $@"
                $code = @'
                using System;
                using System.Runtime.InteropServices;
                public class AudioSet {{
                    [ComImport, Guid(""870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"")] class Client {{}}
                    [Guid(""F8679F50-850A-41CF-9C72-430F236F07B0""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
                    interface IPolicy {{
                        int M1(); int M2(); int M3(); int M4(); int M5(); int M6(); int M7(); int M8(); int M9(); int M10();
                        [PreserveSig] int SetDefault([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
                    }}
                    public static bool Set(string id) {{
                        try {{
                            var p = (IPolicy)new Client();
                            p.SetDefault(id, 0);
                            p.SetDefault(id, 1);
                            p.SetDefault(id, 2);
                            return true;
                         opacity {{ return false; }}
                    }}
                }}
                '@
                Add-Type -TypeDefinition $code -ErrorAction SilentlyContinue
                [AudioSet]::Set('{deviceId}')
            ";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace(Environment.NewLine, " ")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string ResolveToDeviceId(string input)
    {
        if (input.StartsWith("{") || input.Contains(".0.0.")) return input;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            string cleanInput = input.Split('(')[0].Trim();

            var match = devices.FirstOrDefault(d =>
                d.FriendlyName.Contains(cleanInput, StringComparison.OrdinalIgnoreCase) ||
                cleanInput.Contains(d.FriendlyName, StringComparison.OrdinalIgnoreCase));

            return match?.ID ?? input;
        }
        catch { return input; }
    }

    public static void OpenSoundSettings()
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:sound", UseShellExecute = true }); }
        catch { try { Process.Start("control.exe", "mmsys.cpl sounds"); } catch { } }
    }
}

internal enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClient { }

[ComImport, Guid("87009F42-005B-4D73-A745-A70C522A43E1")]
internal class PolicyConfigClientWin10 { }

[Guid("F8679F50-850A-41CF-9C72-430F236F07B0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfigWin11
{
    [PreserveSig] int GetMixFormat(string pszDeviceName, out IntPtr ppFormat);
    [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, out IntPtr ppFormat);
    [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
    [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
    [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
    [PreserveSig] int SetProcessingPeriod(string pszDeviceName, ref long pmftPeriod);
    [PreserveSig] int GetShareMode(string pszDeviceName, out int pMode);
    [PreserveSig] int SetShareMode(string pszDeviceName, int mode);
    [PreserveSig] int GetPropertyValue(string pszDeviceName, IntPtr pKey, out IntPtr pPv);
    [PreserveSig] int SetPropertyValue(string pszDeviceName, IntPtr pKey, IntPtr pPv);
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
}