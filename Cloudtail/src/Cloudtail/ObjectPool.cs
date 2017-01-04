using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cloudtail
{
    public class ObjectPool<T>
    {
        private readonly Func<T> factoryFunc;
        private int _Capacity = 5;

        private readonly Stack<T> pool = new Stack<T>();

        public int Capacity
        {
            get { return _Capacity; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _Capacity = value;
                TrimExcess();
            }
        }

        public ObjectPool(Func<T> factoryFunc)
        {
            if (factoryFunc == null) throw new ArgumentNullException(nameof(factoryFunc));
            this.factoryFunc = factoryFunc;
        }

        private void TrimExcess()
        {
            lock (pool)
            {
                while (pool.Count > _Capacity) pool.Pop();
            }
        }

        public T Create()
        {
            return factoryFunc();
        }

        public T Get()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                    return pool.Pop();
            }
            return factoryFunc();
        }

        public bool Put(T obj)
        {
            lock (pool)
            {
                if (pool.Count >= _Capacity)
                    return false;
                pool.Push(obj);
            }
            return true;
        }

        public void Clear()
        {
            lock (pool) pool.Clear();
        }
    }
}
