namespace ClearWaters;

// A reversible override of a *blended* value. Never used on source profiles.
// Preserve a game's intervening write instead of restoring a stale value over it.
public sealed class TransientOverride<T> where T : IEquatable<T>
{
    private readonly Func<T> read;
    private readonly Action<T> write;
    private readonly T neutral;
    private T original = default!;
    private bool changed;
    public TransientOverride(Func<T> read, Action<T> write, T neutral) { this.read = read; this.write = write; this.neutral = neutral; }
#if RENDER_DIAGNOSTICS
    public T Original => original;
#endif
    public bool Apply(bool remove)
    {
        original = read();
        changed = remove && !original.Equals(neutral);
        if (changed) write(neutral);
        return changed;
    }
    public void Restore()
    {
        if (changed && read().Equals(neutral)) write(original);
        changed = false;
    }
}
