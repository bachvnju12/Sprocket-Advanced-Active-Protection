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

        }

        internal void FixedUpdate()
        {
            if (!APSCore.ConfigEnabled.Value) return;
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

        internal void FireInterceptEffect(APSModuleState module, ThreatTrack threat, bool success)
        {
            if (_disposed) return;
            // Spawn interceptor trail from module to predicted intercept point
            SpawnInterceptorTrail(module, threat, success);
        }

        internal void FireSoftKillEffect(APSModuleState module, ThreatTrack threat)
        {
            if (_disposed) return;
            // Spawn jamming beam / dazzle effect
            SpawnJamEffect(module, threat);
        }

        private Material? _trailMaterial;
        private Material? _jamMaterial;
        private bool _effectWarning;

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

        // Visual + audio of the launch attempt; success explosion comes from the native
        // factory after the projectile is actually released.
        internal void SpawnInterceptorTrail(APSModuleState module, ThreatTrack threat, bool success)
        {
            SpawnBeam(module.Transform.position, threat.Position, Color.yellow, 0.05f, 0.02f, 0.3f, "interceptor trail");
        }

        private void SpawnJamEffect(APSModuleState module, ThreatTrack threat)
        {
            SpawnBeam(module.Transform.position, threat.Position, Color.magenta, 0.1f, 0.05f, 0.5f, "jam effect");
        }

        private void SpawnBeam(Vector3 from, Vector3 to, Color color, float startWidth, float endWidth, float lifetime, string label)
        {
            try
            {
                var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader == null) return;

                // Two cached materials, reused by every shot, destroyed on unload.
                bool jam = color.b > color.r;
                var material = jam ? _jamMaterial : _trailMaterial;
                if (material == null)
                {
                    material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    material.SetColor("_UnlitColor", color);
                    material.SetColor("_Color", color);
                    if (jam)
                    {
                        material.SetColor("_EmissiveColor", color);
                        _jamMaterial = material;
                    }
                    else _trailMaterial = material;
                }

                var go = new GameObject("SAAPS_Effect");
                go.transform.position = from;
                go.hideFlags = HideFlags.HideAndDontSave;

                var line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.SetPosition(0, from);
                line.SetPosition(1, to);
                line.startWidth = startWidth;
                line.endWidth = endWidth;
                line.sharedMaterial = material;

                UnityEngine.Object.Destroy(go, lifetime);
            }
            catch (Exception ex)
            {
                _log.LogError($"[SAAPS] Failed to spawn {label}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_trailMaterial != null) UnityEngine.Object.Destroy(_trailMaterial);
            if (_jamMaterial != null) UnityEngine.Object.Destroy(_jamMaterial);
            _trailMaterial = null;
            _jamMaterial = null;
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