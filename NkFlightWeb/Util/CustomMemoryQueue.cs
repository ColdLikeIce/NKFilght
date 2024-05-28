namespace NkFlightWeb.Util
{
    public class CustomMemoryQueue<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly object _lockObject = new object();

        // 入队操作
        public void Enqueue(T item)
        {
            lock (_lockObject)
            {
                _queue.Enqueue(item);
                Monitor.PulseAll(_lockObject); // 唤醒等待的线程
            }
        }

        // 出队操作（阻塞）
        public T Dequeue()
        {
            lock (_lockObject)
            {
                while (_queue.Count == 0)
                {
                    Monitor.Wait(_lockObject); // 等待入队操作
                }
                return _queue.Dequeue();
            }
        }

        // 尝试出队操作（非阻塞）
        public bool TryDequeue(out T item)
        {
            lock (_lockObject)
            {
                if (_queue.Count > 0)
                {
                    item = _queue.Dequeue();
                    return true;
                }
                else
                {
                    item = default;
                    return false;
                }
            }
        }

        // 获取队列长度
        public int Count
        {
            get
            {
                lock (_lockObject)
                {
                    return _queue.Count;
                }
            }
        }
    }
}