using IndoorCO2MapAppV2.ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.PersistentData
{
    public abstract class AutoSaveSettings
    {
        protected void SetProperty<T>(ref T field, T value)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                UserSettings.SaveAsync().SafeFireAndForget("AutoSaveSettings|SetProperty|UserSettings.SaveAsync");
            }
        }
    }
}
