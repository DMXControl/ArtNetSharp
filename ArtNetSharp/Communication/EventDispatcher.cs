using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ArtNetSharp.Communication;

/// <summary>
/// Raises the events of one <see cref="AbstractInstance"/> (and of its RemoteClients, ports and port configs)
/// outside the network receive loops.
/// <list type="bullet">
/// <item>Ordered: handlers run one after another, in the order the events occurred.</item>
/// <item>Non-blocking: posting only enqueues, so a slow or blocking handler in the application never stalls
/// packet reception, ArtPollReply processing or the DMX send loop. It only delays later events of the same instance.</item>
/// <item>Fail-safe: an exception thrown by a handler is logged and does not affect the library or other events.</item>
/// </list>
/// The queue is processed by an async loop on the thread pool, so idle instances do not hold a thread.
/// </summary>
internal sealed class EventDispatcher : IDisposable
{
    /// <summary>A handler taking longer than this is reported as a warning, as it delays all following events.</summary>
    internal static TimeSpan SlowHandlerThreshold { get; set; } = TimeSpan.FromSeconds(1);

    private readonly Channel<Action> channel = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false, // never run handlers inline on the posting (receive) thread
    });
    private readonly ILogger logger;
    private readonly string name;
    private static readonly ILogger StaticLogger = Logging.CreateLogger<EventDispatcher>();

    /// <summary>
    /// Raises an event via <paramref name="dispatcher"/> if the object belongs to an instance; objects created
    /// standalone (e.g. a RemoteClient built directly by an application) raise synchronously, but still fail-safe.
    /// </summary>
    internal static void Raise(EventDispatcher dispatcher, Action raise)
    {
        if (dispatcher != null)
        {
            dispatcher.Post(raise);
            return;
        }
        try { raise(); }
        catch (Exception e) { StaticLogger.LogError(e, "Event handler threw an exception"); }
    }

    public EventDispatcher(string name, ILogger logger)
    {
        this.name = name;
        this.logger = logger;
        _ = Task.Run(processQueue);
    }

    /// <summary>Queues <paramref name="raise"/>; returns immediately.</summary>
    public void Post(Action raise)
    {
        if (raise != null)
            channel.Writer.TryWrite(raise); // false only after Dispose: events of a disposed instance are dropped
    }

    /// <summary>
    /// Queues <paramref name="raise"/> and completes with its result once it ran on the event queue
    /// (for events whose handlers return data, e.g. an RDM response). The caller awaits without blocking a thread.
    /// </summary>
    public Task<T> InvokeAsync<T>(Func<T> raise)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!channel.Writer.TryWrite(() =>
        {
            try { tcs.TrySetResult(raise()); }
            catch (Exception e) { tcs.TrySetException(e); }
        }))
            tcs.TrySetCanceled();
        return tcs.Task;
    }

    private async Task processQueue()
    {
        var reader = channel.Reader;
        var stopwatch = new Stopwatch();
        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
                while (reader.TryRead(out Action raise))
                {
                    stopwatch.Restart();
                    try
                    {
                        raise();
                    }
                    catch (Exception e)
                    {
                        logger?.LogError(e, $"Event handler of {name} threw an exception");
                    }
                    stopwatch.Stop();
                    if (stopwatch.Elapsed >= SlowHandlerThreshold)
                        logger?.LogWarning($"Event handler of {name} took {stopwatch.Elapsed.TotalMilliseconds:0}ms; following events of this instance were delayed. Avoid blocking work in ArtNetSharp event handlers.");
                }
        }
        catch (Exception e)
        {
            logger?.LogError(e, $"Event queue of {name} stopped");
        }
    }

    public void Dispose() => channel.Writer.TryComplete();
}
