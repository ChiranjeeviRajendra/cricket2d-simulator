using System.Diagnostics;
using Cricket.Physics;
using Cricket2D.Presentation;

internal static class PerformanceChecks
{
    public static void PreviewWorker()
    {
        using var worker = new DeliveryPreviewWorker(new());
        worker.Request(new(5, 25, 1.53));
        worker.Poll(out _);
        for (int i = 0; i < 100; i++) worker.Request(new(18 + i % 20, 8 + i % 16, 2.15));
        var latest = new DeliverySettings(32, 16, 2.15);
        worker.Request(latest);
        DeliveryPlan? plan = Wait();
        if (plan?.Settings != latest) throw new InvalidOperationException("A stale slider calculation replaced the latest preview.");
        worker.Request(new(0, 16, 2.15));
        if (Wait() != null) throw new InvalidOperationException("Invalid settings must clear the preview.");
        worker.Request(new(5, 25, 1.53)); worker.Poll(out _);
        worker.Dispose();
        if (worker.Poll(out _)) throw new InvalidOperationException("Disposed preview worker published a result.");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try { DeliverySolver.TrySolve(latest, new(), out _, out _, cancelled.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Solver ignored cancellation.");

        DeliveryPlan? Wait()
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.Elapsed.TotalSeconds < 10)
            {
                if (worker.Poll(out var result)) return result;
                Thread.Sleep(1);
            }
            throw new InvalidOperationException("Preview worker timed out.");
        }
    }

    public static void SteadySimulationAllocations()
    {
        var simulation = new Simulation(new());
        if (!simulation.Begin(new(32, 16, 2.15), out var error)) throw new InvalidOperationException(error);
        for (int i = 0; i < 2000; i++) simulation.Tick(default);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 2000; i++) simulation.Tick(new(.5, -.5, .3, -.2));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated > 4096) throw new InvalidOperationException($"Steady physics allocated {allocated} bytes.");
        if (simulation.Trail.Count > 320) throw new InvalidOperationException("Ball trail grows without a bound.");
    }
}
