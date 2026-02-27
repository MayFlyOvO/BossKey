using System.Runtime.InteropServices;

namespace HideProcess.Core.Services;

internal sealed class ProcessAudioMuteService
{
    public IReadOnlyDictionary<int, bool> CaptureAndMuteProcesses(IReadOnlyCollection<int> processIds)
    {
        if (processIds.Count == 0)
        {
            return new Dictionary<int, bool>();
        }

        var targets = processIds.ToHashSet();
        var states = new Dictionary<int, ProcessMuteState>();
        var eventContext = Guid.Empty;

        TryForEachAudioSession((sessionProcessId, volume) =>
        {
            if (!targets.Contains(sessionProcessId))
            {
                return;
            }

            if (!states.TryGetValue(sessionProcessId, out var state))
            {
                state = new ProcessMuteState(sessionProcessId);
                states[sessionProcessId] = state;
            }

            var hrGet = volume.GetMute(out var isMuted);
            if (hrGet == 0)
            {
                state.HasMuteInfo = true;
                state.OriginalAllMuted &= isMuted;
            }

            var hrSet = volume.SetMute(true, ref eventContext);
            if (hrSet != 0)
            {
                state.SetMuteFailed = true;
            }
        });

        var snapshots = new Dictionary<int, bool>();
        foreach (var state in states.Values)
        {
            if (state.HasMuteInfo && !state.SetMuteFailed)
            {
                snapshots[state.ProcessId] = state.OriginalAllMuted;
            }
        }

        return snapshots;
    }

    public void RestoreMuteStates(IReadOnlyDictionary<int, bool> originalMuteStates)
    {
        if (originalMuteStates.Count == 0)
        {
            return;
        }

        var targets = originalMuteStates.Keys.ToHashSet();
        var eventContext = Guid.Empty;

        TryForEachAudioSession((sessionProcessId, volume) =>
        {
            if (!targets.Contains(sessionProcessId))
            {
                return;
            }

            var desiredMute = originalMuteStates[sessionProcessId];
            volume.SetMute(desiredMute, ref eventContext);
        });
    }

    private static bool TryForEachAudioSession(Action<int, ISimpleAudioVolume> action)
    {
        IMMDeviceEnumerator? deviceEnumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? sessionManager = null;
        IAudioSessionEnumerator? sessionEnumerator = null;

        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(ComGuids.MMDeviceEnumeratorClassId)!)!;

            var hr = deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
            if (hr != 0 || device is null)
            {
                return false;
            }

            var audioSessionManager2Guid = typeof(IAudioSessionManager2).GUID;
            hr = device.Activate(ref audioSessionManager2Guid, (int)ClsCtx.InprocServer, IntPtr.Zero, out var managerObject);
            if (hr != 0 || managerObject is null)
            {
                return false;
            }

            sessionManager = (IAudioSessionManager2)managerObject;
            hr = sessionManager.GetSessionEnumerator(out sessionEnumerator);
            if (hr != 0 || sessionEnumerator is null)
            {
                return false;
            }

            hr = sessionEnumerator.GetCount(out var count);
            if (hr != 0)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                IAudioSessionControl? sessionControl = null;
                IAudioSessionControl2? sessionControl2 = null;
                ISimpleAudioVolume? volume = null;

                try
                {
                    hr = sessionEnumerator.GetSession(i, out sessionControl);
                    if (hr != 0 || sessionControl is null)
                    {
                        continue;
                    }

                    sessionControl2 = sessionControl as IAudioSessionControl2;
                    if (sessionControl2 is null)
                    {
                        continue;
                    }

                    hr = sessionControl2.GetProcessId(out var processId);
                    if (hr != 0 || processId == 0)
                    {
                        continue;
                    }

                    volume = sessionControl as ISimpleAudioVolume;
                    if (volume is null)
                    {
                        continue;
                    }

                    action(unchecked((int)processId), volume);
                }
                finally
                {
                    ReleaseComObject(volume);
                    ReleaseComObject(sessionControl2);
                    ReleaseComObject(sessionControl);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            ReleaseComObject(sessionEnumerator);
            ReleaseComObject(sessionManager);
            ReleaseComObject(device);
            ReleaseComObject(deviceEnumerator);
        }
    }

    private static void ReleaseComObject<T>(T? comObject)
        where T : class
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    private sealed class ProcessMuteState
    {
        public ProcessMuteState(int processId)
        {
            ProcessId = processId;
        }

        public int ProcessId { get; }
        public bool HasMuteInfo { get; set; }
        public bool OriginalAllMuted { get; set; } = true;
        public bool SetMuteFailed { get; set; }
    }

    private static class ComGuids
    {
        public static readonly Guid MMDeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    }

    private enum EDataFlow
    {
        Render = 0
    }

    private enum ERole
    {
        Multimedia = 1
    }

    [Flags]
    private enum ClsCtx
    {
        InprocServer = 0x1
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(
            ref Guid iid,
            int clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.Interface)] out object interfacePointer);

        int OpenPropertyStore(int stgmAccess, out IntPtr properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IntPtr sessionControl);
        int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out IntPtr audioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        int RegisterSessionNotification(IntPtr sessionNotification);
        int UnregisterSessionNotification(IntPtr sessionNotification);
        int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr duckNotification);
        int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        int GetCount(out int sessionCount);
        int GetSession(int sessionCount, out IAudioSessionControl sessionControl);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        int GetState(out int state);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
        int GetGroupingParam(out Guid groupingId);
        int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        int RegisterAudioSessionNotification(IntPtr client);
        int UnregisterAudioSessionNotification(IntPtr client);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2 : IAudioSessionControl
    {
        new int GetState(out int state);
        new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
        new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
        new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
        new int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
        new int GetGroupingParam(out Guid groupingId);
        new int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        new int RegisterAudioSessionNotification(IntPtr client);
        new int UnregisterAudioSessionNotification(IntPtr client);

        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);
        int GetProcessId(out uint processId);
        int IsSystemSoundsSession();
        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        int SetMasterVolume(float level, ref Guid eventContext);
        int GetMasterVolume(out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }
}
