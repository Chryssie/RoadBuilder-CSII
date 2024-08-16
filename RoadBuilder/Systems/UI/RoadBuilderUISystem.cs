﻿using Game.City;
using Game.Input;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;

using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Configurations;
using RoadBuilder.Domain.Enums;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Domain.UI;
using RoadBuilder.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Unity.Entities;

using UnityEngine;

namespace RoadBuilder.Systems.UI
{
	public partial class RoadBuilderUISystem : ExtendedUISystemBase
	{
		private Entity workingEntity;
		private INetworkConfig workingConfig;

		private PrefabSystem prefabSystem;
		private PrefabUISystem prefabUISystem;
		private ToolSystem toolSystem;
		private RoadBuilderSystem roadBuilderSystem;
		private RoadBuilderGenerationDataSystem roadGenerationDataSystem;
		private RoadBuilderToolSystem roadBuilderToolSystem;
		private DefaultToolSystem defaultToolSystem;
		private SimulationSystem simulationSystem;
		private RoadBuilderNetSectionsUISystem netSectionsUISystem;
		private RoadBuilderNetSectionsSystem netSectionsSystem;
		private CityConfigurationSystem cityConfigurationSystem;
		private RoadBuilderConfigurationsUISystem roadBuilderConfigurationsUISystem;

		private ValueBindingHelper<RoadBuilderToolMode> RoadBuilderMode;
		private ValueBindingHelper<string> RoadId;
		private ValueBindingHelper<string> RoadName;
		private ValueBindingHelper<string> RoadTypeName;
		private ValueBindingHelper<RoadLaneUIBinder[]> RoadLanes;
		private ValueBindingHelper<OptionSectionUIEntry[]> RoadOptions;
		private ValueBindingHelper<bool> RoadListView;
		private ValueBindingHelper<bool> IsPaused;
		private ValueBindingHelper<bool> IsCustomRoadSelected;
		private ProxyAction _toolKeyBinding;

		public RoadBuilderToolMode Mode { get => RoadBuilderMode; set => RoadBuilderMode.Value = value; }
		public Entity WorkingEntity => workingEntity;

		protected override void OnCreate()
		{
			base.OnCreate();

			_toolKeyBinding = Mod.Settings.GetAction(nameof(Setting.ToolToggle));
			_toolKeyBinding.shouldBeEnabled = true;

			prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
			prefabUISystem = World.GetOrCreateSystemManaged<PrefabUISystem>();
			toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
			roadBuilderSystem = World.GetOrCreateSystemManaged<RoadBuilderSystem>();
			roadGenerationDataSystem = World.GetOrCreateSystemManaged<RoadBuilderGenerationDataSystem>();
			roadBuilderToolSystem = World.GetOrCreateSystemManaged<RoadBuilderToolSystem>();
			defaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
			simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			netSectionsUISystem = World.GetOrCreateSystemManaged<RoadBuilderNetSectionsUISystem>();
			netSectionsSystem = World.GetOrCreateSystemManaged<RoadBuilderNetSectionsSystem>();
			cityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			roadBuilderConfigurationsUISystem = World.GetOrCreateSystemManaged<RoadBuilderConfigurationsUISystem>();

			toolSystem.EventToolChanged += OnToolChanged;

			RoadBuilderMode = CreateBinding("RoadBuilderToolMode", RoadBuilderToolMode.None);
			RoadId = CreateBinding("GetRoadId", string.Empty);
			RoadName = CreateBinding("GetRoadName", "SetRoadName", string.Empty, name => UpdateRoad(x => x.Name = name));
			RoadTypeName = CreateBinding("GetRoadTypeName", string.Empty);
			RoadLanes = CreateBinding("GetRoadLanes", new RoadLaneUIBinder[0]);
			RoadOptions = CreateBinding("GetRoadOptions", new OptionSectionUIEntry[0]);
			IsPaused = CreateBinding("IsPaused", simulationSystem.selectedSpeed == 0f);
			RoadListView = CreateBinding("RoadListView", "SetRoadListView", true);
			IsCustomRoadSelected = CreateBinding("IsCustomRoadSelected", false);

			CreateTrigger<RoadLaneUIBinder[]>("SetRoadLanes", x => UpdateRoad(c => UpdateLaneOrder(c, x)));
			CreateTrigger<int, int, int>("RoadOptionClicked", (x, y, z) => UpdateRoad(c => RoadOptionClicked(c, x, y, z)));
			CreateTrigger<int, int, int, int>("OptionClicked", (i, x, y, z) => UpdateRoad(c => LaneOptionClicked(c, i, x, y, z)));
			CreateTrigger<int>("DuplicateLane", x => UpdateRoad(c => DuplicateLane(c, x)));
			CreateTrigger("ToggleTool", ToggleTool);
			CreateTrigger("CreateNewPrefab", () => CreateNewPrefab(workingEntity));
			CreateTrigger("ClearTool", ClearTool);
			CreateTrigger("PickPrefab", PickPrefab);
			CreateTrigger("EditPrefab", EditPrefab);
			CreateTrigger("CancelActionPopup", CancelActionPopup);
		}

		protected override void OnUpdate()
		{
			IsPaused.Value = simulationSystem.selectedSpeed == 0f;
			RoadId.Value = GetWorkingId();

			if (_toolKeyBinding.WasPerformedThisFrame())
			{
				ToggleTool();
			}

			base.OnUpdate();
		}

		private void OnToolChanged(ToolBaseSystem system)
		{
			if (system is not RoadBuilderToolSystem)
			{
				RoadBuilderMode.Value = RoadBuilderToolMode.None;
			}
		}

		private void ToggleTool()
		{
			if (toolSystem.activeTool is RoadBuilderToolSystem)
			{
				ClearTool();
			}
			else
			{
				RoadBuilderMode.Value = RoadBuilderToolMode.Picker;

				toolSystem.selected = Entity.Null;
				toolSystem.activeTool = roadBuilderToolSystem;

				roadBuilderConfigurationsUISystem.UpdateConfigurationList();
			}
		}

		public void ClearTool()
		{
			RoadBuilderMode.Value = RoadBuilderToolMode.None;

			toolSystem.selected = Entity.Null;
			toolSystem.activeTool = defaultToolSystem;
		}

		public void ShowActionPopup(Entity entity, PrefabBase prefab)
		{
			IsCustomRoadSelected.Value = prefab is INetworkBuilderPrefab;
			SetWorkingEntity(entity, RoadBuilderToolMode.ActionSelection);
		}

		public string GetWorkingId()
		{
			if (Mode < RoadBuilderToolMode.Editing)
			{
				return string.Empty;
			}

			if (Mode == RoadBuilderToolMode.EditingNonExistent || workingEntity == Entity.Null)
			{
				return workingConfig?.ID ?? string.Empty;
			}

			if (prefabSystem.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(workingEntity), out var prefab) && prefab is INetworkBuilderPrefab builderPrefab)
			{
				return builderPrefab.Config.ID;
			}

			return string.Empty;
		}

		public void CancelActionPopup()
		{
			SetWorkingEntity(Entity.Null, RoadBuilderToolMode.Picker);
		}

		public void EditPrefab()
		{
			if (workingEntity != Entity.Null)
			{
				SetWorkingEntity(workingEntity, RoadBuilderToolMode.Editing);
			}
		}

		public void PickPrefab()
		{
			if (workingEntity != Entity.Null && prefabSystem.TryGetPrefab<PrefabBase>(EntityManager.GetComponentData<PrefabRef>(workingEntity), out var prefab))
			{
				toolSystem.ActivatePrefabTool(prefab);
			}
		}

		public void CreateNewPrefab(Entity entity)
		{
			SetWorkingEntity(entity, RoadBuilderToolMode.EditingSingle);
		}

		private void SetWorkingEntity(Entity entity, RoadBuilderToolMode mode)
		{
			workingEntity = entity;

			RoadBuilderMode.Value = mode;

			roadBuilderConfigurationsUISystem.UpdateConfigurationList();

			if (mode is RoadBuilderToolMode.Picker)
			{
				workingConfig = null;
				RoadListView.Value = true;
				return;
			}

			var config = roadBuilderSystem.GetOrGenerateConfiguration(workingEntity);

			SetWorkingPrefab(config, mode);
		}

		public void SetWorkingPrefab(INetworkConfig config, RoadBuilderToolMode mode)
		{
			RoadBuilderMode.Value = mode;
			workingConfig = config;

			if (config is null)
			{
				return;
			}

			RoadListView.Value = false;
			SetBindings(config);
		}

		private void UpdateRoad(Action<INetworkConfig> action)
		{
			var createNew = RoadBuilderMode == RoadBuilderToolMode.EditingSingle;
			var nonExistent = RoadBuilderMode == RoadBuilderToolMode.EditingNonExistent;
			var config = nonExistent ? workingConfig : createNew
				? roadBuilderSystem.GenerateConfiguration(workingEntity)
				: roadBuilderSystem.GetOrGenerateConfiguration(workingEntity);

			Mod.Log.Debug("UpdateRoad > " + RoadBuilderMode.Value);

			if (config == null)
			{
				Mod.Log.Warn("Failed to create configuration?");

				return;
			}

			action(config);

			if (nonExistent)
			{
				roadBuilderSystem.UpdateRoad(config, Entity.Null, false);

				workingConfig = config;
			}
			else if (createNew)
			{
				roadBuilderSystem.UpdateRoad(config, workingEntity, true);

				RoadBuilderMode.Value = RoadBuilderToolMode.Editing;
			}
			else
			{
				roadBuilderSystem.UpdateRoad(config, workingEntity, false);
			}

			SetBindings(config);
		}

		private void SetBindings(INetworkConfig config)
		{
			netSectionsUISystem.RefreshEntries(config);
			RoadTypeName.Value = $"RoadBuilder.Config[{config.GetType().Name}]";
			RoadName.Value = config.Name;
			RoadOptions.Value = RoadOptionsUtil.GetRoadOptions(config);
			RoadLanes.Value = From(config);
		}

		private void UpdateLaneOrder(INetworkConfig config, RoadLaneUIBinder[] roadLanes)
		{
			var newLanes = new List<LaneConfig>();

			foreach (var item in roadGenerationDataSystem.RoadGenerationData.LeftHandTraffic ? roadLanes.Reverse() : roadLanes)
			{
				var existingLane = config.Lanes.ElementAtOrDefault(item.Index);

				if (existingLane != null)
				{
					newLanes.Add(existingLane);
				}
				else
				{
					var lane = item.ToLaneConfig();

					if (lane.GroupPrefabName is not null && netSectionsSystem.LaneGroups.TryGetValue(lane.GroupPrefabName, out var group))
					{
						var similarLane = !Mod.Settings.NoImitateLaneOptionsOnPlace ? FindSimilarLane(config.Lanes, newLanes.Count - 1, lane.GroupPrefabName) : null;

						if (similarLane != null)
						{
							lane.GroupOptions = new(similarLane.GroupOptions);
						}

						LaneOptionsUtil.FixGroupOptions(config, lane, group);
					}

					if (newLanes.Count == 0)
					{
						lane.Invert = true;
					}
					else if (newLanes.Count > 1)
					{
						lane.Invert = newLanes[newLanes.Count - 1].Invert;
					}
					else if (config.Lanes.Count > 1)
					{
						lane.Invert = config.Lanes[1].Invert;
					}

					newLanes.Add(lane);
				}
			}

			config.Lanes = newLanes;
		}

		private LaneConfig FindSimilarLane(List<LaneConfig> array, int startIndex, string groupPrefabName)
		{
			if (array.Count == 0 || startIndex < 0 || startIndex >= array.Count)
			{
				return null;
			}

			// Check the starting index first
			if (array[startIndex].GroupPrefabName == groupPrefabName)
			{
				return array[startIndex];
			}

			var left = startIndex - 1;
			var right = startIndex + 1;

			while (left >= 0 || right < array.Count)
			{
				if (left >= 0 && array[left].GroupPrefabName == groupPrefabName)
				{
					return array[left];
				}

				if (right < array.Count && array[right].GroupPrefabName == groupPrefabName)
				{
					return array[right];
				}

				left--;
				right++;
			}

			return null;
		}

		private void DuplicateLane(INetworkConfig config, int index)
		{
			var existingLane = config.Lanes.ElementAtOrDefault(index);

			if (existingLane is null)
			{
				return;
			}

			config.Lanes.Insert(index, new LaneConfig
			{
				SectionPrefabName = existingLane.SectionPrefabName,
				GroupPrefabName = existingLane.GroupPrefabName,
				Invert = existingLane.Invert,
				GroupOptions = new(existingLane.GroupOptions),
			});
		}

		private void RoadOptionClicked(INetworkConfig config, int option, int id, int value)
		{
			RoadOptionsUtil.OptionClicked(config, option, id, value);

			if (!Mod.Settings.AdvancedUserMode)
			{
				config.Lanes.RemoveAll(x =>
				{
					NetworkPrefabGenerationUtil.GetNetSection(roadGenerationDataSystem.RoadGenerationData, config, x, out var section, out var group);

					return !(section?.MatchCategories(config) ?? true) || !(group?.MatchCategories(config) ?? true);
				});
			}
		}

		private void LaneOptionClicked(INetworkConfig config, int index, int option, int id, int value)
		{
			var existingLane = config.Lanes.ElementAtOrDefault(index);

			if (existingLane != null)
			{
				LaneOptionsUtil.OptionClicked(roadGenerationDataSystem.RoadGenerationData, config, existingLane, option, id, value);
			}
		}

		private RoadLaneUIBinder[] From(INetworkConfig config)
		{
			var binders = new RoadLaneUIBinder[config.Lanes.Count];

			for (var i = 0; i < binders.Length; i++)
			{
				var lane = config.Lanes[i];
				var validSection = NetworkPrefabGenerationUtil.GetNetSection(roadGenerationDataSystem.RoadGenerationData, config, lane, out var section, out var groupPrefab);
				var isBackward = cityConfigurationSystem.leftHandTraffic && (i == 0 || i == config.Lanes.Count - 1) ? !lane.Invert : lane.Invert;

				GetThumbnailAndColor(config, lane, section, groupPrefab, isBackward, out var thumbnail, out var color, out var texture);

				binders[cityConfigurationSystem.leftHandTraffic ? (binders.Length - i - 1) : i] = new RoadLaneUIBinder
				{
					Index = i,
					Invert = lane.Invert,
					InvertImage = isBackward,
					TwoWay = validSection && section.SupportsTwoWay(),
					SectionPrefabName = string.IsNullOrEmpty(lane.GroupPrefabName) ? lane.SectionPrefabName : lane.GroupPrefabName,
					IsGroup = !string.IsNullOrEmpty(lane.GroupPrefabName),
					Options = LaneOptionsUtil.GenerateOptions(roadGenerationDataSystem.RoadGenerationData, config, lane),
					Texture = texture ?? "asphalt",
					Color = color is null ? null : $"rgba({color?.r * 255}, {color?.g * 255}, {color?.b * 255}, {color?.a})",
					NetSection = new()
					{
						PrefabName = section?.name ?? groupPrefab?.name,
						IsGroup = !string.IsNullOrEmpty(lane.GroupPrefabName),
						DisplayName = !validSection && groupPrefab is null ? "Unknown Lane" : GetAssetName((PrefabBase)groupPrefab ?? section),
						Width = validSection ? section.CalculateWidth() : 1F,
						Thumbnail = thumbnail,
					}
				};
			}

			return binders;
		}

		private static void GetThumbnailAndColor(INetworkConfig config, LaneConfig lane, NetSectionPrefab section, LaneGroupPrefab groupPrefab, bool invert, out string thumbnail, out Color? color, out string texture)
		{
			thumbnail = null;
			color = null;
			texture = null;

			if (section is null)
			{
				return;
			}

			if (section.TryGet<RoadBuilderLaneDecorationInfo>(out var decorationInfo) && groupPrefab?.Options.FirstOrDefault(x => x.Type is LaneOptionType.Decoration) is RoadBuilderLaneOption decorationOption)
			{
				switch (LaneOptionsUtil.GetSelectedOptionValue(config, lane, decorationOption))
				{
					case "G":
						if (decorationInfo.GrassThumbnail is not null or "")
						{
							thumbnail = decorationInfo.GrassThumbnail;
						}

						break;
					case "T":
						if (decorationInfo.TreeThumbnail is not null or "")
						{
							thumbnail = decorationInfo.TreeThumbnail;
						}

						break;
					case "GT":
						if (decorationInfo.GrassAndTreeThumbnail is not null or "")
						{
							thumbnail = decorationInfo.GrassAndTreeThumbnail;
						}

						break;
				}
			}

			if (section.TryGet<RoadBuilderLaneInfo>(out var sectionInfo))
			{
				if (thumbnail is null && !string.IsNullOrEmpty(invert ? sectionInfo.FrontThumbnail : sectionInfo.BackThumbnail))
				{
					thumbnail = invert ? sectionInfo.FrontThumbnail : sectionInfo.BackThumbnail;
				}

				if (sectionInfo.LaneColor != default)
				{
					color = sectionInfo.LaneColor;
				}
			}

			if (thumbnail is null && ImageSystem.GetIcon(section) is string sectionIcon)
			{
				thumbnail = sectionIcon;
			}

			if (groupPrefab?.TryGet<RoadBuilderLaneInfo>(out var groupInfo) ?? false)
			{
				if (thumbnail is null && !string.IsNullOrEmpty(invert ? groupInfo.FrontThumbnail : groupInfo.BackThumbnail))
				{
					thumbnail = invert ? groupInfo.FrontThumbnail : groupInfo.BackThumbnail;
				}

				if (groupInfo.LaneColor != default && color is null)
				{
					color = groupInfo.LaneColor;
				}
			}

			if (thumbnail is null && groupPrefab is not null)
			{
				thumbnail = ImageSystem.GetIcon(groupPrefab);
			}

			if (color is null)
			{
				if (config.Category.HasFlag(RoadCategory.Gravel))
				{
					texture = "gravel";
					color = new(143 / 255f, 131 / 255f, 97 / 255f);
				}
				else if (config.Category.HasFlag(RoadCategory.Tiled))
				{
					texture = "tiled";
					color = new(76 / 255f, 78 / 255f, 83 / 255f);
				}
			}

			if (section.IsTrainOrSubway())
			{
				texture = "train";
			}
			else if (section.IsBus())
			{
				texture = "bus";
			}
		}

		public string GetLaneName(INetworkConfig config, LaneConfig lane)
		{
			var validSection = NetworkPrefabGenerationUtil.GetNetSection(roadGenerationDataSystem.RoadGenerationData, config, lane, out var section, out var groupPrefab);

			if (validSection)
			{
				return GetAssetName((PrefabBase)groupPrefab ?? section);
			}

			return string.Empty;
		}

		private string GetAssetName(PrefabBase prefab)
		{
			prefabUISystem.GetTitleAndDescription(prefab, out var titleId, out var _);

			if (GameManager.instance.localizationManager.activeDictionary.TryGetValue(titleId, out var name))
			{
				return name;
			}

			return prefab.name.Replace('_', ' ').FormatWords();
		}
	}
}
