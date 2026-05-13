using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using SoundTracker.App.Diagnostics;
using SoundTracker.App.Processes;

namespace SoundTracker.App.Audio;

internal sealed class AudioSessionMonitor : IAudioSessionSource
{
    private const int MaxBufferedActivities = 256;

    private readonly object _sync = new();
    private readonly BlockingCollection<Action?> _workQueue = new();
    private readonly ProcessNameResolver _processNameResolver = new();
    private readonly List<AudioActivityEvent> _recentActivities = [];
    private readonly Dictionary<string, TrackedSession> _trackedSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly EndpointNotificationClient _endpointNotificationClient;
    private readonly AudioSessionNotificationSink _sessionNotificationSink;
    private readonly ManualResetEventSlim _startupCompleted = new(false);
    private readonly Thread _workerThread;

    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _defaultDevice;
    private IAudioSessionManager2? _sessionManager;
    private bool _endpointNotificationsRegistered;
    private bool _sessionNotificationsRegistered;
    private bool _disposed;
    private Exception? _startupException;

    public AudioSessionMonitor()
    {
        AppLog.Info("audio session monitor constructing");
        _endpointNotificationClient = new EndpointNotificationClient(HandleDefaultEndpointChanged);
        _sessionNotificationSink = new AudioSessionNotificationSink(HandleSessionCreated);
        _workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "SoundTracker.AudioSessionMonitor",
        };
        _workerThread.SetApartmentState(ApartmentState.MTA);
        _workerThread.Start();
        AppLog.Info("audio monitor worker thread started");

        _startupCompleted.Wait();
        if (_startupException is not null)
        {
            AppLog.Error("audio session monitor startup failed", _startupException);
            throw new InvalidOperationException("Failed to initialize the audio session monitor.", _startupException);
        }

        AppLog.Info("audio session monitor startup completed");
    }

    public event EventHandler<AudioActivityEventArgs>? ActivityRecorded;

    public event EventHandler? SessionsChanged;

    public IReadOnlyList<string> GetActiveSessionNames()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            return _trackedSessions.Values
                .Where(session => session.State == AudioSessionState.Active)
                .Select(session => session.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<AudioActivityEvent> GetRecentActivities()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _recentActivities.ToList();
        }
    }

    public void Dispose()
    {
        var shouldStop = false;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            shouldStop = true;
        }

        if (shouldStop)
        {
            AppLog.Info("audio session monitor disposing");
            _workQueue.Add(null);
            _workerThread.Join();
            _workQueue.Dispose();
            _startupCompleted.Dispose();
            AppLog.Info("audio session monitor disposed");
        }

        GC.SuppressFinalize(this);
    }

    private void WorkerLoop()
    {
        var coInitialized = false;

        try
        {
            AppLog.Info("audio monitor worker loop starting");
            var hr = CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            coInitialized = true;
            AppLog.Info($"audio monitor CoInitializeEx succeeded hr=0x{hr:X8}");

            lock (_sync)
            {
                if (!_disposed)
                {
                    AppLog.Info("audio monitor attaching to default render endpoint");
                    TryAttachToDefaultRenderEndpointLocked(allowMissingDefaultEndpoint: true);
                }
            }

            _startupCompleted.Set();

            if (!_disposed)
            {
                RaiseSessionsChanged();
            }

            foreach (var workItem in _workQueue.GetConsumingEnumerable())
            {
                if (workItem is null)
                {
                    AppLog.Info("audio monitor worker received shutdown sentinel");
                    break;
                }

                AppLog.Info("audio monitor worker executing queued action");
                workItem();
            }
        }
        catch (Exception ex)
        {
            _startupException ??= ex;
            _startupCompleted.Set();
            AppLog.Error("audio monitor worker loop faulted", ex);
        }
        finally
        {
            lock (_sync)
            {
                AppLog.Info("audio monitor worker tearing down");
                TeardownLocked();
            }

            if (coInitialized)
            {
                CoUninitialize();
                AppLog.Info("audio monitor CoUninitialize completed");
            }
        }
    }

    private bool TryAttachToDefaultRenderEndpointLocked(bool allowMissingDefaultEndpoint)
    {
        EnsureDeviceEnumeratorLocked();

        try
        {
            Marshal.ThrowExceptionForHR(
                _deviceEnumerator!.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out _defaultDevice));
        }
        catch (COMException ex) when (allowMissingDefaultEndpoint && ex.HResult == unchecked((int)0x80070490))
        {
            TryReleaseComObject(_defaultDevice);
            _defaultDevice = null;
            AppLog.Warn("audio monitor could not find a default render endpoint; waiting for the next endpoint callback");
            return false;
        }

        var sessionManagerGuid = typeof(IAudioSessionManager2).GUID;
        Marshal.ThrowExceptionForHR(
            _defaultDevice!.Activate(ref sessionManagerGuid, CLSCTX.InProcServer, IntPtr.Zero, out var managerObject));
        _sessionManager = (IAudioSessionManager2)managerObject;

        Marshal.ThrowExceptionForHR(_sessionManager.RegisterSessionNotification(_sessionNotificationSink));
        _sessionNotificationsRegistered = true;

        AppLog.Info("audio monitor registered endpoint and session notifications");
        EnumerateAndTrackSessionsLocked(SessionDiscoveryKind.StartupSnapshot, []);
        return true;
    }

    private void EnsureDeviceEnumeratorLocked()
    {
        if (_deviceEnumerator is not null)
        {
            return;
        }

        _deviceEnumerator = CreateDeviceEnumerator();
        Marshal.ThrowExceptionForHR(
            _deviceEnumerator.RegisterEndpointNotificationCallback(_endpointNotificationClient));
        _endpointNotificationsRegistered = true;
    }

    private void EnumerateAndTrackSessionsLocked(
        SessionDiscoveryKind discoveryKind,
        List<AudioActivityEvent> recordedActivities)
    {
        IAudioSessionEnumerator? sessionEnumerator = null;

        try
        {
            Marshal.ThrowExceptionForHR(_sessionManager!.GetSessionEnumerator(out sessionEnumerator));
            Marshal.ThrowExceptionForHR(sessionEnumerator.GetCount(out var count));
            AppLog.Info($"audio monitor enumerating sessions count={count}");

            for (var index = 0; index < count; index++)
            {
                IAudioSessionControl? sessionControl = null;
                var releaseSessionControl = true;

                try
                {
                    Marshal.ThrowExceptionForHR(sessionEnumerator.GetSession(index, out sessionControl));
                    releaseSessionControl = !TryTrackSessionLocked(sessionControl, discoveryKind, recordedActivities);
                }
                catch (COMException)
                {
                    // Sessions can disappear while they are being enumerated.
                }
                finally
                {
                    if (releaseSessionControl)
                    {
                        TryReleaseComObject(sessionControl);
                    }
                }
            }
        }
        finally
        {
            TryReleaseComObject(sessionEnumerator);
        }
    }

    private bool TryTrackSessionLocked(
        IAudioSessionControl? sessionControl,
        SessionDiscoveryKind discoveryKind,
        List<AudioActivityEvent> recordedActivities)
    {
        if (sessionControl is not IAudioSessionControl2 sessionControl2)
        {
            return false;
        }

        string instanceId;
        try
        {
            Marshal.ThrowExceptionForHR(sessionControl2.GetSessionInstanceIdentifier(out instanceId));
        }
        catch (COMException)
        {
            return false;
        }

        if (_trackedSessions.ContainsKey(instanceId))
        {
            return false;
        }

        if (!TryReadSessionSnapshot(sessionControl2, out var snapshot))
        {
            return false;
        }

        var eventSink = new AudioSessionEventsSink(
            instanceId,
            HandleSessionStateChanged,
            HandleSessionDisconnected);

        Marshal.ThrowExceptionForHR(sessionControl.RegisterAudioSessionNotification(eventSink));

        DateTimeOffset? activeSinceUtc = snapshot.State == AudioSessionState.Active
            ? DateTimeOffset.UtcNow
            : null;
        _trackedSessions[instanceId] = new TrackedSession(
            instanceId,
            sessionControl2,
            eventSink,
            snapshot.ProcessId,
            snapshot.DisplayName,
            snapshot.State,
            activeSinceUtc);

        AppLog.Info($"audio monitor tracked session instanceId={instanceId} processId={snapshot.ProcessId} state={snapshot.State} name=\"{snapshot.DisplayName}\"");
        if (snapshot.State == AudioSessionState.Active && activeSinceUtc is not null)
        {
            recordedActivities.Add(CreateActivityForTrackedSession(
                trackedSession: _trackedSessions[instanceId],
                kind: discoveryKind == SessionDiscoveryKind.StartupSnapshot
                    ? AudioActivityKind.ObservedActive
                    : AudioActivityKind.Started,
                timestampUtc: activeSinceUtc.Value,
                duration: null));
        }

        return true;
    }

    private bool TryReadSessionSnapshot(
        IAudioSessionControl2 sessionControl2,
        out SessionSnapshot snapshot)
    {
        snapshot = default;

        try
        {
            Marshal.ThrowExceptionForHR(sessionControl2.GetState(out var state));
            Marshal.ThrowExceptionForHR(sessionControl2.GetProcessId(out var processId));

            var displayName = ResolveDisplayName(sessionControl2, processId);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            snapshot = new SessionSnapshot(processId, displayName, state);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private string? ResolveDisplayName(IAudioSessionControl2 sessionControl2, uint processId)
    {
        var systemSoundsHr = sessionControl2.IsSystemSoundsSession();
        if (systemSoundsHr == 0)
        {
            return "System Sounds";
        }

        if (processId == 0)
        {
            return null;
        }

        return _processNameResolver.TryGetProcessName(processId);
    }

    private void HandleSessionCreated(IAudioSessionControl newSession)
    {
        AppLog.Info("audio monitor OnSessionCreated callback");
        EnqueueWork(() =>
        {
            List<AudioActivityEvent> recordedActivities = [];
            lock (_sync)
            {
                if (_disposed || _sessionManager is null)
                {
                    return;
                }

                EnumerateAndTrackSessionsLocked(SessionDiscoveryKind.CallbackEnumeration, recordedActivities);
            }

            AppLog.Info("audio monitor OnSessionCreated processed");
            RaiseActivityRecorded(recordedActivities);
            RaiseSessionsChanged();
        });
    }

    private void HandleSessionStateChanged(string instanceId, AudioSessionState newState)
    {
        AppLog.Info($"audio monitor session state callback instanceId={instanceId} state={newState}");
        EnqueueWork(() =>
        {
            AudioActivityEvent? recordedActivity = null;
            lock (_sync)
            {
                if (_disposed || !_trackedSessions.TryGetValue(instanceId, out var trackedSession))
                {
                    return;
                }

                if (trackedSession.State == newState)
                {
                    return;
                }

                var changedAtUtc = DateTimeOffset.UtcNow;
                if (newState == AudioSessionState.Active)
                {
                    trackedSession.ActiveSinceUtc = changedAtUtc;
                    recordedActivity = CreateActivityForTrackedSession(
                        trackedSession,
                        AudioActivityKind.Started,
                        changedAtUtc,
                        duration: null);
                }
                else if (trackedSession.State == AudioSessionState.Active && newState != AudioSessionState.Active)
                {
                    recordedActivity = CreateStoppedActivity(trackedSession, changedAtUtc);
                    trackedSession.ActiveSinceUtc = null;
                }

                trackedSession.State = newState;
                if (newState == AudioSessionState.Expired)
                {
                    RemoveTrackedSessionLocked(instanceId, trackedSession);
                }
            }

            RaiseActivityRecorded(recordedActivity);
            RaiseSessionsChanged();
        });
    }

    private void HandleSessionDisconnected(string instanceId)
    {
        AppLog.Info($"audio monitor session disconnected callback instanceId={instanceId}");
        EnqueueWork(() =>
        {
            AudioActivityEvent? recordedActivity = null;
            lock (_sync)
            {
                if (_disposed || !_trackedSessions.TryGetValue(instanceId, out var trackedSession))
                {
                    return;
                }

                if (trackedSession.State == AudioSessionState.Active)
                {
                    recordedActivity = CreateStoppedActivity(trackedSession, DateTimeOffset.UtcNow);
                    trackedSession.ActiveSinceUtc = null;
                }

                RemoveTrackedSessionLocked(instanceId, trackedSession);
            }

            RaiseActivityRecorded(recordedActivity);
            RaiseSessionsChanged();
        });
    }

    private void HandleDefaultEndpointChanged(EDataFlow dataFlow, ERole role, string defaultDeviceId)
    {
        AppLog.Info($"audio monitor default endpoint changed flow={dataFlow} role={role} deviceId=\"{defaultDeviceId}\"");
        if (dataFlow != EDataFlow.Render || role != ERole.Console)
        {
            return;
        }

        EnqueueWork(() =>
        {
            AudioActivityEvent? recordedActivity = null;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                DetachCurrentEndpointLocked();
                _ = TryAttachToDefaultRenderEndpointLocked(allowMissingDefaultEndpoint: true);
                recordedActivity = new AudioActivityEvent(
                    DateTimeOffset.UtcNow,
                    AudioActivityKind.DefaultDeviceChanged,
                    "Default render device changed",
                    SessionInstanceId: null,
                    ProcessId: null,
                    DeviceId: defaultDeviceId,
                    Duration: null);
            }

            RaiseActivityRecorded(recordedActivity);
            RaiseSessionsChanged();
        });
    }

    private void RemoveTrackedSessionLocked(string instanceId, TrackedSession trackedSession)
    {
        _trackedSessions.Remove(instanceId);
        AppLog.Info($"audio monitor removing session instanceId={instanceId} processId={trackedSession.ProcessId} name=\"{trackedSession.DisplayName}\"");

        try
        {
            var sessionControl = (IAudioSessionControl)trackedSession.Control;
            sessionControl.UnregisterAudioSessionNotification(trackedSession.EventSink);
        }
        catch (COMException)
        {
            // The session may already be gone by the time we unsubscribe.
        }
        catch (InvalidComObjectException)
        {
            // The RCW can already be separated during teardown or rapid device churn.
        }
        finally
        {
            TryReleaseComObject(trackedSession.Control);
        }
    }

    private void TeardownLocked()
    {
        AppLog.Info($"audio monitor teardown start trackedSessions={_trackedSessions.Count}");
        DetachCurrentEndpointLocked();

        if (_endpointNotificationsRegistered && _deviceEnumerator is not null)
        {
            try
            {
                _deviceEnumerator.UnregisterEndpointNotificationCallback(_endpointNotificationClient);
            }
            catch (COMException)
            {
            }
        }

        _sessionNotificationsRegistered = false;
        _endpointNotificationsRegistered = false;

        TryReleaseComObject(_deviceEnumerator);

        _deviceEnumerator = null;
        AppLog.Info("audio monitor teardown complete");
    }

    private void DetachCurrentEndpointLocked()
    {
        foreach (var trackedSession in _trackedSessions.Values.ToList())
        {
            RemoveTrackedSessionLocked(trackedSession.InstanceId, trackedSession);
        }

        _trackedSessions.Clear();

        if (_sessionNotificationsRegistered && _sessionManager is not null)
        {
            try
            {
                _sessionManager.UnregisterSessionNotification(_sessionNotificationSink);
            }
            catch (COMException)
            {
            }
            catch (InvalidComObjectException)
            {
            }
        }

        _sessionNotificationsRegistered = false;

        TryReleaseComObject(_sessionManager);
        TryReleaseComObject(_defaultDevice);
        _sessionManager = null;
        _defaultDevice = null;
    }

    private void RaiseSessionsChanged()
    {
        AppLog.Info("audio monitor raising SessionsChanged");
        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseActivityRecorded(AudioActivityEvent? activity)
    {
        if (activity is null)
        {
            return;
        }

        lock (_sync)
        {
            _recentActivities.Add(activity);
            if (_recentActivities.Count > MaxBufferedActivities)
            {
                _recentActivities.RemoveRange(0, _recentActivities.Count - MaxBufferedActivities);
            }
        }

        AppLog.Info(
            $"audio monitor recorded activity kind={activity.Kind} description=\"{activity.Description}\" processId={activity.ProcessId?.ToString() ?? "n/a"} sessionId=\"{activity.SessionInstanceId ?? string.Empty}\" durationMs={(activity.Duration?.TotalMilliseconds.ToString("F0") ?? "n/a")}");
        ActivityRecorded?.Invoke(this, new AudioActivityEventArgs(activity));
    }

    private void RaiseActivityRecorded(IEnumerable<AudioActivityEvent> activities)
    {
        foreach (var activity in activities)
        {
            RaiseActivityRecorded(activity);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void EnqueueWork(Action action)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
        }

        try
        {
            _workQueue.Add(action);
            AppLog.Info($"audio monitor queued action pending={_workQueue.Count}");
        }
        catch (InvalidOperationException)
        {
            AppLog.Warn("audio monitor queue rejected action during shutdown");
        }
    }

    private static IMMDeviceEnumerator CreateDeviceEnumerator()
    {
        return (IMMDeviceEnumerator)Activator.CreateInstance(
            Type.GetTypeFromCLSID(CoreAudioInterop.MMDeviceEnumeratorClsid)!)!;
    }

    private static AudioActivityEvent CreateActivityForTrackedSession(
        TrackedSession trackedSession,
        AudioActivityKind kind,
        DateTimeOffset timestampUtc,
        TimeSpan? duration)
    {
        return new AudioActivityEvent(
            timestampUtc,
            kind,
            trackedSession.DisplayName,
            trackedSession.InstanceId,
            trackedSession.ProcessId,
            DeviceId: null,
            duration);
    }

    private static AudioActivityEvent CreateStoppedActivity(
        TrackedSession trackedSession,
        DateTimeOffset timestampUtc)
    {
        TimeSpan? duration = trackedSession.ActiveSinceUtc is null
            ? null
            : timestampUtc - trackedSession.ActiveSinceUtc.Value;

        return CreateActivityForTrackedSession(
            trackedSession,
            AudioActivityKind.Stopped,
            timestampUtc,
            duration);
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    private const uint COINIT_MULTITHREADED = 0x0;

    private static void TryReleaseComObject(object? instance)
    {
        try
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                Marshal.FinalReleaseComObject(instance);
            }
        }
        catch (InvalidComObjectException)
        {
        }
    }

    private readonly record struct SessionSnapshot(
        uint ProcessId,
        string DisplayName,
        AudioSessionState State);

    private sealed class TrackedSession
    {
        public TrackedSession(
            string instanceId,
            IAudioSessionControl2 control,
            AudioSessionEventsSink eventSink,
            uint processId,
            string displayName,
            AudioSessionState state,
            DateTimeOffset? activeSinceUtc)
        {
            InstanceId = instanceId;
            Control = control;
            EventSink = eventSink;
            ProcessId = processId;
            DisplayName = displayName;
            State = state;
            ActiveSinceUtc = activeSinceUtc;
        }

        public string InstanceId { get; }

        public IAudioSessionControl2 Control { get; }

        public AudioSessionEventsSink EventSink { get; }

        public uint ProcessId { get; }

        public string DisplayName { get; }

        public AudioSessionState State { get; set; }

        public DateTimeOffset? ActiveSinceUtc { get; set; }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class EndpointNotificationClient : IMMNotificationClient
    {
        private readonly Action<EDataFlow, ERole, string> _defaultEndpointChanged;

        public EndpointNotificationClient(Action<EDataFlow, ERole, string> defaultEndpointChanged)
        {
            _defaultEndpointChanged = defaultEndpointChanged;
        }

        public int OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
        {
            _defaultEndpointChanged(flow, role, defaultDeviceId);
            return 0;
        }

        public int OnDeviceAdded(string deviceId) => 0;

        public int OnDeviceRemoved(string deviceId) => 0;

        public int OnDeviceStateChanged(string deviceId, uint newState) => 0;

        public int OnPropertyValueChanged(string deviceId, PROPERTYKEY propertyKey) => 0;
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class AudioSessionNotificationSink : IAudioSessionNotification
    {
        private readonly Action<IAudioSessionControl> _sessionCreated;

        public AudioSessionNotificationSink(Action<IAudioSessionControl> sessionCreated)
        {
            _sessionCreated = sessionCreated;
        }

        public int OnSessionCreated(IAudioSessionControl newSession)
        {
            _sessionCreated(newSession);
            return 0;
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class AudioSessionEventsSink : IAudioSessionEvents
    {
        private readonly string _instanceId;
        private readonly Action<string, AudioSessionState> _stateChanged;
        private readonly Action<string> _sessionDisconnected;

        public AudioSessionEventsSink(
            string instanceId,
            Action<string, AudioSessionState> stateChanged,
            Action<string> sessionDisconnected)
        {
            _instanceId = instanceId;
            _stateChanged = stateChanged;
            _sessionDisconnected = sessionDisconnected;
        }

        public int OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, ref Guid eventContext) => 0;

        public int OnDisplayNameChanged(string newDisplayName, ref Guid eventContext) => 0;

        public int OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext) => 0;

        public int OnIconPathChanged(string newIconPath, ref Guid eventContext) => 0;

        public int OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        {
            _sessionDisconnected(_instanceId);
            return 0;
        }

        public int OnSimpleVolumeChanged(float newVolume, bool newMute, ref Guid eventContext) => 0;

        public int OnStateChanged(AudioSessionState newState)
        {
            _stateChanged(_instanceId, newState);
            return 0;
        }
    }

    private enum SessionDiscoveryKind
    {
        StartupSnapshot,
        CallbackEnumeration,
    }
}
