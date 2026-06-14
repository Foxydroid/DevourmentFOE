using MonoMod.RuntimeDetour;
using RWDevourment;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using RWCustom;

namespace DevourmentFOE.Hooks
{
    /// <summary>
    /// Implements the "Other Exit :)" mechanic — an alternative regurgitation from the lower body.
    /// </summary>
    public class OtherExitHooks
    {
        public const DevourmentMain.CurrentBellyStatus STATUS_OTHER_EXIT = (DevourmentMain.CurrentBellyStatus)6;

        public static readonly Color COLOR_OTHER_EXIT = new Color(0.5f, 0.3f, 0.15f);
        public static readonly Color COLOR_OTHER_EXIT_DARK = new Color(0.15f, 0.09f, 0.045f);

        private Hook _playerUpdateHook;
        private Hook _regurgitateThingHook;
        private Hook _reloadSlotsHook;
        private Hook _runActionHook;
        private Hook _getValueFromStatusHook;
        private Hook _getColorsFromStatusHook;
        private Hook _bellySpriteCtorHook;
        private Hook _bellySpriteUpdateHook;
        private Hook _getPosFromDepthHook;
        private Hook _getNameFromStatusHook;
        private Hook _getIconNameFromStatusHook;
        private Hook _getColorFromStatusHook;
        private Hook _radialMenuSlotDrawSpritesHook;
        private Hook _bellySpriteNewScaleHook;
        private Hook _critRefDatStruggleHook;
        private Hook _playerGraphicsUpdateHook;
        private Hook _playerGraphicsDrawSpritesHook;

        private class PlayerOtherExitState
        {
            public float tension = 0f;
        }

        private class PreyOtherExitState
        {
            public float otherDepth = 0f;
            public int cooldownTicks = 0;
        }

        private static readonly ConditionalWeakTable<Player, PlayerOtherExitState> _playerStates
            = new ConditionalWeakTable<Player, PlayerOtherExitState>();

        private static readonly ConditionalWeakTable<AbstractPhysicalObject, PreyOtherExitState> _preyStates
            = new ConditionalWeakTable<AbstractPhysicalObject, PreyOtherExitState>();

        private static bool _isOtherExitActive = false;
        private static AbstractCreature _activeSwallower = null;

        [ThreadStatic]
        private static BellySprite _currentUpdatingBellySprite = null;

        private const float DEPTH_READY_THRESHOLD = 0.9f;
        private const float TENSION_FILL_TICKS = 160f;
        private const float TENSION_DECAY_TICKS = 60f;
        private const float LAUNCH_SPEED = 20f;
        private const float SPAWN_OFFSET = 12f;
        private const float MAX_SHAKE_INTENSITY = 1.5f;
        private const float MOVEMENT_DAMPING = 0.3f;
        private const float MAX_BELLY_DROP = 10f;

        private const float TAIL_SIN_MULT = 5.0f;
        private const float TAIL_TIP_UP = 1.73f;
        private const float TAIL_TIP_HEAD = 1.0f;
        private const float TAIL_PULL_STRENGTH = 5.0f;

        public void Apply()
        {
            MethodInfo playerUpdate = typeof(Player).GetMethod(
                "Update", BindingFlags.Public | BindingFlags.Instance);
            if (playerUpdate != null)
            {
                _playerUpdateHook = new Hook(playerUpdate,
                    new Action<Action<Player, bool>, Player, bool>(Player_Update_Hook));
            }

            MethodInfo regurgitateThing = typeof(DevourmentMain).GetMethod(
                "RegurgitateThing", BindingFlags.Public | BindingFlags.Static);
            if (regurgitateThing != null)
            {
                _regurgitateThingHook = new Hook(regurgitateThing,
                    new Action<Action<AbstractPhysicalObject, int>, AbstractPhysicalObject, int>(
                        RegurgitateThing_Hook));
            }

            MethodInfo reloadSlots = typeof(MenuManager).GetMethod(
                "ReloadSlots", BindingFlags.Public | BindingFlags.Instance);
            if (reloadSlots != null)
            {
                _reloadSlotsHook = new Hook(reloadSlots,
                    new Func<Func<MenuManager, List<RadialMenu.Slot>>, MenuManager, List<RadialMenu.Slot>>(
                        MenuManager_ReloadSlots_Hook));
            }

            MethodInfo runAction = typeof(MenuManager).GetMethod(
                "RunAction", BindingFlags.Public | BindingFlags.Instance);
            if (runAction != null)
            {
                _runActionHook = new Hook(runAction,
                    new Action<Action<MenuManager, RadialMenu.Slot>, MenuManager, RadialMenu.Slot>(
                        MenuManager_RunAction_Hook));
            }

            MethodInfo getValueFromStatus = typeof(DevourmentMain).GetMethod(
                "GetValueFromStatus", BindingFlags.Public | BindingFlags.Static);
            if (getValueFromStatus != null)
            {
                _getValueFromStatusHook = new Hook(getValueFromStatus,
                    new Func<Func<AbstractPhysicalObject, float>, AbstractPhysicalObject, float>(
                        GetValueFromStatus_Hook));
            }

            MethodInfo getColorsFromStatus = typeof(DevourmentMain).GetMethod(
                "GetColorsFromStatus", BindingFlags.Public | BindingFlags.Static);
            if (getColorsFromStatus != null)
            {
                _getColorsFromStatusHook = new Hook(getColorsFromStatus,
                    new Func<Func<AbstractPhysicalObject, Color[]>, AbstractPhysicalObject, Color[]>(
                        GetColorsFromStatus_Hook));
            }

            ConstructorInfo bellySpriteCtor = typeof(BellySprite).GetConstructor(
                new Type[] { typeof(CritRef.CritRefDat), typeof(CritRef.CritRefDat), typeof(RoomCamera), typeof(Vector2) });
            if (bellySpriteCtor != null)
            {
                _bellySpriteCtorHook = new Hook(bellySpriteCtor,
                    new Action<Action<BellySprite, CritRef.CritRefDat, CritRef.CritRefDat, RoomCamera, Vector2>,
                        BellySprite, CritRef.CritRefDat, CritRef.CritRefDat, RoomCamera, Vector2>(
                            BellySprite_ctor_Hook));
            }

            MethodInfo bellySpriteUpdate = typeof(BellySprite).GetMethod(
                "Update", BindingFlags.Public | BindingFlags.Instance);
            if (bellySpriteUpdate != null)
            {
                _bellySpriteUpdateHook = new Hook(bellySpriteUpdate,
                    new Action<Action<BellySprite>, BellySprite>(
                        BellySprite_Update_Hook));
            }

            MethodInfo getPosFromDepth = typeof(DevourmentMain).GetMethod(
                "GetPosFromDepth", BindingFlags.Public | BindingFlags.Static);
            if (getPosFromDepth != null)
            {
                _getPosFromDepthHook = new Hook(getPosFromDepth,
                    new Func<Func<Vector2[], float, Vector2>, Vector2[], float, Vector2>(
                        GetPosFromDepth_Hook));
            }

            MethodInfo getNameFromStatus = typeof(MenuManager).GetMethod(
                "GetNameFromStatus", BindingFlags.Public | BindingFlags.Static);
            if (getNameFromStatus != null)
            {
                _getNameFromStatusHook = new Hook(getNameFromStatus,
                    new Func<Func<DevourmentMain.CurrentBellyStatus, bool, string>, DevourmentMain.CurrentBellyStatus, bool, string>(
                        GetNameFromStatus_Hook));
            }

            MethodInfo getIconNameFromStatus = typeof(MenuManager).GetMethod(
                "GetIconNameFromStatus", BindingFlags.Public | BindingFlags.Static);
            if (getIconNameFromStatus != null)
            {
                _getIconNameFromStatusHook = new Hook(getIconNameFromStatus,
                    new Func<Func<DevourmentMain.CurrentBellyStatus, string>, DevourmentMain.CurrentBellyStatus, string>(
                        GetIconNameFromStatus_Hook));
            }

            MethodInfo getColorFromStatus = typeof(MenuManager).GetMethod(
                "GetColorFromStatus", BindingFlags.Public | BindingFlags.Static);
            if (getColorFromStatus != null)
            {
                _getColorFromStatusHook = new Hook(getColorFromStatus,
                    new Func<Func<DevourmentMain.CurrentBellyStatus, Color>, DevourmentMain.CurrentBellyStatus, Color>(
                        GetColorFromStatus_Hook));
            }

            MethodInfo slotDrawSprites = typeof(RadialMenu.Slot).GetMethod(
                "DrawSprites", BindingFlags.Public | BindingFlags.Instance);
            if (slotDrawSprites != null)
            {
                _radialMenuSlotDrawSpritesHook = new Hook(slotDrawSprites,
                    new Action<Action<RadialMenu.Slot, float>, RadialMenu.Slot, float>(
                        RadialMenu_Slot_DrawSprites_Hook));
            }

            MethodInfo bellySpriteNewScale = typeof(BellySprite).GetMethod(
                "NewScale", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (bellySpriteNewScale != null)
            {
                _bellySpriteNewScaleHook = new Hook(bellySpriteNewScale,
                    new Func<Func<BellySprite, float>, BellySprite, float>(
                        BellySprite_NewScale_Hook));
            }

            MethodInfo critRefDatStruggle = typeof(CritRef.CritRefDat).GetMethod(
                "Struggle", BindingFlags.Public | BindingFlags.Instance);
            if (critRefDatStruggle != null)
            {
                _critRefDatStruggleHook = new Hook(critRefDatStruggle,
                    new Action<Action<CritRef.CritRefDat>, CritRef.CritRefDat>(
                        CritRefDat_Struggle_Hook));
            }

            MethodInfo playerGraphicsUpdate = typeof(PlayerGraphics).GetMethod(
                "Update", BindingFlags.Public | BindingFlags.Instance);
            if (playerGraphicsUpdate != null)
            {
                _playerGraphicsUpdateHook = new Hook(playerGraphicsUpdate,
                    new Action<Action<PlayerGraphics>, PlayerGraphics>(PlayerGraphics_Update_Hook));
            }

            MethodInfo playerGraphicsDrawSprites = typeof(PlayerGraphics).GetMethod(
                "DrawSprites", BindingFlags.Public | BindingFlags.Instance);
            if (playerGraphicsDrawSprites != null)
            {
                _playerGraphicsDrawSpritesHook = new Hook(playerGraphicsDrawSprites,
                    new Action<Action<PlayerGraphics, RoomCamera.SpriteLeaser, RoomCamera, float, Vector2>,
                        PlayerGraphics, RoomCamera.SpriteLeaser, RoomCamera, float, Vector2>(
                            PlayerGraphics_DrawSprites_Hook));
            }
        }

        public void Dispose()
        {
            _playerUpdateHook?.Dispose();
            _regurgitateThingHook?.Dispose();
            _reloadSlotsHook?.Dispose();
            _runActionHook?.Dispose();
            _getValueFromStatusHook?.Dispose();
            _getColorsFromStatusHook?.Dispose();
            _bellySpriteCtorHook?.Dispose();
            _bellySpriteUpdateHook?.Dispose();
            _getPosFromDepthHook?.Dispose();
            _getNameFromStatusHook?.Dispose();
            _getIconNameFromStatusHook?.Dispose();
            _getColorFromStatusHook?.Dispose();
            _radialMenuSlotDrawSpritesHook?.Dispose();
            _bellySpriteNewScaleHook?.Dispose();
            _critRefDatStruggleHook?.Dispose();
            _playerGraphicsUpdateHook?.Dispose();
            _playerGraphicsDrawSpritesHook?.Dispose();
        }

        private static bool IsMarkedForOtherExit(AbstractPhysicalObject absObj)
        {
            if (absObj == null) return false;
            var critRef = absObj.GetCritRefs();
            return critRef != null && critRef.currentBellyStatus == STATUS_OTHER_EXIT;
        }

        private static float GetPreyOtherDepth(AbstractPhysicalObject absObj)
        {
            if (absObj == null) return 0f;
            return _preyStates.GetOrCreateValue(absObj).otherDepth;
        }

        private static void SetPreyOtherDepth(AbstractPhysicalObject absObj, float val)
        {
            if (absObj == null) return;
            var state = _preyStates.GetOrCreateValue(absObj);
            state.otherDepth = val;
            state.cooldownTicks = 0;
        }

        private static float GetPlayerTension(Player player)
        {
            if (player == null) return 0f;
            return _playerStates.GetOrCreateValue(player).tension;
        }

        private static bool IsPreyReady(AbstractPhysicalObject absObj)
        {
            if (!IsMarkedForOtherExit(absObj)) return false;
            return GetPreyOtherDepth(absObj) >= DEPTH_READY_THRESHOLD;
        }

        private static float GetValueFromStatus_Hook(Func<AbstractPhysicalObject, float> orig, AbstractPhysicalObject absObj)
        {
            if (IsMarkedForOtherExit(absObj))
            {
                return GetPreyOtherDepth(absObj);
            }
            return orig(absObj);
        }

        private static Color[] GetColorsFromStatus_Hook(Func<AbstractPhysicalObject, Color[]> orig, AbstractPhysicalObject absObj)
        {
            if (IsMarkedForOtherExit(absObj))
            {
                return new Color[] { COLOR_OTHER_EXIT, COLOR_OTHER_EXIT_DARK };
            }
            return orig(absObj);
        }

        private static string GetNameFromStatus_Hook(Func<DevourmentMain.CurrentBellyStatus, bool, string> orig, DevourmentMain.CurrentBellyStatus status, bool rawID)
        {
            if (status == STATUS_OTHER_EXIT)
            {
                return rawID ? "devourmentSetOtherExit" : "Other Exit :)";
            }
            return orig(status, rawID);
        }

        private static string GetIconNameFromStatus_Hook(Func<DevourmentMain.CurrentBellyStatus, string> orig, DevourmentMain.CurrentBellyStatus status)
        {
            if (status == STATUS_OTHER_EXIT)
            {
                return "devourmentArrow";
            }
            return orig(status);
        }

        private static Color GetColorFromStatus_Hook(Func<DevourmentMain.CurrentBellyStatus, Color> orig, DevourmentMain.CurrentBellyStatus status)
        {
            if (status == STATUS_OTHER_EXIT)
            {
                return COLOR_OTHER_EXIT;
            }
            return orig(status);
        }

        private static void RadialMenu_Slot_DrawSprites_Hook(Action<RadialMenu.Slot, float> orig, RadialMenu.Slot self, float timeStacker)
        {
            orig(self, timeStacker);

            if (self.targetCrit != null && self.targetCrit.currentBellyStatus == STATUS_OTHER_EXIT)
            {
                self.statusIconName = "devourmentArrow";
                self.statusIconColor = COLOR_OTHER_EXIT;
                self.statusVisible = true;
                if (self.statusIcon != null)
                {
                    self.statusIcon.element = Futile.atlasManager.GetElementWithName("devourmentArrow");
                    self.statusIcon.color = COLOR_OTHER_EXIT;
                    self.statusIcon.isVisible = true;
                }
            }
        }

        private static float BellySprite_NewScale_Hook(Func<BellySprite, float> orig, BellySprite self)
        {
            float scale = orig(self);
            if (self.preyRef != null && IsMarkedForOtherExit(self.preyRef.abstractObject))
            {
                var playerPred = self.predRef?.abstractCreature?.realizedCreature as Player;
                if (playerPred != null)
                {
                    float tension = GetPlayerTension(playerPred);
                    if (tension > 0f)
                    {
                        scale *= Mathf.Lerp(1.0f, 0.6f, tension);
                    }
                }
            }
            return scale;
        }

        private static void CritRefDat_Struggle_Hook(Action<CritRef.CritRefDat> orig, CritRef.CritRefDat self)
        {
            if (self.abstractObject != null && IsMarkedForOtherExit(self.abstractObject))
            {
                if (self.currentStruggleMode != DevourmentMain.CurrentStruggleMode.Fighting)
                {
                    // Any struggle mode other than Fighting does absolutely nothing
                    return;
                }
            }
            orig(self);
        }

        private static void BellySprite_ctor_Hook(Action<BellySprite, CritRef.CritRefDat, CritRef.CritRefDat, RoomCamera, Vector2> orig,
            BellySprite self, CritRef.CritRefDat preyRef, CritRef.CritRefDat predRef, RoomCamera rcam, Vector2 camPos)
        {
            _currentUpdatingBellySprite = self;
            try
            {
                orig(self, preyRef, predRef, rcam, camPos);
            }
            finally
            {
                _currentUpdatingBellySprite = null;
            }
        }

        private static void BellySprite_Update_Hook(Action<BellySprite> orig, BellySprite self)
        {
            _currentUpdatingBellySprite = self;
            try
            {
                orig(self);
            }
            finally
            {
                _currentUpdatingBellySprite = null;
            }
        }

        private static Vector2 GetPosFromDepth_Hook(Func<Vector2[], float, Vector2> orig, Vector2[] points, float depth)
        {
            if (_currentUpdatingBellySprite != null
                && _currentUpdatingBellySprite.preyRef != null
                && IsMarkedForOtherExit(_currentUpdatingBellySprite.preyRef.abstractObject))
            {
                var preyAbs = _currentUpdatingBellySprite.preyRef.abstractObject;
                var predRef = _currentUpdatingBellySprite.predRef;
                var playerPred = predRef.abstractCreature?.realizedCreature as Player;

                if (playerPred != null && playerPred.bodyChunks.Length > 1 && points != null && points.Length > 0)
                {
                    Vector2 pathPos = orig(points, 0f);

                    float tension = GetPlayerTension(playerPred);
                    if (tension > 0f)
                    {
                        Vector2 launchDir = (playerPred.bodyChunks[1].pos - playerPred.bodyChunks[0].pos).normalized;
                        pathPos += launchDir * (tension * MAX_BELLY_DROP);
                    }

                    return pathPos;
                }
            }

            return orig(points, depth);
        }

        private static List<RadialMenu.Slot> MenuManager_ReloadSlots_Hook(
            Func<MenuManager, List<RadialMenu.Slot>> orig, MenuManager self)
        {
            var slots = orig(self);

            if (self.subMenuType == MenuManager.SubMenuTypes.ChangeStatus)
            {
                slots.Add(new RadialMenu.Slot(self.menu)
                {
                    name = "devourmentSetOtherExit",
                    iconName = "devourmentArrow",
                    curIconColor = COLOR_OTHER_EXIT,
                    tooltip = "Other Exit :)"
                });
            }

            if (self.subMenuType == MenuManager.SubMenuTypes.ViewList)
            {
                slots.RemoveAll(slot =>
                    slot.name == "devourmentSingleCrit"
                    && slot.targetCrit != null
                    && IsPreyReady(slot.targetCrit.abstractObject));
            }

            return slots;
        }

        private static void MenuManager_RunAction_Hook(
            Action<MenuManager, RadialMenu.Slot> orig, MenuManager self, RadialMenu.Slot slot)
        {
            if (self.subMenuType == MenuManager.SubMenuTypes.ChangeStatus
                && slot != null
                && slot.name == "devourmentSetOtherExit")
            {
                if (self.targetCritRef != null)
                {
                    foreach (var critRef in self.targetCritRef)
                    {
                        critRef.currentBellyStatus = STATUS_OTHER_EXIT;
                        SetPreyOtherDepth(critRef.abstractObject, 0f);
                    }
                }

                try
                {
                    MethodInfo moveBack = typeof(MenuManager).GetMethod(
                        "MoveSelectionBack",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    moveBack?.Invoke(self, null);
                }
                catch { }

                return;
            }

            orig(self, slot);
        }

        private static void PlayerGraphics_Update_Hook(Action<PlayerGraphics> orig, PlayerGraphics self)
        {
            orig(self);

            Player player = self.owner as Player;
            if (player == null || self.tail == null || self.tail.Length == 0) return;

            float tension = GetPlayerTension(player);
            if (tension <= 0f) return;

            float t_pose = Mathf.Min(tension / 0.4f, 1.0f);
            float shakeIntensity = Mathf.InverseLerp(0.4f, 1.0f, tension);

            Vector2 headDir = (player.bodyChunks[0].pos - player.bodyChunks[1].pos).normalized;
            Vector2 upDir = Vector2.up;

            // Sickle / scorpion arc:
            //   base (t=0):  lift strongly upward, no forward pull yet
            //   mid  (t=0.5): arc upward-forward
            //   tip  (t=1):  curl toward head (forward), still slightly up
            //
            // upCoeff   decreases from 2 at base to 0.5 at tip (lifts base high)
            // headCoeff increases from 0 at base to 2 at tip   (tip points at head)

            int L = self.tail.Length;
            for (int i = 0; i < L; i++)
            {
                float t = (L > 1) ? (float)i / (L - 1) : 0f;

                // tail[0] (t=0): zero pull — base stays put
                // tail[1-2]     : sin-peak sideways push
                // tail[3] (t=1) : tip direction points up/forward
                float upCoeff = Mathf.Sin(t * Mathf.PI) * TAIL_SIN_MULT + Mathf.Lerp(0f, TAIL_TIP_UP, t);
                float headCoeff = Mathf.Lerp(0f, TAIL_TIP_HEAD, t * t) - Mathf.Sin(t * Mathf.PI) * TAIL_SIN_MULT;

                Vector2 pullDir = upDir * upCoeff + headDir * headCoeff;
                if (pullDir.sqrMagnitude > 0.0001f)
                    pullDir.Normalize();

                self.tail[i].vel += pullDir * (TAIL_PULL_STRENGTH * t_pose);

                if (shakeIntensity > 0f)
                {
                    self.tail[i].vel += Custom.RNV() * (shakeIntensity * 4.5f);
                }
            }
        }

        private static void PlayerGraphics_DrawSprites_Hook(
            Action<PlayerGraphics, RoomCamera.SpriteLeaser, RoomCamera, float, Vector2> orig,
            PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            Player player = self.owner as Player;
            if (player != null)
            {
                float tension = GetPlayerTension(player);
                if (tension >= 0.6f && sLeaser.sprites != null && sLeaser.sprites.Length > 9 && sLeaser.sprites[9] != null)
                {
                    sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName("FaceStunned");
                }
            }
        }


        private static void Player_Update_Hook(Action<Player, bool> orig, Player self, bool eu)
        {
            orig(self, eu);

            if (self == null || self.room == null || self.dead) return;

            var state = _playerStates.GetOrCreateValue(self);
            var critRef = self.abstractCreature.GetCritRefs();

            if (critRef == null || critRef.bellyCrits.Count == 0)
            {
                state.tension = 0f;
                return;
            }

            List<AbstractPhysicalObject> readyPreys = new List<AbstractPhysicalObject>();
            for (int i = 0; i < critRef.bellyCrits.Count; i++)
            {
                var prey = critRef.bellyCrits[i];
                if (IsMarkedForOtherExit(prey))
                {
                    var preyState = _preyStates.GetOrCreateValue(prey);
                    var preyCritRef = prey.GetCritRefs();

                    if (preyCritRef != null)
                    {
                        if (preyCritRef.currentStruggleMode == DevourmentMain.CurrentStruggleMode.Fighting)
                        {
                            float currentDepth = preyCritRef.Depth;
                            if (currentDepth > 0f)
                            {
                                float toSubtract = Mathf.Min(preyState.otherDepth, currentDepth);
                                preyState.otherDepth -= toSubtract;
                                preyCritRef.Depth = currentDepth - toSubtract;

                                if (preyCritRef.Depth > 0f)
                                {
                                    preyState.cooldownTicks = 80;
                                }
                            }
                        }

                        if (preyState.cooldownTicks > 0)
                        {
                            preyState.cooldownTicks--;
                        }
                        else
                        {
                            preyState.otherDepth = Mathf.Min(preyState.otherDepth + 0.01f, 1.0f);
                        }
                    }

                    if (preyState.otherDepth >= DEPTH_READY_THRESHOLD)
                    {
                        readyPreys.Add(prey);
                    }
                }
                else
                {
                    var preyState = _preyStates.GetOrCreateValue(prey);
                    if (preyState.otherDepth > 0f)
                    {
                        preyState.otherDepth = Mathf.Max(0f, preyState.otherDepth - 0.02f);
                    }
                }
            }

            bool holdingDown = self.input[0].y < 0;
            bool holdingGrab = self.input[0].pckp;
            bool holdingOtherExit = holdingDown && holdingGrab;

            if (holdingOtherExit && readyPreys.Count > 0)
            {
                self.input[0].pckp = false;

                self.bodyChunks[0].vel.x *= MOVEMENT_DAMPING;
                self.bodyChunks[1].vel.x *= MOVEMENT_DAMPING;

                state.tension += 1f / TENSION_FILL_TICKS;
                state.tension = Mathf.Min(state.tension, 1f);

                self.Blink(5);
                float shakeAmount = state.tension * MAX_SHAKE_INTENSITY;
                self.mainBodyChunk.vel += Custom.RNV() * shakeAmount;

                if (state.tension >= 1f)
                {
                    _isOtherExitActive = true;
                    _activeSwallower = self.abstractCreature;

                    try
                    {
                        foreach (var prey in readyPreys)
                        {
                            DevourmentMain.RegurgitateThing(prey, 80);
                            prey.GetCritRefs().currentBellyStatus = DevourmentMain.CurrentBellyStatus.Held;
                        }
                    }
                    finally
                    {
                        _isOtherExitActive = false;
                        _activeSwallower = null;
                    }

                    state.tension = 0f;

                    self.bodyChunks[0].vel.y += 8f;
                    self.room.PlaySound(SoundID.Slugcat_Terrain_Impact_Hard, self.mainBodyChunk);
                }
            }
            else
            {
                if (state.tension > 0f)
                {
                    state.tension = Mathf.Max(0f, state.tension - 1f / TENSION_DECAY_TICKS);
                }
            }
        }

        private static void RegurgitateThing_Hook(
            Action<AbstractPhysicalObject, int> orig, AbstractPhysicalObject absObj, int stun)
        {
            if (!_isOtherExitActive || _activeSwallower == null)
            {
                orig(absObj, stun);
                return;
            }

            AbstractCreature swallower = _activeSwallower;
            orig(absObj, stun);

            if (absObj.realizedObject == null || swallower.realizedCreature == null)
                return;

            Creature swallowerCreature = swallower.realizedCreature;

            Vector2 exitPos;
            Vector2 launchDir;

            if (swallowerCreature.bodyChunks.Length > 1)
            {
                exitPos = swallowerCreature.bodyChunks[1].pos;
                launchDir = Custom.DirVec(
                    swallowerCreature.bodyChunks[0].pos,
                    swallowerCreature.bodyChunks[1].pos);
            }
            else
            {
                exitPos = swallowerCreature.firstChunk.pos;
                launchDir = -Vector2.up;
            }

            Vector2 launchVelocity = launchDir * LAUNCH_SPEED;

            Vector2 spawnPos = exitPos + launchDir * SPAWN_OFFSET;
            foreach (BodyChunk chunk in absObj.realizedObject.bodyChunks)
            {
                chunk.HardSetPosition(spawnPos);
                chunk.vel = launchVelocity;
            }

            BodyChunk lowerChunk = swallowerCreature.bodyChunks.Length > 1
                ? swallowerCreature.bodyChunks[1]
                : swallowerCreature.firstChunk;
            if (lowerChunk.vel.magnitude < 12f)
            {
                lowerChunk.vel -= launchDir * 7f;
            }
        }
    }
}
