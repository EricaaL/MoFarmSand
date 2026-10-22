using System.Collections.Generic;

/// <summary>Stable ids of the in-play tips. The TipData assets and the code that raises them both use these.</summary>
public static class TipIds
{
    public const string FirstDay = "first_day";
    public const string HoldingSeed = "holding_seed";
    public const string FirstPlant = "first_plant";
    public const string FirstRipe = "first_ripe";
    public const string FirstHarvest = "first_harvest";
    public const string Merchant = "merchant";
    public const string FirstNight = "first_night";
    public const string ShooterHeld = "shooter_held";
    public const string SupportHeld = "support_held";
    public const string FirstDisaster = "first_disaster";
    public const string FirstVoidFall = "first_void_fall";

    public static readonly string[] All =
    {
        FirstDay, HoldingSeed, FirstPlant, FirstRipe, FirstHarvest, Merchant,
        FirstNight, ShooterHeld, SupportHeld, FirstDisaster, FirstVoidFall,
    };
}

/// <summary>
/// Decides which tip shows next: each tip is raised any number of times but shown once ever, in the order raised, one at a time.
/// Pure state; the view owns timing and the storage of <see cref="Seen"/>.
/// </summary>
public sealed class TipBook
{
    private readonly HashSet<string> seen;
    private readonly Queue<string> queue = new Queue<string>();

    public TipBook(IEnumerable<string> alreadySeen = null)
    {
        seen = alreadySeen != null ? new HashSet<string>(alreadySeen) : new HashSet<string>();
    }

    /// <summary>Off means nothing is queued or shown (the Options toggle).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The tip showing now, or null.</summary>
    public string Current { get; private set; }

    public IReadOnlyCollection<string> Seen => seen;

    public bool HasSeen(string id) => seen.Contains(id);

    /// <summary>Queues a tip unless it was already shown, is showing or is queued. Returns true if it was queued.</summary>
    public bool Raise(string id)
    {
        if (!Enabled || string.IsNullOrEmpty(id) || seen.Contains(id) || id == Current || queue.Contains(id)) return false;
        queue.Enqueue(id);
        if (Current == null) Current = queue.Dequeue();
        return true;
    }

    /// <summary>Marks the current tip seen and moves on to the next queued one. Returns true if something changed.</summary>
    public bool Dismiss()
    {
        if (Current == null) return false;
        seen.Add(Current);
        Current = queue.Count > 0 ? queue.Dequeue() : null;
        return true;
    }

    /// <summary>Forgets everything seen and clears the queue (for the next showcase player).</summary>
    public void Reset()
    {
        seen.Clear();
        queue.Clear();
        Current = null;
    }
}
