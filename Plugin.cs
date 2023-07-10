using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace Dredged
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	public class Plugin : BaseUnityPlugin
	{
		internal static ManualLogSource Log;

		private void Awake()
		{
			Log = Logger;
			// Plugin startup logic
			Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
			InitConfigs();

			// load harmony patches
			var harmony = new Harmony("dredged");
			harmony.PatchAll();
			Logger.LogInfo($"{harmony.GetPatchedMethods().Count()} harmony patches loaded!");

			return;
		}

		// configs
		internal static ConfigEntry<bool> cfgAutoFishing;
		internal static ConfigEntry<bool> cfgForceAberration;
		internal static ConfigEntry<KeyboardShortcut> cfgForceAberrationToggleKey;
		internal static ConfigEntry<bool> cfgForceTrophySize;
		internal static ConfigEntry<KeyboardShortcut> cfgForceTrophySizeToggleKey;

		private void InitConfigs()
		{
			Config.SaveOnConfigSet = false;

			cfgAutoFishing = Config.Bind("GENERAL",
				"auto_fishing",
				true,
				"automatically handles the fishing minigames");
			cfgForceAberration = Config.Bind("GENERAL",
				"force_aberration",
				true,
				"fishing always results in aberration type when available");
			cfgForceAberrationToggleKey = Config.Bind("GENERAL",
				"force_aberration_toggle_key",
				new KeyboardShortcut(KeyCode.Alpha1, new KeyCode[] { KeyCode.LeftControl }),
				"key to toggle \"force_aberration\" on/off");
			cfgForceTrophySize = Config.Bind("GENERAL",
				"force_trophy_size",
				true,
				"fishing always results in aberration type when available");
			cfgForceTrophySizeToggleKey = Config.Bind("GENERAL",
				"force_trophy_size_toggle_key",
				new KeyboardShortcut(KeyCode.Alpha2, new KeyCode[] { KeyCode.LeftControl }),
				"key to toggle \"force_trophy_size\" on/off");
			Logger.LogInfo($"plugin configs initiated!");
			return;
		}

		public void Update()
		{
			if (cfgForceAberrationToggleKey.Value.IsDown())
			{
				cfgForceAberration.Value ^= true;
				GameEvents.Instance.TriggerNotification(NotificationType.ERROR,
					"Force aberration fish: " + (cfgForceAberration.Value ? "ON" : "OFF"));
			}
			if (cfgForceTrophySizeToggleKey.Value.IsDown())
			{
				cfgForceTrophySize.Value ^= true;
				GameEvents.Instance.TriggerNotification(NotificationType.ERROR,
					"Force trophy-size fish: " + (cfgForceTrophySize.Value ? "ON" : "OFF"));
			}
			return;
		}
	}

	// TWEAK: AUTO FISH (ball catching)
	[HarmonyPatch(typeof(BallCatcherMinigame))]
	[HarmonyPatch("Update")]
	internal class AutoBallCatcherMinigame
	{
		// requires manual curation to remove private/protected access qualifier of
		// following methods/fields/types:
		// HarvestMinigame.isGameRunning
		// BallCatcherMinigame.DoesHitTarget()
		// BallCatcherMinigame.balls
		static int FindSpecialBall(ref BallCatcherMinigame obj)
		{
			for (int i = 0; i < obj.balls.Count; i++)
				if ((obj.balls[i].ballType == BallCatcherBallType.SPECIAL) &&
					(!obj.balls[i].HasGonePastTargetZone))
					return i;
			return -1;
		}

		static bool HaveBallHitTarget(ref BallCatcherMinigame obj)
		{
			foreach (var ball in obj.balls)
				if ((obj.DoesHitTarget(ball)) && (!ball.HasGonePastTargetZone))
					return true;
			return false;
		}

		static void Postfix(ref BallCatcherMinigame __instance)
		{
			if (Plugin.cfgAutoFishing.Value && __instance.isGameRunning)
			{
				var specialBallIndex = FindSpecialBall(ref __instance);
				if ((specialBallIndex == -1) && HaveBallHitTarget(ref __instance))
				{
					__instance.OnMinigameInteractPress();
				}
				else
				{
					var specialBall = __instance.balls[specialBallIndex];
					if (__instance.DoesHitTarget(specialBall) && !specialBall.HasGonePastTargetZone)
						__instance.OnMinigameInteractPress();
				}
			}
			return;
		}
	}

	// TWEAK: AUTO FISH (expanding circle)
	[HarmonyPatch(typeof(DiamondMinigame))]
	[HarmonyPatch("Update")]
	internal class AutoDiamondMinigame
	{
		// requires manual curation to remove private/protected access qualifier of
		// following methods/fields/types:
		// HarvestMinigame.isGameRunning
		// DiamondMinigame.DoesHitTarget()
		// DiamondMinigame.targets
		static int FindSpecialTarget(ref DiamondMinigame obj)
		{
			for (int i = 0; i < obj.targets.Count; i++)
				if (obj.targets[i].IsSpecial)
					return i;
			return -1;
		}

		static bool HaveTargetInRange(ref DiamondMinigame obj)
		{
			foreach (var target in obj.targets)
				if (target.IsInPlay && obj.DoesHitTarget(target.GetCurrentScale()))
					return true;
			return false;
		}

		static void Postfix(ref DiamondMinigame __instance)
		{
			if (Plugin.cfgAutoFishing.Value && __instance.isGameRunning)
			{
				var specialTargetIndex = FindSpecialTarget(ref __instance);
				if ((specialTargetIndex == -1) && HaveTargetInRange(ref __instance))
					__instance.OnMinigameInteractPress();
				else if (__instance.DoesHitTarget(__instance.targets[specialTargetIndex].GetCurrentScale()))
					__instance.OnMinigameInteractPress();
			}
			return;
		}
	}

	// TWEAK: AUTO FISH (dredge)
	[HarmonyPatch(typeof(DredgeMinigame))]
	[HarmonyPatch("Update")]
	internal class AutoDredgeMinigame
	{
		// requires manual curation to remove private/protected access qualifier of
		// following methods/fields/types:
		// HarvestMinigame.TargetConfig
		// HarvestMinigame.isGameRunning
		// DredgeMinigame.GetCurrentIndicatorAngle()
		// DredgeMinigame.isInner
		// DredgeMinigame.innerTargetConfigs
		// DredgeMinigame.outerTargetConfigs
		static bool AboutToHit(ref DredgeMinigame obj)
		{
			var targetConfigs = obj.isInner ? obj.innerTargetConfigs : obj.outerTargetConfigs;
			var currentAngle = obj.GetCurrentIndicatorAngle();
			foreach (var targetConfig in targetConfigs)
				if ((currentAngle < targetConfig.angleDeg + targetConfig.widthDeg * 1.0f) &&
					(currentAngle > targetConfig.angleDeg))
					return true;
			return false;
		}

		static bool SafeToSwitch(ref DredgeMinigame obj)
		{
			var targetConfigs = obj.isInner ? obj.outerTargetConfigs : obj.innerTargetConfigs;
			var currentAngle = obj.GetCurrentIndicatorAngle();
			foreach (var targetConfig in targetConfigs)
				if ((currentAngle > targetConfig.angleDeg - targetConfig.widthDeg * 0.6f) &&
					(currentAngle < targetConfig.angleDeg))
					return false;
			return true;
		}

		static void Postfix(ref DredgeMinigame __instance)
		{
			//Plugin.Log.LogInfo(__instance.GetCurrentIndicatorAngle());
			if (Plugin.cfgAutoFishing.Value && __instance.isGameRunning)
				if (AboutToHit(ref __instance) && SafeToSwitch(ref __instance))
					__instance.OnMinigameInteractPress();
			return;
		}
	}

	// TWEAK: AUTO FISH (circular clock)
	[HarmonyPatch(typeof(FishMinigame))]
	[HarmonyPatch("Update")]
	internal class AutoFishminigame
	{
		// requires manual curation to remove private/protected access qualifier of
		// following methods/fields/types:
		// HarvestMinigame.TargetConfig
		// HarvestMinigame.isGameRunning
		// HarvestMinigame.targetIndexesHitThisRotation
		// FishMinigame.DoesHitTarget()
		// FishMinigame.targetConfigs

		static void Postfix(ref FishMinigame __instance)
		{
			if (Plugin.cfgAutoFishing.Value && __instance.isGameRunning)
			{
				var targetConfigs = __instance.targetConfigs;
				for (int i = 0; i < targetConfigs.Count; i++)
					if (__instance.DoesHitTarget(targetConfigs[i]) && !__instance.targetIndexesHitThisRotation.Contains(i))
						__instance.OnMinigameInteractPress();
			}
			return;
		}
	}

	// TWEAK: AUTO FISH (pendulum swing)
	[HarmonyPatch(typeof(PendulumMinigame))]
	[HarmonyPatch("Update")]
	internal class AutoPendulumMinigame
	{
		// requires manual curation to remove private/protected access qualifier of
		// following methods/fields/types:
		// HarvestMinigame.TargetConfig
		// HarvestMinigame.isGameRunning
		// PendulumMinigame.DoesHitTarget()
		// PendulumMinigame.activeSegmentIndex
		// PendulumMinigame.didHitTargetThisSwing
		// PendulumMinigame.targetConfigs
		static void Postfix(ref PendulumMinigame __instance)
		{
			if (Plugin.cfgAutoFishing.Value && __instance.isGameRunning)
				if (__instance.DoesHitTarget(__instance.targetConfigs[__instance.activeSegmentIndex]) &&
					!__instance.didHitTargetThisSwing)
					__instance.OnMinigameInteractPress();
			return;
		}
	}

	// TWEAK: HULL DAMAGE IMMUNITY
	// no hull damage will be received no matter what the boat runs into!
	[HarmonyPatch]
	internal class DamageImmunity
	{
		[HarmonyTargetMethod]
		static MethodBase TargetMethod()
		{
			return AccessTools.Method(
				typeof(GridManager),
				"AddDamageToInventory",
				new System.Type[]
				{
					typeof(int),
					typeof(int),
					typeof(int),
				}
			);
		}

		[HarmonyPrefix]
		static bool Prefix()
		{
			return false;
		}
	}

	// TWEAK: FORCE FISH RARITY (ABERRATION AND TROPHY SIZE)
	[HarmonyPatch(typeof(ItemManager))]
	[HarmonyPatch(nameof(ItemManager.CreateFishItem))]
	internal class ForceFishRarity
	{
		static bool Prefix(ref FishAberrationGenerationMode aberrationGenerationMode, ref FishSizeGenerationMode sizeGenerationMode)
		{
			if (Plugin.cfgForceAberration.Value)
				aberrationGenerationMode = FishAberrationGenerationMode.FORCE;
			if (Plugin.cfgForceTrophySize.Value)
				sizeGenerationMode = FishSizeGenerationMode.FORCE_BIG_TROPHY;
			return true;
		}
	}
}
