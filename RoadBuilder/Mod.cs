﻿using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;

using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;

using RoadBuilder.Systems;
using RoadBuilder.Systems.UI;
using RoadBuilder.Utilities;

using System.IO;

namespace RoadBuilder
{
	public class Mod : IMod
	{
		public const string Id = nameof(RoadBuilder);
		public static ILog Log { get; } = LogManager.GetLogger(nameof(RoadBuilder)).SetShowsErrorsInUI(false);
		public static Setting Settings { get; private set; }

		public void OnLoad(UpdateSystem updateSystem)
		{
			Log.Info(nameof(OnLoad));

#if DEBUG
			Log.SetEffectiveness(Level.Debug);
#endif

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

			updateSystem.UpdateAfter<NetSectionsSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
			updateSystem.UpdateBefore<RoadBuilderSerializeSystem>(SystemUpdatePhase.Serialize);
			updateSystem.UpdateAt<RoadBuilderUpdateSystem>(SystemUpdatePhase.Modification1);
			updateSystem.UpdateAt<RoadBuilderSystem>(SystemUpdatePhase.Modification1);
			updateSystem.UpdateAt<RoadBuilderApplyTagSystem>(SystemUpdatePhase.Modification1);
			updateSystem.UpdateAt<RoadBuilderToolSystem>(SystemUpdatePhase.ToolUpdate);
			updateSystem.UpdateAt<RoadBuilderUISystem>(SystemUpdatePhase.UIUpdate);
			updateSystem.UpdateAt<NetSectionsUISystem>(SystemUpdatePhase.UIUpdate);
		}

		public void OnDispose()
		{
			Log.Info(nameof(OnDispose));

			Settings?.UnregisterInOptionsUI();
		}
	}
}