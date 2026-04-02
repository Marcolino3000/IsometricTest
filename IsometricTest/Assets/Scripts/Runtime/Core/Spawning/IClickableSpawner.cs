using System;
using Runtime.Controls;

namespace Runtime.Core.Spawning
{
    public interface IClickableSpawner
    {
        public event Action<IClickable> OnClickableSpawned;
    }
}