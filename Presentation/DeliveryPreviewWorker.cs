using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>One cancellable solver job; rapid slider edits replace pending work instead of queuing it.</summary>
public sealed class DeliveryPreviewWorker(SimulationSettings settings) : IDisposable
{
    private readonly record struct RequestData(DeliverySettings? Manual, PracticeBowlingSettings? Practice, int Seed);
    private RequestData? _requested, _pending;
    private Task<(long Revision, DeliveryPlan? Plan, string Error)>? _work;
    private CancellationTokenSource? _cancellation;
    private long _revision;
    private bool _disposed;

    public void Request(DeliverySettings request) => Enqueue(new(request, null, 0));
    public void RequestPractice(PracticeBowlingSettings request, int seed) => Enqueue(new(null, request, seed));

    private void Enqueue(RequestData request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_requested == request) return;
        _requested = _pending = request;
        _revision++;
        _cancellation?.Cancel();
    }

    /// <summary>Call on the UI thread. Only a result for the latest request may be published.</summary>
    public bool Poll(out DeliveryPlan? plan) => Poll(out plan, out _);

    public bool Poll(out DeliveryPlan? plan, out string error)
    {
        plan = null;
        error = "";
        if (_disposed) return false;
        bool ready = false;
        if (_work is { IsCompleted: true })
        {
            try
            {
                var result = _work.GetAwaiter().GetResult();
                if (result.Revision == _revision) { plan = result.Plan; error = result.Error; ready = true; }
            }
            catch (OperationCanceledException) { }
            _work = null;
            _cancellation!.Dispose(); _cancellation = null;
        }
        if (_work == null && _pending is RequestData request)
        {
            _pending = null;
            long revision = _revision;
            _cancellation = new();
            CancellationToken token = _cancellation.Token;
            _work = Task.Run(() =>
            {
                DeliveryPlan? result;
                string error;
                if (request.Manual is DeliverySettings manual)
                    DeliverySolver.TrySolve(manual, settings, out result, out error, token);
                else PracticeDeliveryGenerator.TryCreatePlan(request.Practice!, request.Seed, settings, out result, out error, token);
                return (revision, result, error);
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
