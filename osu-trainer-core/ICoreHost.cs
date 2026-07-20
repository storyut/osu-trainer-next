using System;

namespace OsuTrainerCore
{
    public interface ICoreHost
    {
        void InvokeOnUiThread(Action action);
        void ShowError(string message);
    }
}
