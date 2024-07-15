﻿using Game;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;

using RoadBuilder.Domain.Components;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.LaneGroups;

using System;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Entities;

namespace RoadBuilder.Systems
{
	public partial class NetSectionsSystem : GameSystemBase
	{
		private PrefabSystem prefabSystem;
		private EntityQuery prefabQuery;
		private bool customGroupsAdded;

		public event Action SectionsAdded;

		public Dictionary<string, NetSectionPrefab> NetSections { get; } = new();
		public Dictionary<string, LaneGroupPrefab> LaneGroups { get; } = new();

		protected override void OnCreate()
		{
			base.OnCreate();

			prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
			prefabQuery = SystemAPI.QueryBuilder().WithAll<Created, PrefabData, NetSectionData>().Build();

			RequireForUpdate(prefabQuery);
		}

		protected override void OnUpdate()
		{
			var entities = prefabQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < entities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<NetSectionPrefab>(entities[i], out var prefab))
				{
					NetSections[prefab.name] = prefab;

					if (_blacklist.Contains(prefab.name))
					{
						prefab.AddOrGetComponent<RoadBuilderHideComponent>();
					}
				}
			}

			if (!customGroupsAdded && NetSections.Count > 0)
			{
				AddCustomGroups();

				GameManager.instance.localizationManager.ReloadActiveLocale();

				customGroupsAdded = true;
			}

			SectionsAdded?.Invoke();
		}

		private void AddCustomGroups()
		{
			foreach (var type in typeof(NetSectionsSystem).Assembly.GetTypes())
			{
				if (typeof(BaseLaneGroupPrefab).IsAssignableFrom(type) && !type.IsAbstract)
				{
					var prefab = (BaseLaneGroupPrefab)Activator.CreateInstance(type, NetSections);

					prefab.name = type.FullName;

					prefabSystem.AddPrefab(prefab);

					LaneGroups[prefab.name] = prefab;
				}
			}
		}

		#region Blacklist
		private readonly HashSet<string> _blacklist = new()
		{
			"Missing Net Section",
			"Alley Side 0",
			"Road Side 0",
			"Highway Side 0",
			"Gravel Side 0",
			"Tiled Side 0",
			"Subway Side 0",
			"Train Side 0",
			"Pavement Path Side Section 0",
			"Golden Gate Sidewalk",
			"Golden Gate Drive",
			"Golden Gate Bridge",
			"Ground Cable Section 1",
			"Invisible Edge Section 0",
			"Invisible Pedestrian Section 2",
			"Invisible Pedestrian Section 0.5",
			"Invisible Boarding Section 0",
			"Invisible Car Oneway Section 3",
			"Invisible Median Section 0",
			"Invisible Parking Section 5.5",
			"Invisible Car Twoway Section 3",
			"Small Sewage Marker Section",
			"Low-voltage Marker Section - Small",
			"Small Water Marker Section",
			"Invisible Car Bay Section 3",
			"Invisible Airplane Airspace",
			"Invisible Helicopter Twoway Section",
			"Invisible Helicopter Edge Section 0",
			"Invisible Airplane Twoway Section",
			"Invisible Airplane Oneway Section",
			"Invisible Airplane Runway Section",
			"Invisible Airplane Edge Section 0",
			"Waterway Median Section 0",
			"Ship Drive Section 50",
			"Ship Drive Section 50 - Outermost",
			"Seaway Edge Section 5",
			"Water Pipe Section 3",
			"Water Pipe Section 1.5",
			"Water Pipe Section 1",
			"Stormwater Pipe Section 1.5",
			"Pipeline Spacing Section 1",
			"Pipeline Spacing Section 1",
			"Sewage Pipe Section 4",
			"Sewage Pipe Section 2",
			"Sewage Pipe Section 1.5",
			"Low-voltage Marker Section",
			"High-voltage Marker Section",
			"Hydroelectric_Power_Plant_01 Dam Section",
			"Ground Cable Section 8",
			"Ground Cable Section 1.5",
			"Low-voltage Cables",
			"High-voltage Cables",
			"All-way Stop",
			"Traffic Lights",
			"Wide Sidewalk",
			"Wooden Covered Bridge Shoulder",
			"2-Lane Wooden Covered Bridge",
			"2-Lane Truss Arch Bridge",
			"Highway Shoulder 2",
			"2-Lane Suspension Bridge",
			"3-Lane Suspension Bridge",
			"4-Lane Suspension Bridge",
			"5-Lane Suspension Bridge",
			"4-Lane Tied Arch Bridge 00",
			"8-Lane Cable Stayed Bridge 00",
			"6-Lane Extradosed Bridge",
			"Grand Bridge",
			"Cable Stayed Pedestrian Bridge",
			"Covered Pedestrian Bridge",
			"Arc Pedestrian Bridge",
		};
		#endregion
	}
}
