static class ThreadSafeRandom
{
    [ThreadStatic]
    static Random? _local;
    static readonly Random Global = new();

    public static Random Instance
    {
        get
        {
            if (_local is null)
            {
                int seed;
                lock (Global)
                {
                    seed = Global.Next();
                }

                _local = new Random(seed);
            }

            return _local;
        }
    }
}