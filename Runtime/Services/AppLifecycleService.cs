using System;
using Object = UnityEngine.Object;

namespace AbyssMoth
{
    public sealed class AppLifecycleService
    {
        public event Action<bool, Object> FocusChanged;
        public event Action<bool, Object> PauseChanged;
        public event Action<Object> Quit;

        public bool IsFocused { get; private set; } = true;
        public bool IsPausedBySystem { get; private set; }

        public void RaiseFocusChanged(bool hasFocus, Object sender)
        {
            if (IsFocused == hasFocus)
                return;

            IsFocused = hasFocus;
            FocusChanged?.Invoke(hasFocus, sender);
        }

        public void RaisePauseChanged(bool isPaused, Object sender)
        {
            if (IsPausedBySystem == isPaused)
                return;

            IsPausedBySystem = isPaused;
            PauseChanged?.Invoke(isPaused, sender);
        }

        public void RaiseQuit(Object sender) =>
            Quit?.Invoke(sender);
    }
}