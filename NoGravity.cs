using BepInEx;
using BoplFixedMath;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace NoGravity
{
    [BepInPlugin("com.codemob.nogravity", "NoGravity", "1.0.0")]
    public class NoGravity : BaseUnityPlugin
    {
        public Harmony harmony;
        private void Awake()
        {
            harmony = new Harmony(Info.Metadata.GUID);

            Logger.LogInfo("who needs gravity anyway?");
            harmony.PatchAll(typeof(NoGravity));

            MethodInfo SpawnMethod = typeof(SlimeController).GetMethod("Spawn", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(SpawnMethod, postfix: new HarmonyMethod(typeof(NoGravity).GetMethod(nameof(SpawnPatch), BindingFlags.Static | BindingFlags.Public)));


            MethodInfo IntegrateBodyMethod = typeof(DetPhysics).GetMethod("IntegrateBody", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(IntegrateBodyMethod, prefix: new HarmonyMethod(typeof(NoGravity).GetMethod(nameof(DetPhysicsGravityPatch), BindingFlags.Static | BindingFlags.Public)));

            MethodInfo GravityForceMethod = typeof(BlackHole).GetMethod("GravityForce", BindingFlags.Instance | BindingFlags.NonPublic);
            harmony.Patch(GravityForceMethod, postfix: new HarmonyMethod(typeof(NoGravity).GetMethod(nameof(BlackHolePatch), BindingFlags.Static | BindingFlags.Public)));
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.UpdateSim))]
        [HarmonyPrefix]
        public static void PatchUpdateSim(ref PlayerPhysics __instance)
        {
            __instance.airAccel = (Fix)0.005F;
            __instance.gravity_modifier = Fix.Zero;
            __instance.gravity_accel = Fix.Zero;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.AddGravityFactor))]
        [HarmonyPrefix]
        public static bool PatchPlayerGravity()
        {
            return false;
        }

        [HarmonyPatch(typeof(Gravity), nameof(Gravity.UpdateSim))]
        [HarmonyPrefix]
        public static bool PatchGravityUpdate(ref Gravity __instance)
        {
            return false;
        }

        [HarmonyPatch(typeof(BoplBody), nameof(BoplBody.UpdateSim))]
        [HarmonyPrefix]
        public static void PatchBoplBodyUpdate(ref BoplBody __instance)
        {
            __instance.gravityScale = Fix.Zero;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.Jump))]
        [HarmonyPrefix]
        public static bool PatchJump(ref PlayerPhysics __instance)
        {
            FieldInfo attachedGroundField = typeof(PlayerPhysics).GetField("attachedGround", BindingFlags.Instance | BindingFlags.NonPublic);
            StickyRoundedRectangle attachedGround = attachedGroundField.GetValue(__instance) as StickyRoundedRectangle;

            PlayerBody body = __instance.GetPlayerBody();

            __instance.jumpedThisFrame = true;
            Vec2 facingDirection = (!__instance.IsGrounded()) ? Vec2.up : attachedGround.currentNormal(body);
            body.selfImposedVelocity = facingDirection * __instance.jumpStrength * (Fix)0.25F;

            body.position += body.selfImposedVelocity * __instance.extraJumpTeleportMultiplier;
            __instance.transform.position = (Vector3)body.position;
            __instance.UnGround(nullRotation: false);
            return false;
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.Move))]
        [HarmonyPrefix]
        public static bool PatchMove(ref PlayerPhysics __instance, ref Vec2 inputVector, ref Fix simDeltaTime)
        {
            if (!__instance.IsGrounded())
            {
                FieldInfo type = typeof(PlayerPhysics).GetField("playerIdHolder", BindingFlags.NonPublic | BindingFlags.Instance);
                IPlayerIdHolder playerIdHolder = type.GetValue(__instance) as IPlayerIdHolder;


                PlayerBody body = __instance.GetPlayerBody();

                body.selfImposedVelocity += inputVector * __instance.airAccel;
                Fix speed = __instance.airAccel / (__instance.Speed + __instance.airAccel);
                body.selfImposedVelocity += Vec2.left * speed * body.selfImposedVelocity.x;
                body.selfImposedVelocity += Vec2.down * speed * body.selfImposedVelocity.y;

                __instance.VelocityBasedRaycasts(attachToGroundIfHit: true, GameTime.FixedDeltaTime(playerIdHolder, simDeltaTime));
                return false;
            }
            return true;
        }

        public static void SpawnPatch(ref SlimeController __instance)
        {
            FieldInfo playerPhysicsField = typeof(SlimeController).GetField("playerPhysics", BindingFlags.Instance | BindingFlags.NonPublic);
            PlayerPhysics playerPhysics = playerPhysicsField.GetValue(__instance) as PlayerPhysics;
            __instance.body.selfImposedVelocity = Vec2.down * playerPhysics.jumpStrength * (Fix)0.125f;
        }

        public static void DetPhysicsGravityPatch(ref PhysicsBody body)
        {
            body.gravityScale = Fix.Zero;
        }

        public static void BlackHolePatch(ref BlackHole __instance, ref Fix __result, ref FixTransform fixTrans)
        {
            PlayerBody player = fixTrans.GetComponent<PlayerBody>();
            Boulder boulder = fixTrans.GetComponent<Boulder>();
            if (boulder != null)
            {
                __result *= (Fix)2;
            }
            else if (player != null)
            {
                __result *= (Fix)15;
            }

            FieldInfo dCircleField = typeof(BlackHole).GetField("dCircle", BindingFlags.Instance | BindingFlags.NonPublic);

            DPhysicsCircle dCircle = dCircleField.GetValue(__instance) as DPhysicsCircle;


            Vec2 distanceVector = dCircle.position - fixTrans.position;
            __result *= Vec2.SqrMagnitude(distanceVector) * (Fix)0.002;
        }

        [HarmonyPatch(typeof(Drill), nameof(Drill.UpdateSim))]
        [HarmonyPrefix]
        public static void DrillUpdatePatch(ref Drill __instance)
        {
            __instance.strongGravity = Fix.Zero;
            __instance.gravityStr = Fix.Zero;
        }

        [HarmonyPatch(typeof(Boulder), nameof(Boulder.UpdateSim))]
        [HarmonyPrefix]
        public static void BoulderUpdatePatch(ref Boulder __instance)
        {
            __instance.hitbox.SetGravityScale(Fix.Zero);
        }

        [HarmonyPatch(typeof(DetPhysics), nameof(DetPhysics.UpdateRopeMesh_parallell))]
        [HarmonyPrefix]
        public static void RopePatch(ref DetPhysics __instance)
        {
            __instance.playerGravity = Fix.Zero;
            __instance.ropeGravity = Fix.Zero;
        }
    }
}
