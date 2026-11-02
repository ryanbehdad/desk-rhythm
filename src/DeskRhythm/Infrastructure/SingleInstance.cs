namespace DeskRhythm.Infrastructure;

/// <summary>
/// One running copy per user session. A second launch signals the first one
/// (which then shows its status panel) and exits.
/// </summary>
internal static class SingleInstance
{
    private const string Id = "DeskRhythm-5c1f0e7a-3b0d-4d8e-9a55-0f6a9f3b2c11";

    private static Mutex? _mutex;
    private static EventWaitHandle? _signal;
    private static RegisteredWaitHandle? _registration;

    public static bool TryAcquire()
    {
        // Create the event before the mutex check, so a fast second launch can always find it.
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{Id}-show");
        _mutex = new Mutex(true, $@"Local\{Id}-mutex", out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _signal.Set();
        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void ListenForSecondLaunch(Action onSecondLaunch)
    {
        if (_signal is null)
        {
            return;
        }

        _registration = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onSecondLaunch(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public static void Release()
    {
        _registration?.Unregister(null);
        _signal?.Dispose();
        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _mutex.Dispose();
        }
    }
}
