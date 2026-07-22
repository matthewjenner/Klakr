using System.IO.Pipes;

namespace Klakr.App;

/// <summary>
/// Cross-process single-instance guard. A named mutex prevents a second launch from running
/// past <see cref="Program.Main"/>; a named pipe lets the second launch signal the primary
/// to activate (Show + Activate the config window) before it exits.
/// </summary>
/// <remarks>
/// The mutex is session-local (no <c>Global\</c> prefix), so two users on the same machine
/// each get their own primary. If the previous holder crashed without releasing the mutex,
/// <see cref="System.Threading.AbandonedMutexException"/> hands ownership to us and we
/// proceed as primary.
/// </remarks>
internal static class SingleInstance
{
    // Debug builds get suffixed names so a `dotnet run` dev build can coexist with the
    // Velopack-installed release. Both would otherwise fight for the same mutex, and
    // whichever launched second would exit as a duplicate - killing the ability to
    // iterate on the dev build while the user's real installed instance keeps running.
#if DEBUG
    private const string MutexName = "Klakr.App.SingleInstance.Debug";
    private const string PipeName = "Klakr.App.Activation.Debug";
#else
    private const string MutexName = "Klakr.App.SingleInstance";
    private const string PipeName = "Klakr.App.Activation";
#endif
    private const string ShowCommand = "show";

    private static Mutex? _mutex;

    /// <summary>Try to become the primary process. Returns true if we hold the mutex.</summary>
    public static bool TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            if (mutex.WaitOne(TimeSpan.Zero, exitContext: false))
            {
                _mutex = mutex;
                return true;
            }
        }
        catch (AbandonedMutexException)
        {
            // Previous holder crashed without releasing - we implicitly own it now.
            _mutex = mutex;
            return true;
        }

        mutex.Dispose();
        return false;
    }

    /// <summary>Release the mutex. Called from Program.Main's finally.</summary>
    public static void Release()
    {
        if (_mutex is null)
            return;
        try { _mutex.ReleaseMutex(); }
        catch { /* Ownership may have already been released via disposal */ }
        _mutex.Dispose();
        _mutex = null;
    }

    /// <summary>Signal the primary to show its config window. Fire-and-forget; silent on failure.</summary>
    public static void SignalActivate()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ShowCommand);
        }
        catch
        {
            // Primary may be starting up or shutting down; nothing we can do from here.
        }
    }

    /// <summary>
    /// Start listening for activation signals from secondary launches. Runs on a background
    /// task; <paramref name="onActivate"/> is invoked once per received signal. Caller cancels
    /// via <paramref name="ct"/> on shutdown.
    /// </summary>
    public static Task ListenAsync(Action onActivate, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct);

                    using var reader = new StreamReader(server);
                    string? command = await reader.ReadLineAsync(ct);

                    if (command == ShowCommand)
                        onActivate();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Rare pipe error - back off briefly then rebuild the server.
                    try { await Task.Delay(500, ct); } catch { break; }
                }
            }
        }, ct);
    }
}
