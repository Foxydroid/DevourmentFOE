using BepInEx;
using DevourmentFOE.Hooks;
using System;

namespace DevourmentFOE
{
    [BepInPlugin("foxydroid_devourmentfoe", "Devourment FOE", "1.0.0")]
    [BepInDependency("devourment", BepInDependency.DependencyFlags.HardDependency)]
    public class DevourmentFOEPlugin : BaseUnityPlugin
    {
        public static BepInEx.Logging.ManualLogSource LogSource;

        private OtherExitHooks _otherExitHooks;

        public void OnEnable()
        {
            LogSource = Logger;
            try
            {
                _otherExitHooks = new OtherExitHooks();
                _otherExitHooks.Apply();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Devourment FOE: Critical error during startup: {ex}");
            }
        }

        public void OnDisable()
        {
            _otherExitHooks?.Dispose();
        }
    }
}
