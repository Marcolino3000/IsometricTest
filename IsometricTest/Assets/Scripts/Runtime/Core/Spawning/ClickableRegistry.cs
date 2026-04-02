using System;
using Runtime.Controls;

namespace Runtime.Core.Spawning
{
    public static class ClickableRegistry
    {
        public static event Action<Clickable> OnClickableSpawned;
        
        public static void RegisterClickable(Clickable clickable)
        {
            OnClickableSpawned?.Invoke(clickable);    
        }
    }
}