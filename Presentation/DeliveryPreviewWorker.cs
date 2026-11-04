using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>One cancellable solver job; rapid slider edits replace pending work instead of queuing it.</summary>
public sealed class DeliveryPreviewWorker(SimulationSettings settings) : IDisposable
{
    private DeliverySettings? _requested, _pending;
    private Task<(long Revision, DeliveryPlan? Plan)>? _work;
    private CancellationTokenSource? _cancellation;
    private long _revision;
    private bool _disposed;

    public void Request(DeliverySettings request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_requested == request) return;
        _requested = _pending = request;
        _revision++;
        _cancellation?.Cancel();
    }

    /// <summary>Call on the UI thread. Only a result for the latest request may be published.</summary>
    public bool Poll(out DeliveryPlan? plan)
    {
        plan = null;
        if (_disposed) return false;
        bool ready = false;
        if (_work is { IsCompleted: true })
        {
            try
            {
                var result = _work.GetAwaiter().GetResult();
                if (result.Revision == _revision) { plan = result.Plan; ready = true; }
            }
            catch (OperationCanceledException) { }
            _work = null;
            _cancellation!.Dispose(); _cancellation = null;
        }
        if (_work == null && _pending is DeliverySettings request)
        {
            _pending = null;
            long revision = _revision;
            _cancellation = new();
            CancellationToken token = _cancellation.Token;
            _work = Task.Run(() =>
            {
                DeliverySolver.TrySolve(request, settings, out var result, out _, token);
                return (revision, result);
            }, token);
        }
        return ready;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _pending = null;
        _cancellation?.Cancel(); _cancellation?.Dispose();
    }
}
