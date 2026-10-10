using System;
using BepInEx.Logging;
using SprocketAdvancedAPS.Core;
using SprocketAdvancedAPS.Systems;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AttachedBehaviours;
using UnityEngine;

namespace SprocketAdvancedAPS
{
    internal sealed class APSDriver : MonoBehaviour
    {
        internal static APSDriver? Instance { get; private set; }
        public APSDriver(IntPtr ptr) : base(ptr) => Instance = this;

        public void LateUpdate()
        {
            try
            {
                APSCore.Instance?.Update();
                APSSystems.Instance?.Update();
            }
            catch (Exception ex) { Main.ModLog.LogError("[SAAPS] Update failed: " + ex); }
        }

        public void FixedUpdate()
        {
            try { APSSystems.Instance?.FixedUpdate(); }
            catch (Exception ex) { Main.ModLog.LogError("[SAAPS] Fixed update failed: " + ex); }
        }

        public void OnDestroy() => Dispose();
        internal void Dispose() => Instance = null;
    }
}

namespace SprocketAdvancedAPS.UI
{
    internal sealed class APSDesignerUI : IDisposable
    {
        internal static APSDesignerUI? Instance { get; private set; }
        internal APSDesignerUI(ManualLogSource log) => Instance = this;
        internal void LateUpdate() { }
        internal void OnGUI() { }
        public void Dispose() => Instance = null;
    }

    internal sealed class APSHud : IDisposable
    {
        internal static APSHud? Instance { get; private set; }
        internal APSHud(ManualLogSource log) => Instance = this;
        internal void LateUpdate() { }
        internal void OnGUI() { }
        internal void ToggleVisibility() { }
        public void Dispose() => Instance = null;
    }
}
