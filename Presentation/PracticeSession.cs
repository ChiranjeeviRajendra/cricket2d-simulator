using Cricket.Physics;

namespace Cricket2D.Presentation;

public readonly record struct PracticeGuides(bool ShowPitchPosition, bool ShowPredictedTrajectory);

/// <summary>Owns one pending delivery and its seed independently of menus, controls and render rate.</summary>
public sealed class PracticeSession : IDisposable
{
    private readonly DeliveryPreviewWorker _worker;
    private readonly Random _seeds;
    private bool _consumed;
    public PracticeBowlingSettings Settings { get; private set; } = new();
    public PracticeGuides Guides { get; set; } = new(true, true);
    public DeliveryPlan? PendingPlan { get; private set; }
    public string Error { get; private set; } = "";
    public int Seed { get; private set; }
    public bool IsPreparing { get; private set; }
    public bool CanBowl => !_consumed && !IsPreparing && PendingPlan != null;

    public PracticeSession(SimulationSettings physics, int? seed = null)
    {
        _worker = new(physics);
        _seeds = new(seed ?? Random.Shared.Next());
        PrepareNextDelivery();
    }

    public bool Configure(PracticeBowlingSettings settings)
    {
        if (Settings == settings) return false;
        Settings = settings;
        Prepare();
        return true;
    }

    public void OpenNextDelivery()
    {
        if (!_consumed) return;
        PrepareNextDelivery();
    }

    /// <summary>Leaving practice discards overrides and invalidates any in-flight preview.</summary>
    public void Reset()
    {
        Settings = new();
        Guides = new(true, true);
        PrepareNextDelivery();
    }

    private void PrepareNextDelivery()
    {
        _consumed = false;
        int next = _seeds.Next();
        Seed = next == Seed ? (next + 1) % int.MaxValue : next;
        Prepare();
    }

    private void Prepare()
    {
        PendingPlan = null;
        Error = "";
        IsPreparing = true;
        _worker.RequestPractice(Settings, Seed);
    }

    public bool Poll()
    {
        if (!_worker.Poll(out var plan, out string error)) return false;
        PendingPlan = plan;
        Error = error;
        IsPreparing = false;
        return true;
    }

    public bool TryBegin(Simulation simulation, out string error)
    {
        error = IsPreparing ? "Preparing delivery…" : Error;
        if (!CanBowl) return false;
        if (!simulation.BeginPrepared(PendingPlan!, out error)) return false;
        _consumed = true;
        return true;
    }

    public void Dispose() { _worker.Dispose(); PendingPlan = null; IsPreparing = false; }
}
