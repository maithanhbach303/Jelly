namespace MyGame.Levels
{
    /// <summary>
    /// Tracks how far into a TraySequence the shared cursor has advanced.
    /// One cursor, shared across all trays.
    /// </summary>
    public class TraySequenceCursor
    {
        public TraySequence Sequence { get; private set; }
        public int Index { get; private set; }

        public TraySequenceCursor(TraySequence sequence)
        {
            SetSequence(sequence);
        }

        public void SetSequence(TraySequence sequence)
        {
            Sequence = sequence;
            Index = 0;
        }

        public void Reset()
        {
            Index = 0;
        }

        public bool HasNext()
        {
            if (Sequence == null) return false;
            if (Sequence.Count == 0) return false;
            if (Sequence.loop) return true;
            return Index < Sequence.Count;
        }

        public bool TryTake(out TraySequence.CubeSpec spec)
        {
            spec = default;
            if (!HasNext()) return false;

            if (Sequence.loop && Index >= Sequence.Count)
                Index = 0;

            spec = Sequence.Get(Index);
            Index++;
            return true;
        }
    }
}