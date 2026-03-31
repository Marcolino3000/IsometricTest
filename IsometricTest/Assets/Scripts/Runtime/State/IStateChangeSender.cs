using System;

namespace Runtime
{
    public interface IStateChangeSender<T> where T : State
    {
        public event Action<T> OnStateChange;
    }
}