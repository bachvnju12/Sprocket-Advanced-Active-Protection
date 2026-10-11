using SprocketAdvancedAPS.Core;
using SprocketAdvancedAPS.Systems;
using UnityEngine;

namespace SprocketAdvancedAPS;

internal sealed class APSDriver : MonoBehaviour
{
    internal static APSDriver? Instance { get; private set; }
    private bool _disposed;
    private float _nextError;
    public APSDriver(IntPtr ptr) : base(ptr) => Instance = this;

    public void LateUpdate()
    {
        if (_disposed) return;
        try
        {
            APSCore.Instance?.Update();
            APSSystems.Instance?.Update();
        }
        catch (Exception ex) { Report(ex); }
    }

    public void FixedUpdate()
    {
        if (_disposed) return;
        try { APSSystems.Instance?.FixedUpdate(); }
        catch (Exception ex) { Report(ex); }
    }

    private void Report(Exception ex)
    {
        if (_disposed || Time.unscaledTime < _nextError) return;
        _nextError = Time.unscaledTime + 5f;
        Main.ModLog?.LogError("[SAAPS] Driver update failed: " + ex);
    }

    public void OnDestroy() => Dispose(false);
    internal void Dispose() => Dispose(true);
    private void Dispose(bool destroy)
    {
        if (_disposed) return;
        _disposed = true;
        if (ReferenceEquals(Instance, this)) Instance = null;
        if (destroy && this != null) { enabled = false; Destroy(this); }
    }
}
