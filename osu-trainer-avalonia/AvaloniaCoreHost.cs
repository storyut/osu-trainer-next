using System;
using Avalonia.Threading;
using OsuTrainerCore;

namespace osu_trainer_avalonia
{
    public class AvaloniaCoreHost : ICoreHost
    {
        private readonly Action<string> onError;

        public AvaloniaCoreHost(Action<string> onError)
        {
            this.onError = onError;
        }

        public void InvokeOnUiThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        public void ShowError(string message) => InvokeOnUiThread(() => onError(message));
    }
}
