﻿using Colossal.Serialization.Entities;

using RoadBuilder.Systems;

namespace RoadBuilder.Domain.Configuration
{
	public class LaneConfig : ISerializable
	{
		public string SectionPrefabName;

		public void Deserialize<TReader>(TReader reader) where TReader : IReader
		{
			reader.Read(out int version);
			reader.Read(out SectionPrefabName);
		}

		public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
		{
			writer.Write(RoadBuilderSystem.CURRENT_VERSION);
			writer.Write(SectionPrefabName);
		}
	}
}
