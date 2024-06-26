﻿using Colossal.PSI.Common;

using Game.Prefabs;

using HarmonyLib;

using RoadBuilder.Domain;
using RoadBuilder.Domain.Configuration;
using RoadBuilder.Domain.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

using Unity.Mathematics;

namespace RoadBuilder.Utilities
{
	public class RoadPrefabGenerationUtil
	{
		private readonly RoadGenerationData _roadGenerationData;

		public RoadBuilderPrefab RoadPrefab { get; }

		public RoadPrefabGenerationUtil(RoadBuilderPrefab prefab, RoadGenerationData roadGenerationData)
		{
			_roadGenerationData = roadGenerationData;

			RoadPrefab = prefab;
		}

		public void GenerateRoad()
		{
			var cfg = RoadPrefab.Config;

			RoadPrefab.isDirty = true;
			RoadPrefab.active = true;
			RoadPrefab.name = cfg.ID;
			RoadPrefab.m_SpeedLimit = cfg.SpeedLimit;
			RoadPrefab.m_RoadType = cfg.Category.HasFlag(RoadCategory.PublicTransport) ? RoadType.PublicTransport : RoadType.Normal;
			RoadPrefab.m_TrafficLights = cfg.GeneratesTrafficLights;
			RoadPrefab.m_HighwayRules = cfg.Category.HasFlag(RoadCategory.Highway);
			RoadPrefab.m_MaxSlopeSteepness = cfg.MaxSlopeSteepness;
			RoadPrefab.m_InvertMode = CompositionInvertMode.FlipLefthandTraffic;
			RoadPrefab.m_ZoneBlock = cfg.GeneratesZoningBlocks ? _roadGenerationData.ZoneBlockPrefab : null;
			RoadPrefab.m_AggregateType = _roadGenerationData.AggregateNetPrefabs.TryGetValue(cfg.AggregateType, out var aggregate) ? aggregate : null;
			//RoadPrefab.m_NodeStates = new NetNodeStateInfo[basePrefab.m_NodeStates.Length];
			//RoadPrefab.m_EdgeStates = new NetEdgeStateInfo[basePrefab.m_EdgeStates.Length];
			RoadPrefab.m_Sections = GenerateSections().ToArray();

			RoadPrefab.components.Clear();
			RoadPrefab.components.AddRange(GenerateComponents());

			if (RoadPrefab.WasGenerated)
			{
				RoadPrefab.Config.ID = $"{PlatformManager.instance.userSpecificPath}-{Guid.NewGuid()}";
			}
		}

		private IEnumerable<NetSectionInfo> GenerateSections()
		{
			return new NetSectionInfo[0];
		}

		private IEnumerable<ComponentBase> GenerateComponents()
		{
			return new ComponentBase[0];
			//yield return new ServiceObject { }
		}

		public void DummyGenerateRoad(RoadBuilderPrefab prefab)
		{
			var prefabs = new List<PrefabBase>();//Traverse.Create(_prefabSystem).Field<List<PrefabBase>>("m_Prefabs").Value;
			RoadPrefab basePrefab = (RoadPrefab)prefabs.FirstOrDefault(p => p.name == "Small Road");
			NetSectionPrefab roadLanePrefab = (NetSectionPrefab)prefabs.FirstOrDefault(p => p.name == "Car Drive Section 3");
			NetSectionPrefab sidewalk5Prefab = (NetSectionPrefab)prefabs.FirstOrDefault(p => p.name == "Sidewalk 5");

			ComponentBase[] baseComponents = new ComponentBase[basePrefab.components.Count];
			basePrefab.components.CopyTo(baseComponents);

			prefab.m_SpeedLimit = 80;
			prefab.m_RoadType = RoadType.Normal;
			prefab.m_TrafficLights = false;
			prefab.m_HighwayRules = false;
			prefab.m_MaxSlopeSteepness = 0.2f;
			prefab.m_InvertMode = CompositionInvertMode.FlipLefthandTraffic;
			prefab.isDirty = true;
			prefab.active = true;
			prefab.m_Sections = new NetSectionInfo[basePrefab.m_Sections.Length];
			prefab.m_ZoneBlock = basePrefab.m_ZoneBlock;
			prefab.m_AggregateType = basePrefab.m_AggregateType;
			prefab.m_NodeStates = new NetNodeStateInfo[basePrefab.m_NodeStates.Length];
			prefab.m_EdgeStates = new NetEdgeStateInfo[basePrefab.m_EdgeStates.Length];

			prefab.components.AddRange(baseComponents);
			basePrefab.m_Sections.CopyTo(prefab.m_Sections, 0);
			basePrefab.m_NodeStates.CopyTo(prefab.m_NodeStates, 0);
			basePrefab.m_EdgeStates.CopyTo(prefab.m_EdgeStates, 0);
			prefab.name = "Test Road Prefab";
			prefab.m_Sections[1].m_Section = sidewalk5Prefab;
			prefab.m_Sections[5].m_Section = sidewalk5Prefab;
			prefab.m_Sections = new NetSectionInfo[]
			{
				prefab.m_Sections[0],
				prefab.m_Sections[1],
				new NetSectionInfo()
				{
					m_Section = roadLanePrefab,
					m_Flip = false,
					m_Invert = true,
					m_RequireAll = new NetPieceRequirements[0],
					m_RequireAny = new NetPieceRequirements[0],
					m_RequireNone = new NetPieceRequirements[0],
					m_Median = false,
					m_Offset = float3.zero
				},
				prefab.m_Sections[2],
				prefab.m_Sections[3],
				prefab.m_Sections[4],
				new NetSectionInfo()
				{
					m_Section = roadLanePrefab,
					m_Flip = false,
					m_Invert = false,
					m_RequireAll = new NetPieceRequirements[0],
					m_RequireAny = new NetPieceRequirements[0],
					m_RequireNone = new NetPieceRequirements[0],
					m_Median = false,
					m_Offset = float3.zero
				},
				prefab.m_Sections[5],
				prefab.m_Sections[6]
			};
		}
	}
}
