using SprocketAdvancedAPS.Core;
using SprocketAdvancedAPS.Systems;
using Sprocket.DamageModelling;
using UnityEngine;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
APSDefinitions.LoadAll();
Check(APSDefinitions.All["arena"].InterceptProbability[ThreatClass.CannonHE] == APSDefinitions.All["arena"].InterceptProbability[ThreatClass.HE], "Built-in HE tuning preserved for CannonHE.");
Check(APSDefinitions.All["afganit"].InterceptProbability[ThreatClass.CannonHE] == .60f, "Explicit CannonHE tuning not overwritten.");
var log = new BepInEx.Logging.ManualLogSource();
var hardKill = new APSHardKillSystem(log);
var softKill = new APSSoftKillSystem(log);
(APSVehicleState State, APSModuleState Module, ThreatTrack Threat, ProjectileRegister Register) Setup(APSType type, float intercept, float jam)
{
    softKill.Dispose();
    softKill = new APSSoftKillSystem(log);
    ProjectileTracker.Clear();
    Time.time = 10;
    var state = new APSVehicleState();
    var module = new APSModuleState(state, new Sprocket.Vehicles.VehicleComponent(), new APSDefinition
    {
        Type = type, CustomSettings = new APSCustomSettings(), CountermeasureCapacity = 2,
        EngagementRangeMin = 2, EngagementRangeMax = 30, DetectionRange = 100,
        EngagementFOVHorizontal = 120, EngagementFOVVertical = 60,
        ReloadTime = 1, CooldownBetweenShots = .5f,
        InterceptProbability = new() { [ThreatClass.ATGM_SACLOS] = intercept, [ThreatClass.CannonHE] = 0 },
        SoftKillProbability = new() { [ThreatClass.ATGM_SACLOS] = jam }
    });
    state.Modules.Add(module);
    var register = new ProjectileRegister();
    register.pool.Add(new() { ID = 1, flags = 1, position = new(0, 0, 10), velocity = new(0, 0, -100), Definition = new("high explosive frag") });
    register.activeIdMap.Add(1, 0);
    typeof(ProjectileTracker).GetMethod("OnFixedUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, new object[] { register });
    var threat = new ThreatTrack { ProjectilePtr = register.Pointer, ProjectileId = 1, ThreatClass = ThreatClass.ATGM_SACLOS,
        Position = new(0, 0, 10), Velocity = new(0, 0, -100), RelativeVelocity = new(0, 0, -100), DetectionTime = 0, TimeToImpact = .1f };
    return (state, module, threat, register);
}
void Dispatch(APSVehicleState state, ThreatTrack threat)
{
    var planner = new APSEngagementPlanner(state, log);
    planner.Update(new[] { threat });
    planner.FixedUpdate();
}
var miss = Setup(APSType.Hybrid, 0, 1);
Dispatch(miss.State, miss.Threat);
Check(miss.Module.RemainingCountermeasures == 1 && miss.Module.CooldownRemaining == .5f, "Hybrid uses one ammo/cooldown.");
Check(miss.Register.activeIdMap.Count == 1 && UnityEngine.Random.Reads == 2, "Hybrid miss tries one jam.");
softKill.FixedUpdate();
Check(miss.Register.pool[0].velocity.x != 0 || miss.Register.pool[0].velocity.z != -100, "Fallback jam affects surviving native projectile.");
var success = Setup(APSType.Hybrid, 1, 1);
UnityEngine.Random.Reads = 0;
Dispatch(success.State, success.Threat);
Check(success.Register.activeIdMap.Count == 0 && UnityEngine.Random.Reads == 1 && success.Module.RemainingCountermeasures == 1, "Released projectile never jammed.");
var failedJam = Setup(APSType.SoftKill, 0, .1f);
UnityEngine.Random.Reads = 0;
Dispatch(failedJam.State, failedJam.Threat);
Dispatch(failedJam.State, failedJam.Threat);
Check(UnityEngine.Random.Reads == 1 && failedJam.Module.RemainingCountermeasures == 1 && failedJam.Module.AssignedThreats.Count == 0, "Failed jam consumes one attempt; no free retry.");
softKill.Dispose();
softKill = new APSSoftKillSystem(log);
var held = Setup(APSType.SoftKill, 0, 1);
Dispatch(held.State, held.Threat);
held.Module.Update(1);
UnityEngine.Random.Reads = 0;
Dispatch(held.State, held.Threat);
Check(held.Module.RemainingCountermeasures == 1 && UnityEngine.Random.Reads == 0, "Held jam does not spend ammo or roll again after cooldown.");
var partial = Setup(APSType.HardKill, 0, 0);
partial.Module.ConsumeCountermeasure();
Time.time += 2;
new APSCountermeasureManager(partial.State, log).FixedUpdate();
Check(partial.Module.RemainingCountermeasures == 1, "Manager must not reload partially spent Custom magazine.");
partial.Module.ConsumeCountermeasure();
partial.Module.Update(.5f);
new APSCountermeasureManager(partial.State, log).FixedUpdate();
Check(partial.Module.RemainingCountermeasures == 0, "Custom waits for countdown.");
partial.Module.Update(.5f);
Check(partial.Module.RemainingCountermeasures == 2, "Custom reloads once after countdown.");
partial.Threat.Position = new(0, 0, 1);
Check(!partial.Module.CanPrepare(partial.Threat) && !partial.Module.CanEngage(partial.Threat), "Minimum stand-off remains enforced.");
Check(APSThreatClassifier.Classify(partial.Register.pool[0], out var he, out _, out _, (IntPtr)1, partial.Register.Pointer) && he == ThreatClass.CannonHE, "Native HE gets a single CannonHE class.");
Check(partial.Module.GetInterceptProbability(he) == 0, "Classified Custom CannonHE zero remains zero.");
Console.WriteLine("PASS: production dispatch, Hybrid fallback, failed jam cooldown, Custom manager reload and stand-off (native stand-ins).");

namespace BepInEx.Logging { public class ManualLogSource { public void LogInfo(string s) { } public void LogWarning(string s) { } public void LogError(string s) { } } }
namespace Sprocket.Vehicles
{
    public class Vehicle { public IntPtr Pointer = (IntPtr)1; public bool Released; public Transform transform = new(); public Vehicle Behaviour => this; }
    public class VehicleComponent { public IntPtr Pointer = (IntPtr)2; public Transform transform = new(); }
}
namespace Sprocket.Vehicles.AttachedBehaviours { }
namespace Sprocket.Vehicles.Cannons { public class Cannon { public Sprocket.Vehicles.Vehicle Vehicle = new(); } }
namespace Sprocket.Vehicles.Weapons.Cannons { public class CannonBehaviour { public Il2CppSystem.Object mount = new(); } }
namespace Il2CppSystem { public class Object { public T? TryCast<T>() where T : class => null; } }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute { public HarmonyPatch() { } public HarmonyPatch(Type type, string method) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
}
namespace SprocketAdvancedAPS.Parts { internal static class APSPartRegistry { internal static APSDefinition? GetModuleDefinition(string id) => null; } }
namespace SprocketAdvancedAPS { internal static class Main { internal static BepInEx.Logging.ManualLogSource ModLog = new(); } }
namespace Sprocket.ProjectilePhysicsSystems
{
    public struct ProjectileFireInfo { public int ProjectileID; }
    public struct ProjectileInstance { public int ID, flags; public Vector3 position, velocity; public Sprocket.DamageModelling.ProjectileDefinition Definition; }
}
namespace Sprocket.DamageModelling
{
    public record ProjectileDefinition(string Name);
    public class ProjectileRegister
    {
        public IntPtr Pointer = (IntPtr)3;
        public Dictionary<int, int> activeIdMap = new();
        public List<Sprocket.ProjectilePhysicsSystems.ProjectileInstance> pool = new();
        public void Release(int id) => activeIdMap.Remove(id);
        public void Launch() { }
        public void FixedUpdate() { }
        public void DestroyAll() { }
    }
}
namespace SprocketAdvancedAPS.Systems
{
    internal class APSVisualManager
    {
        internal static APSVisualManager? Instance = null;
        internal void FireInterceptEffect(APSModuleState m, ThreatTrack t, bool success) { }
        internal void SpawnInterceptExplosion(Sprocket.DamageModelling.ProjectileRegister r, Vector3 p, Vector3 v) { }
    }
    internal class APSAudioManager
    {
        internal static APSAudioManager? Instance = null;
        internal void PlayLaunchSound(APSModuleState m) { }
        internal void PlayJamSound(APSModuleState m) { }
    }
}
namespace SprocketAdvancedAPS.Core
{
    internal class Entry<T> { internal T Value; internal Entry(T value) => Value = value; }
    internal static class APSCore
    {
        internal static Entry<bool> ConfigEnabled = new(true), ConfigDebugMode = new(false);
        internal static float DetectionMultiplier => 1;
        internal static float ReactionMultiplier => 1;
        internal static bool SpeedBasedInterception(APSDefinition d) => d.CustomSettings?.SpeedBased ?? true;
        internal static float MinInterceptSpeed(APSDefinition d) => d.MinEngagementSpeed;
        internal static float MaxInterceptSpeed(APSDefinition d) => d.MaxEngagementSpeed;
        internal static float ModuleRangeMultiplier(string id) => 1;
        internal static float ModuleReactionMultiplier(string id) => 1;
        internal static float ModuleInterceptMultiplier(string id) => 1;
        internal static bool IsModuleEnabled(string id) => true;
    }
    internal class APSVehicleState { internal Sprocket.Vehicles.Vehicle Vehicle = new(); internal List<APSModuleState> Modules = new(); internal Vector3 Position = default, Velocity = default; }
}
namespace UnityEngine
{
    public class Transform
    {
        public Vector3 position;
        public Vector3 InverseTransformDirection(Vector3 v) => v;
        public Vector3 InverseTransformPoint(Vector3 v) => v - position;
    }
    public static class Time { public static float time, deltaTime = .02f, fixedDeltaTime = .02f; }
    public static class Random { public static int Reads; public static float value { get { Reads++; return .5f; } } }
    public static class Mathf
    {
        public const float Rad2Deg = 180 / MathF.PI;
        public static float Clamp01(float v) => Math.Clamp(v, 0, 1);
        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Atan2(float a, float b) => MathF.Atan2(a, b);
    }
    public readonly record struct Vector2(float x, float y) { public float magnitude => MathF.Sqrt(x*x + y*y); }
    public record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => default;
        public static Vector3 forward => new(0, 0, 1);
        public static Vector3 up => new(0, 1, 0);
        public float sqrMagnitude => x*x + y*y + z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this / magnitude : default;
        public void Normalize() { this = normalized; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x, a.y+b.y, a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x, a.y-b.y, a.z-b.z);
        public static Vector3 operator -(Vector3 a) => a * -1;
        public static Vector3 operator *(Vector3 v, float f) => new(v.x*f, v.y*f, v.z*f);
        public static Vector3 operator /(Vector3 v, float f) => v * (1 / f);
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x + a.y*b.y + a.z*b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        public static float Angle(Vector3 a, Vector3 b) => MathF.Acos(Math.Clamp(Dot(a.normalized, b.normalized), -1, 1)) * Mathf.Rad2Deg;
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => (a * (1-t) + b * t).normalized;
    }
}
