﻿using Game.Prefabs;

using RoadBuilder.Domain.Configurations;

namespace RoadBuilder.Domain.Prefabs
{
	public class RoadBuilderPrefab : RoadPrefab, INetworkBuilderPrefab
	{
		public RoadConfig Config { get; set; }
		NetGeometryPrefab INetworkBuilderPrefab.Prefab => this;
		INetworkConfig INetworkBuilderPrefab.Config { get => Config; set => Config = value as RoadConfig; }

		public RoadBuilderPrefab(RoadConfig config)
		{
			Config = config;
			name = config.ID;

			m_Sections = new NetSectionInfo[0];
		}
	}
}
