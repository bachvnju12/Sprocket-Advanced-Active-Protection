using System;
using BepInEx.Logging;
using SprocketAdvancedAPS.Core;
using UnityEngine;

namespace SprocketAdvancedAPS.Systems
{
    /// <summary>
    /// Aggregates all APS subsystems and drives them each frame.
    /// </summary>
    internal sealed class APSSystems
    {
        internal static APSSystems? Instance { get; private set; }
        private readonly ManualLogSource _log;

        private long _profileTicks;
        private long _profileReads;
        private int _profileSamples;
        private float _nextProfileLog;

        internal APSSystems(ManualLogSource log)
        {
            Instance = this;
            _log = log;

            // Initialize singleton systems
            APSHardKillSystem hardKill = new APSHardKillSystem(log);
            APSSoftKillSystem softKill = new APSSoftKillSystem(log);
            APSVisualManager.Instance = new APSVisualManager(log);
            APSAudioManager.Instance = new APSAudioManager(log);

            _log.LogInfo("[SAAPS] APS subsystems initialized.");
        }

        internal void Update()
        {
            // Update all vehicle states
            if (APSCore.Instance != null)
            {
                foreach (var kvp in APSCore.Instance.VehicleStates)
                {
                    try
                    {
                        kvp.Value.Update();
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[SAAPS] Vehicle state update error: {ex.Message}");
                    }
                }
            }
            APSVisualManager.Instance?.Update();
        }

        internal void FixedUpdate()
        {
            if (!APSCore.ConfigEnabled.Value)
            {
                APSSoftKillSystem.Instance?.FixedUpdate();
                return;
            }
            bool profile = APSCore.ConfigPerformanceLog.Value;
            long start = profile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            ProjectileTracker.Capture();

            if (APSCore.Instance != null)
            {
                foreach (var kvp in APSCore.Instance.VehicleStates)
                {
                    try
                    {
                        kvp.Value.FixedUpdate();
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"[SAAPS] Vehicle fixed update error: {ex.Message}");
                    }
                }
            }
            // After the vehicle pass, so a jam dispatched this tick uses the refreshed position
            // and takes effect immediately rather than one physics tick late.
            APSSoftKillSystem.Instance?.FixedUpdate();
            if (profile) ReportPerformance(start);
        }

        private void ReportPerformance(long start)
        {
            _profileTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            _profileReads += ProjectileTracker.NativeReads;
            _profileSamples++;
            if (Time.time < _nextProfileLog) return;
            _nextProfileLog = Time.time + 5f;
            double milliseconds = _profileTicks * 1000d / System.Diagnostics.Stopwatch.Frequency / _profileSamples;
            _log.LogInfo($"[SAAPS] PERF physicsMean={milliseconds:F3}ms snapshotReadsMean={_profileReads / (double)_profileSamples:F1} samples={_profileSamples}");
            _profileTicks = 0; _profileReads = 0; _profileSamples = 0;
        }

        internal void Shutdown()
        {
            APSSoftKillSystem.Instance?.Dispose();
            APSHardKillSystem.Instance?.Dispose();
            APSVisualManager.Instance?.Dispose();
            APSAudioManager.Instance?.Dispose();
            ProjectileTracker.Clear();
            Instance = null;
        }
    }

    /// <summary>
    /// Visual manager: handles intercept effects, muzzle flashes,
    /// radar visualization, and coverage visualization in the editor.
    /// </summary>
    internal sealed class APSVisualManager : IDisposable
    {
        internal static APSVisualManager? Instance { get; set; }
        private readonly ManualLogSource _log;
        private bool _disposed;

        internal APSVisualManager(ManualLogSource log)
        {
            _log = log;
        }

        private sealed record Tracer(Sprocket.DamageModelling.IProjectileEngineObjectFactory Factory,
            Transform Transform, Vector3 From, Vector3 To, float Born, float Duration);
        private readonly List<Tracer> _tracers = new();
        private Sprocket.DamageModelling.ProjectileDefinition? _tracerDefinition;
        private bool _trailWarning;
        private bool _tracerFailed;
        private bool _effectWarning;

        internal void FireInterceptEffect(APSModuleState module, ThreatTrack threat, bool success)
        {
            if (_disposed || _tracerFailed || _tracers.Count >= 64) return;
            Transform? visual = null;
            Sprocket.DamageModelling.IProjectileEngineObjectFactory? factory = null;
            try
            {
                var prototype = UnityEngine.Object.FindObjectOfType<Sprocket.DamageModelling.ProjectilesMain>()?.ProjectilePrototype;
                if (prototype == null) throw new InvalidOperationException("Native shell visual prototype unavailable");
                if (prototype.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    prototype.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Native shell prototype has physics components; tracer disabled");
                // Private on-demand factory: never borrow the incoming ATGM's visual or shell pool.
                factory = new Sprocket.DamageModelling.OnDemandProjectileEngineObjectFactory(prototype);
                // Native projectile visual only: no Launch, register entry or damage functions.
                _tracerDefinition ??= new Sprocket.DamageModelling.ProjectileDefinition("SAAPS visual interceptor", default,
                    default, .1f, .04f, .16f, 0f, 1f,
                    new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprocket.DamageModelling.ProjectileFunction>(0),
                    new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprocket.DamageModelling.ProjectileFunction>(0));
                visual = factory.Get(_tracerDefinition);
                if (visual == null) throw new InvalidOperationException("Native projectile visual unavailable");
                if (visual.GetComponentsInChildren<Collider>(true).Length != 0 ||
                    visual.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Native visual has physics components; tracer disabled");
                var from = module.Transform.position;
                var direction = threat.Position - from;
                visual.position = from;
                if (direction.sqrMagnitude > 1e-6f) visual.rotation = Quaternion.LookRotation(direction);
                foreach (var trail in visual.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
                _tracers.Add(new Tracer(factory, visual, from, threat.Position, Time.time,
                    Mathf.Clamp(direction.magnitude / 1000f, .025f, .08f)));
            }
            catch (Exception ex)
            {
                if (visual != null && factory != null) ReleaseTracer(factory, visual);
                _tracerFailed = true;
                if (_trailWarning) return;
                _trailWarning = true;
                _log.LogWarning($"[SAAPS] Native interceptor tracer unavailable; interception unaffected: {ex.Message}");
            }
        }

        private void ReleaseTracer(Sprocket.DamageModelling.IProjectileEngineObjectFactory factory, Transform visual)
        {
            try { factory.Release(visual); }
            catch (Exception ex)
            {
                _tracerFailed = true;
                // Only instances from our private on-demand factory; never the native prototype.
                try { if (visual != null) UnityEngine.Object.Destroy(visual.gameObject); } catch { }
                if (!_trailWarning) _log.LogWarning($"[SAAPS] Native tracer release failed; tracer disabled: {ex.Message}");
                _trailWarning = true;
            }
        }

        internal void Update()
        {
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var tracer = _tracers[i];
                float age = Time.time - tracer.Born;
                try
                {
                    if (tracer.Transform == null || age >= tracer.Duration + .05f)
                    {
                        if (tracer.Transform != null) ReleaseTracer(tracer.Factory, tracer.Transform);
                        _tracers.RemoveAt(i);
                        continue;
                    }
                    tracer.Transform.position = Vector3.Lerp(tracer.From, tracer.To, Mathf.Clamp01(age / tracer.Duration));
                }
                catch (Exception ex)
                {
                    _tracers.RemoveAt(i);
                    if (tracer.Transform != null) ReleaseTracer(tracer.Factory, tracer.Transform);
                    _tracerFailed = true;
                    if (!_trailWarning) _log.LogWarning($"[SAAPS] Native tracer update failed: {ex.Message}");
                    _trailWarning = true;
                }
            }
        }

        internal void SpawnInterceptExplosion(Sprocket.DamageModelling.ProjectileRegister register, Vector3 position, Vector3 velocity)
        {
            if (_disposed) return;
            try
            {
                // Same visual-only factory used by native shell effects; no blast damage or spall.
                var factory = register.Context?.ShellImpactMarkEffectFactory;
                if (factory == null) throw new InvalidOperationException("Native projectile effect factory unavailable");
                factory.PlayEffect(new Sprocket.DamageModelling.ProjectileEffectInfo
                {
                    Type = Sprocket.DamageModelling.ProjectileEffectType.Explosion,
                    Position = position,
                    HitNormal = -velocity.normalized,
                    HitVelocity = velocity,
                    // APS interceptor burst, not the incoming round: native explosion scale follows calibre.
                    Calibre = 40
                });
            }
            catch (Exception ex)
            {
                if (_effectWarning) return;
                _effectWarning = true;
                _log.LogWarning($"[SAAPS] Native intercept visual unavailable; projectile release unaffected: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (var tracer in _tracers)
            {
                if (tracer.Transform == null) continue;
                ReleaseTracer(tracer.Factory, tracer.Transform);
            }
            _tracers.Clear();
            _tracerDefinition = null;
            Instance = null;
        }
    }

    /// <summary>
    /// Audio manager: handles APS launch sounds, radar pings,
    /// and warning tones.
    /// </summary>
    internal sealed class APSAudioManager : IDisposable
    {
        internal static APSAudioManager? Instance { get; set; }
        private readonly ManualLogSource _log;
        private AudioClip? _launchClip;
        private AudioClip? _jamClip;
        private AudioClip? _radarClip;
        private AudioClip? _warningClip;
        private bool _disposed;

        internal APSAudioManager(ManualLogSource log)
        {
            _log = log;
        }

        internal void PlayLaunchSound(APSModuleState module)
        {
            if (_disposed) return;
            PlayAtPosition(module.Transform.position, ref _launchClip, "Audio/drozd_launch", 1f, 0.8f);
        }

        internal void PlayJamSound(APSModuleState module)
        {
            if (_disposed) return;
            PlayAtPosition(module.Transform.position, ref _jamClip, "Audio/shtora_jam", 0.7f, 0.5f);
        }

        internal void PlayRadarPing(APSModuleState module)
        {
            if (_disposed) return;
            PlayAtPosition(module.Transform.position, ref _radarClip, "Audio/drozd_radar", 0.3f, 0.3f);
        }

        internal void PlayWarningTone(Vector3 position)
        {
            if (_disposed) return;
            PlayAtPosition(position, ref _warningClip, "Audio/warning", 0.5f, 0.6f);
        }

        private void PlayAtPosition(Vector3 position, ref AudioClip? clip, string path, float volume, float pitch)
        {
            try
            {
                if (clip == null)
                {
                    clip = LoadClip(path);
                }

                if (clip == null)
                {
                    // Fallback: generate a simple tone procedurally
                    clip = GenerateTone(800f, 0.2f, volume);
                }

                var go = new GameObject("SAAPS_Audio");
                go.transform.position = position;
                go.hideFlags = HideFlags.HideAndDontSave;

                var source = go.AddComponent<AudioSource>();
                source.clip = clip;
                source.volume = volume;
                source.pitch = pitch;
                source.spatialBlend = 1f;
                source.Play();

                UnityEngine.Object.Destroy(go, clip.length + 0.1f);
            }
            catch (Exception ex)
            {
                _log.LogError($"[SAAPS] Audio play failed: {ex.Message}");
            }
        }

        private AudioClip? LoadClip(string path)
        {
            // Try to load from mod assets
            try
            {
                var fullPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(SprocketAdvancedAPS.Main).Assembly.Location) ?? "",
                    path);

                if (System.IO.File.Exists(fullPath))
                {
                    var bytes = System.IO.File.ReadAllBytes(fullPath);
                    return AudioClip.Create(path, 44100, 1, 44100, false);
                }
            }
            catch
            {
                // Fall through to procedural
            }
            return null;
        }

        private AudioClip GenerateTone(float frequency, float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume;
            }

            var clip = AudioClip.Create("SAAPS_Tone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (var clip in new[] { _launchClip, _jamClip, _radarClip, _warningClip })
                if (clip != null) UnityEngine.Object.Destroy(clip);
            _launchClip = null;
            _jamClip = null;
            _radarClip = null;
            _warningClip = null;
            Instance = null;
        }
    }
}