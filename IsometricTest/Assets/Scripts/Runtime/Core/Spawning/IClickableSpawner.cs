using System;
using Runtime.Gameplay.Controls;

namespace Runtime.Core.Spawning
{
    public interface IClickableSpawner
    {
        public event Action<IClickable> OnClickableSpawned;
    }
}