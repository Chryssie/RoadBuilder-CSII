﻿using Colossal.PSI.Common;

using RoadBuilder.Domain.API;
using RoadBuilder.Domain.Configurations;

using System.Text;
using System.Threading.Tasks;

namespace RoadBuilder.Utilities.Online
{
	public class ApiUtil
	{
		private readonly ApiUtilBase _apiUtil = new();

		public static ApiUtil Instance { get; } = new();

		public async Task<PagedContent<RoadBuilderEntry>> GetEntries(string query = null, int? category = null, int order = 0, int page = 1)
		{
			return await Get<PagedContent<RoadBuilderEntry>>("/Roads", (nameof(query), query), (nameof(category), category), (nameof(order), order), (nameof(page), page));
		}

		public async Task<ApiResponse> UploadRoad(RoadBuilderEntryPost road)
		{
			return await Post<RoadBuilderEntryPost, ApiResponse>("/SaveRoad", road);
		}

		public async Task<INetworkConfig> GetConfig(string id)
		{
			var data = await Get<byte[]>($"/RoadConfig/{id}.json");

			if (data == null)
			{
				return null;
			}

			return LocalSaveUtil.LoadFromJson(Encoding.UTF8.GetString(data));
		}

		private async Task<T> Get<T>(string url, params (string, object)[] queryParams)
		{
			return await _apiUtil.Get<T>(KEYS.API_URL + url
				, new (string, string)[] { ("USER_ID", PdxModsUtil.UserId), ("IDENTIFIER", PlatformManager.instance.userSpecificPath) }
				, queryParams);
		}

		private async Task<T> Delete<T>(string url, params (string, object)[] queryParams)
		{
			return await _apiUtil.Delete<T>(KEYS.API_URL + url
				, new (string, string)[] { ("USER_ID", PdxModsUtil.UserId), ("IDENTIFIER", PlatformManager.instance.userSpecificPath) }
				, queryParams);
		}

		private async Task<T> Post<TBody, T>(string url, TBody body, params (string, object)[] queryParams)
		{
			return await _apiUtil.Post<TBody, T>(KEYS.API_URL + url
				, body
				, new (string, string)[] { ("USER_ID", PdxModsUtil.UserId), ("IDENTIFIER", PlatformManager.instance.userSpecificPath) }
				, queryParams);
		}
	}
}
