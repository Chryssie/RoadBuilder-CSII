﻿using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;

using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI.InGame;
using HarmonyLib;

using RoadBuilder.Systems;
using RoadBuilder.Systems.UI;
using RoadBuilder.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace RoadBuilder
{
	public class Mod : IMod
	{
		public const string Id = nameof(RoadBuilder);
        private UpgradeNameUtil _upgradeNameUtil;

        public static ILog Log { get; } = LogManager.GetLogger(nameof(RoadBuilder)).SetShowsErrorsInUI(false);
		public static Setting Settings { get; private set; }

		public void OnLoad(UpdateSystem updateSystem)
		{
			Log.Info(nameof(OnLoad));

#if DEBUG
			Log.SetEffectiveness(Level.Debug);
#endif

			UIManager.defaultUISystem.AddHostLocation("roadbuilderthumbnails", FoldersUtil.TempFolder, true);

			if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
			{
				UIManager.defaultUISystem.AddHostLocation($"roadbuildericons", Path.Combine(Path.GetDirectoryName(asset.path), "PrefabIcons"), false);
			}
			else
			{
				Log.Error("Load Failed, could not get executable path");
			}

			Settings = new Setting(this);
			Settings.RegisterKeyBindings();
			Settings.RegisterInOptionsUI();

			foreach (var item in new LocaleHelper("RoadBuilder.Locale.json").GetAvailableLanguages())
			{
				GameManager.instance.localizationManager.AddSource(item.LocaleId, item);
			}

			AssetDatabase.global.LoadSettings(nameof(RoadBuilder), Settings, new Setting(this));
			CreatePlatformUpgrade();

			updateSystem.UpdateAfter<RoadBuilderGenerationDataSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
			updateSystem.UpdateAfter<RoadBuilderPrefabUpdateSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
			updateSystem.UpdateAt<RoadBuilderInitializerSystem>(SystemUpdatePhase.MainLoop);
			updateSystem.UpdateAt<RoadBuilderInfoViewFixSystem>(SystemUpdatePhase.PrefabUpdate);
			updateSystem.UpdateBefore<RoadBuilderSerializeSystem>(SystemUpdatePhase.Serialize);
			updateSystem.UpdateAt<RoadBuilderSystem>(SystemUpdatePhase.Modification1);
			updateSystem.UpdateAt<RoadBuilderApplyTagSystem>(SystemUpdatePhase.Modification2);
			updateSystem.UpdateAt<RoadBuilderClearTagSystem>(SystemUpdatePhase.Modification2);
			updateSystem.UpdateAt<RoadBuilderToolSystem>(SystemUpdatePhase.ToolUpdate);
			updateSystem.UpdateAt<RoadBuilderConfigCleanupSystem>(SystemUpdatePhase.MainLoop);
			updateSystem.UpdateAt<RoadBuilderUISystem>(SystemUpdatePhase.UIUpdate);
			updateSystem.UpdateAt<RoadBuilderNetSectionsUISystem>(SystemUpdatePhase.UIUpdate);
			updateSystem.UpdateAt<RoadBuilderConfigurationsUISystem>(SystemUpdatePhase.UIUpdate);
		}

		public void CreatePlatformUpgrade()
		{
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            var prefabs = Traverse.Create(prefabSystem).Field<List<PrefabBase>>("m_Prefabs").Value;
            var basePrefab = prefabs.FirstOrDefault(p => p.name == "Grass");
			var baseUIObject = basePrefab.GetComponent<UIObject>();
			var baseNetUpgrade = basePrefab.GetComponent<NetUpgrade>();
			
			var platformPrefab = Object.Instantiate(basePrefab);
			platformPrefab.name = "RB_MedianPlatformUpgrade";
			platformPrefab.Remove<UIObject>();

			var nUIObject = Object.Instantiate(baseUIObject);
			nUIObject.m_Icon = "coui://gameui/Media/Placeholder.svg";
			nUIObject.name = nUIObject.name.Replace("Grass", "RB_MedianPlatformUpgrade");
			platformPrefab.AddComponentFrom(nUIObject);

			var netUpgrade = Object.Instantiate(baseNetUpgrade);
			netUpgrade.m_SetState = new[]
			{
				NetPieceRequirements.MiddlePlatform
			};
			netUpgrade.m_UnsetState = new NetPieceRequirements[0];
			platformPrefab.Remove<NetUpgrade>();
			platformPrefab.AddComponentFrom(netUpgrade);


            if (!prefabSystem.AddPrefab(platformPrefab))
			{
				Mod.Log.Warn("Unable to create platform upgrade prefab");
				return;
			}

			_upgradeNameUtil = new UpgradeNameUtil(
				World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabUISystem>(), 
				new[] { platformPrefab }
			);
        }

		public void OnDispose()
		{
			Log.Info(nameof(OnDispose));

			Settings?.UnregisterInOptionsUI();

			UIManager.defaultUISystem.RemoveHostLocation("roadbuilderthumbnails");

			new DirectoryInfo(FoldersUtil.TempFolder).Delete(true);
		}
	}
}