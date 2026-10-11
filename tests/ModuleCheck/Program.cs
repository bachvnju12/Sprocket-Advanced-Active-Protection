using SprocketAdvancedAPS.Core;
using SprocketAdvancedAPS.Systems;
using UnityEngine;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
APSModuleState Module(APSType type, bool custom = true) => new(new APSVehicleState(), new Sprocket.Vehicles.VehicleComponent(), new APSDefinition
{
    Type = type, CustomSettings = custom ? new APSCustomSettings() : null,
    EngagementFOVHorizontal = 10, EngagementFOVVertical = 10,
    CountermeasureCapacity = 2, ReloadTime = 1, CooldownBetweenShots = .1f,
    InterceptProbability = new() { [ThreatClass.APFSDS] = 0 }
});
var hard = Module(APSType.HardKill);
Check(hard.GetInterceptProbability(ThreatClass.APFSDS) == 0, "Custom zero chance must stay zero.");
Check(Module(APSType.HardKill, false).GetInterceptProbability(ThreatClass.APFSDS) == .1f, "Builtin speed fallback unchanged.");
hard.ConsumeCountermeasure(); hard.ConsumeCountermeasure();
Check(hard.RemainingCountermeasures == 0, "Consume finite ammo.");
hard.Update(.5f); Check(hard.RemainingCountermeasures == 0, "Wait for full reload.");
hard.Update(.5f); Check(hard.RemainingCountermeasures == 2, "Custom reload restores capacity.");
var builtin = Module(APSType.HardKill, false);
builtin.ConsumeCountermeasure(); builtin.ConsumeCountermeasure(); builtin.Update(2);
Check(builtin.RemainingCountermeasures == 0, "New custom reload must not change builtins.");
var destroyed = Module(APSType.HardKill);
destroyed.ConsumeCountermeasure(); destroyed.ConsumeCountermeasure(); destroyed.TakeDamage(1); destroyed.Update(2);
Check(destroyed.RemainingCountermeasures == 0 && destroyed.IsDestroyed, "Dead module must not reload.");
var threat = new ThreatTrack { Position = new Vector3(2, 0, 10), Velocity = new Vector3(0, 0, -100), RelativeVelocity = new Vector3(0, 0, -100), ThreatClass = ThreatClass.ATGM_SACLOS };
var soft = Module(APSType.SoftKill);
Check(soft.CanPrepare(threat) && soft.IsInDispatchEnvelope(threat) && !soft.CanEngage(threat), "Soft-kill uses jam cone, never hard-kill release gate.");
var hybrid = Module(APSType.Hybrid);
Check(hybrid.CanPrepare(threat) && hybrid.IsInDispatchEnvelope(threat) && !hybrid.CanEngage(threat), "Hybrid jam allowed outside narrow hard-kill cone.");
Check(!hard.CanPrepare(threat), "Hard-kill cannot borrow jam cone.");
hybrid.RemainingCountermeasures = 0; hybrid.Health = .2f; hybrid.CooldownRemaining = .7f; hybrid.ReloadRemaining = .4f; hybrid.IsOperational = false;
var replacement = Module(APSType.Hybrid);
replacement.KeepRuntimeState(hybrid);
Check(replacement.RemainingCountermeasures == 0 && replacement.Health == .2f && replacement.CooldownRemaining == .7f && replacement.ReloadRemaining == .4f && !replacement.IsOperational, "Settings refresh must not refill ammo, repair health or reset timers.");
hybrid.Dispose(); Check(!hybrid.IsOperational && !hybrid.CanPrepare(threat), "Disposed old module stops held jam.");
Console.WriteLine("PASS: production module custom-zero, reload, dead-state, jam dispatch and disposal (offline Unity stand-ins; no native gameplay).");

namespace Sprocket.Vehicles
{
    public sealed class Vehicle { public Transform transform = new(); }
    public sealed class VehicleComponent { public IntPtr Pointer = (IntPtr)1; public Transform transform = new(); }
}
namespace Sprocket.Vehicles.AttachedBehaviours { }
namespace SprocketAdvancedAPS.Core
{
    internal sealed class Entry { internal float Value = 1; }
    internal static class APSCore
    {
        internal static Entry ConfigGlobalDetectionMultiplier = new();
        internal static bool SpeedBasedInterception(APSDefinition d) => true;
        internal static float MinInterceptSpeed(APSDefinition d) => 50;
        internal static float MaxInterceptSpeed(APSDefinition d) => 2000;
        internal static float ModuleRangeMultiplier(string id) => 1;
        internal static float ModuleInterceptMultiplier(string id) => 1;
        internal static bool IsModuleEnabled(string id) => true;
    }
    internal sealed class APSVehicleState { internal Sprocket.Vehicles.Vehicle Vehicle = new(); }
}
namespace UnityEngine
{
    public sealed class Transform
    {
        public Vector3 position;
        public Vector3 InverseTransformDirection(Vector3 v) => v;
        public Vector3 InverseTransformPoint(Vector3 v) => v - position;
    }
    public static class Time { public static float time; }
    public static class Mathf
    {
        public const float Rad2Deg = 180 / MathF.PI;
        public static float Clamp01(float v) => Math.Clamp(v, 0, 1);
        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Atan2(float a, float b) => MathF.Atan2(a, b);
    }
    public readonly record struct Vector2(float x, float y) { public float magnitude => MathF.Sqrt(x*x + y*y); }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 forward => new(0, 0, 1);
        public float sqrMagnitude => x*x + y*y + z*z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this * (1 / magnitude) : default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x, a.y+b.y, a.z+b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x-b.x, a.y-b.y, a.z-b.z);
        public static Vector3 operator *(Vector3 v, float f) => new(v.x*f, v.y*f, v.z*f);
        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x + a.y*b.y + a.z*b.z;
        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;
        public static float Angle(Vector3 a, Vector3 b) => MathF.Acos(Math.Clamp(Dot(a.normalized, b.normalized), -1, 1)) * Mathf.Rad2Deg;
    }
}
