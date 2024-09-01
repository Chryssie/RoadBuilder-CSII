﻿using Colossal.Serialization.Entities;

using RoadBuilder.Domain.Enums;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Systems;

using System;
using System.Collections.Generic;

using static RoadBuilder.Systems.RoadBuilderSerializeSystem;

namespace RoadBuilder.Domain.Configurations
{
	public class TrackConfig : INetworkConfig
	{
		public string Type { get; set; }
		public ushort Version { get; set; }
		public string OriginalID { get; set; }
		public string ID { get; set; }
		public string Name { get; set; }
		public string PillarPrefabName { get; set; }
		public float SpeedLimit { get; set; }
		public float MaxSlopeSteepness { get; set; }
		public RoadCategory Category { get; set; }
		public RoadAddons Addons { get; set; }
		public List<LaneConfig> Lanes { get; set; } = new();
		public ShowInToolbarState ToolbarState { get; set; }

		public void Deserialize<TReader>(TReader reader) where TReader : IReader
		{
			reader.Read(out string iD);
			reader.Read(out string name);

			if (Version < VER_REMOVE_AGGREGATE_TYPE)
			{
				reader.Read(out string _);
			}

			reader.Read(out string pillarPrefabName);
			reader.Read(out float speedLimit);
			reader.Read(out float maxSlopeSteepness);
			reader.Read(out ulong category);
			reader.Read(out ulong addons);

			var toolbarState = 0;

			if (Version >= VER_ADD_TOOLBAR_STATE)
			{
				reader.Read(out toolbarState);
			}

			ID = iD;
			Name = name;
			PillarPrefabName = pillarPrefabName;
			SpeedLimit = speedLimit;
			MaxSlopeSteepness = maxSlopeSteepness;
			Category = (RoadCategory)category;
			Addons = (RoadAddons)addons;
			ToolbarState = (ShowInToolbarState)toolbarState;

			reader.Read(out int laneCount);

			for (var i = 0; i < laneCount; i++)
			{
				var lane = new LaneConfig { Version = Version };

				reader.Read(lane);

				Lanes.Add(lane);
			}

			OriginalID = ID;
		}

		public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
		{
			writer.Write(ID);
			writer.Write(Name);
			writer.Write(PillarPrefabName ?? string.Empty);
			writer.Write(SpeedLimit);
			writer.Write(MaxSlopeSteepness);
			writer.Write((ulong)Category);
			writer.Write((ulong)Addons);
			writer.Write((int)ToolbarState);

			writer.Write(Lanes.Count);

			foreach (var lane in Lanes)
			{
				writer.Write(lane);
			}
		}

		public void ApplyVersionChanges()
		{

		}

		public Type GetPrefabType()
		{
			return typeof(TrackBuilderPrefab);
		}
	}
}
