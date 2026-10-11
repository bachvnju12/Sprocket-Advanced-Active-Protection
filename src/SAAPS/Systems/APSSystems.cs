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

        private float _errorLogWindowEnd;
        private int _errorCount;

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
                        ThrottledError("frame", ex);
                    }
                }
            }
            APSVisualManager.Instance?.Update();
            APSAudioManager.Instance?.Update();
        }

        /// Repeated failures are summarized at most once per window: an exception thrown every
        /// frame by every vehicle would otherwise flood the log and the disk.
        private void ThrottledError(string context, Exception ex)
        {
            if (Time.unscaledTime < _errorLogWindowEnd)
            {
                _errorCount++;
                return;
            }
            if (_errorCount > 0) _log.LogError($"[SAAPS] {context} update error repeated {_errorCount} more time(s), last: {ex.Message}");
            else _log.LogError($"[SAAPS] {context} update error: {ex.Message}");
            _errorCount = 0;
            _errorLogWindowEnd = Time.unscaledTime + 5f;
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
            if (APSCore.Instance?.VehicleStates.Values.Any(state => state.IsActive) != true)
            {
                ProjectileTracker.Snapshot.Clear();
                APSSoftKillSystem.Instance?.FixedUpdate();
                return;
            }
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
                        ThrottledError("fixed", ex);
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
            var errors = new List<Exception>();
            foreach (var cleanup in new Action[] { () => APSSoftKillSystem.Instance?.Dispose(),
                () => APSHardKillSystem.Instance?.Dispose(), () => APSVisualManager.Instance?.Dispose(),
                () => APSAudioManager.Instance?.Dispose(), ProjectileTracker.Clear })
                try { cleanup(); } catch (Exception ex) { errors.Add(ex); }
            if (errors.Count != 0) throw new AggregateException(errors);
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

        private sealed record Tracer(Sprocket.DamageModelling.OnDemandProjectileEngineObjectFactory Factory,
            Transform Transform, Sprocket.Vehicles.Vehicle Vehicle, Vector3 From, Vector3 To, float Born, float Duration);
        private readonly List<Tracer> _tracers = new();
        private Sprocket.DamageModelling.ProjectileDefinition? _tracerDefinition;
        private bool _trailWarning;
        private bool _tracerFailed;
        private bool _effectWarning;

        internal void FireInterceptEffect(APSModuleState module, ThreatTrack threat, bool success)
        {
            if (_disposed || _tracerFailed || _tracers.Count >= 64) return;
            Transform? visual = null;
            Sprocket.DamageModelling.OnDemandProjectileEngineObjectFactory? factory = null;
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
                _tracers.Add(new Tracer(factory, visual, module.Vehicle, from, threat.Position, Time.time,
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

        private void ReleaseTracer(Sprocket.DamageModelling.OnDemandProjectileEngineObjectFactory factory, Transform visual)
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
                    if (tracer.Transform == null || tracer.Vehicle == null || tracer.Vehicle.Released || age >= tracer.Duration + .05f)
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
                if (tracer.Transform != null) ReleaseTracer(tracer.Factory, tracer.Transform);
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
        private readonly Dictionary<string, AudioClip> _clips = new();
        private readonly List<(GameObject Object, float Until)> _voices = new();
        private readonly HashSet<string> _warnings = new();
        // Profiles whose clip could not be built. Latched so a broken profile costs one
        // decode attempt, not a file read plus resample on every single shot.
        private readonly HashSet<string> _clipFailed = new();
        private bool _disposed;

        internal APSAudioManager(ManualLogSource log)
        {
            _log = log;
        }

        internal void PlayLaunchSound(APSModuleState module)
        {
            if (_disposed || module.Definition.Type == APSType.SoftKill) return;
            string profile = module.Definition.LaunchSound switch
            {
                "audio/rocket.wav" or "Audio/drozd_launch" or "Audio/drozd2_launch" or "Audio/afganit_launch" => "rocket",
                "audio/launcher.wav" or "Audio/trophy_launch" => "launcher",
                _ => "shot"
            };
            PlayAtPosition(module.Transform.position, profile, APSCore.ConfigLaunchVolume.Value, UnityEngine.Random.Range(.96f, 1.04f));
        }

        // No invented firing/radar noises for silent soft-kill emitters.
        internal void PlayJamSound(APSModuleState module) { }
        internal void PlayRadarPing(APSModuleState module) { }
        internal void PlayWarningTone(Vector3 position) { }

        internal void Update()
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (_voices[i].Object == null || Time.unscaledTime >= _voices[i].Until)
                {
                    if (_voices[i].Object != null) UnityEngine.Object.Destroy(_voices[i].Object);
                    _voices.RemoveAt(i);
                }
        }

        private void PlayAtPosition(Vector3 position, string profile, float volume, float pitch)
        {
            GameObject? go = null;
            try
            {
                Update();
                if (_voices.Count >= 24 || !float.IsFinite(volume) || volume <= 0) return;
                if (_clipFailed.Contains(profile)) return;
                if (!_clips.TryGetValue(profile, out var clip))
                {
                    int rate = 44100;
                    float[] samples;
                    try
                    {
                        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Main).Assembly.Location) ?? "", "SprocketAdvancedAPS", "audio", profile + ".wav");
                        using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                        if (stream.Length > APSWave.MaxBytes) throw new System.IO.InvalidDataException("WAV exceeds 1 MiB.");
                        using var reader = new System.IO.BinaryReader(stream);
                        byte[] bytes = reader.ReadBytes(checked((int)stream.Length));
                        if (stream.ReadByte() != -1) throw new System.IO.InvalidDataException("WAV changed while reading.");
                        (rate, samples) = APSWave.Decode(bytes);
                    }
                    catch (Exception ex)
                    {
                        if (_warnings.Add(profile)) _log.LogWarning($"[SAAPS] {profile} WAV unavailable; original synthetic shot fallback: {ex.Message}");
                        samples = APSWave.Synthesize(profile);
                    }
                    try
                    {
                        clip = AudioClip.Create("SAAPS_" + profile, samples.Length, 1, rate, false);
                        if (clip == null || !clip.SetData(samples, 0)) throw new InvalidOperationException("AudioClip creation or SetData failed.");
                        _clips.Add(profile, clip);
                    }
                    catch { if (clip != null) UnityEngine.Object.Destroy(clip); _clipFailed.Add(profile); throw; }
                }
                go = new GameObject("SAAPS_Audio");
                go.transform.position = position;
                go.hideFlags = HideFlags.HideAndDontSave;
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.clip = clip;
                source.volume = Mathf.Clamp01(volume);
                source.pitch = pitch;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 3f;
                source.maxDistance = 150f;
                source.dopplerLevel = 0;
                source.Play();
                float lifetime = clip.length / pitch + .15f;
                _voices.Add((go, Time.unscaledTime + lifetime));
                UnityEngine.Object.Destroy(go, lifetime);
            }
            catch (Exception ex)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                if (_warnings.Add("play")) _log.LogWarning($"[SAAPS] Audio failed; interception unaffected: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _disposed = true;
            foreach (var voice in _voices) if (voice.Object != null) UnityEngine.Object.Destroy(voice.Object);
            foreach (var clip in _clips.Values) if (clip != null) UnityEngine.Object.Destroy(clip);
            _voices.Clear(); _clips.Clear(); _warnings.Clear(); _clipFailed.Clear();
            Instance = null;
        }
    }
}