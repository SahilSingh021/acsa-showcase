using System.Collections.Concurrent;

namespace acsa_web.Services
{
    public sealed class AntiCheatCommandQueue
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<object>> _queues = new();

        public void Enqueue(string userId, object payload)
        {
            var q = _queues.GetOrAdd(userId, _ => new ConcurrentQueue<object>());
            q.Enqueue(payload);
        }

        public bool TryDequeue(string userId, out object? payload)
        {
            payload = null;
            if (!_queues.TryGetValue(userId, out var q)) return false;
            return q.TryDequeue(out payload);
        }

        public int Clear(string userId)
        {
            if (!_queues.TryGetValue(userId, out var q))
                return 0;

            int drained = 0;
            while (q.TryDequeue(out _))
                drained++;

            return drained;
        }
    }
}
