﻿using Game.Prefabs;

using RoadBuilder.Domain.Components;
using RoadBuilder.Domain.Components.Prefabs;

using System.Collections.Generic;

using Unity.Entities;

namespace RoadBuilder.Domain.Prefabs
{
	public class LaneGroupPrefab : PrefabBase
	{
		public RoadBuilderLaneOption[]? Options;

		internal List<NetSectionPrefab> LinkedSections { get; set; } = new();
		internal bool RoadBuilder { get; set; }

		public override void GetPrefabComponents(HashSet<ComponentType> components)
		{
			base.GetPrefabComponents(components);

			components.Add(ComponentType.ReadWrite<RoadBuilderLaneGroupPrefabData>());
		}
	}
}
