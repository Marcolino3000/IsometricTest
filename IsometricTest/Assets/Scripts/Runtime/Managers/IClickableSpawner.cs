using System;
using Runtime.Controls;

namespace Runtime
{
    public interface IClickableSpawner
    {
        public event Action<IClickable> OnClickableSpawned;
    }
}