﻿using Newtonsoft.Json;

namespace Picturepark.SDK.V1.ServiceProvider
{
	public class Configuration
	{
		public Configuration()
		{
			SerializerSettings = new JsonSerializerSettings();
			SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter { CamelCaseText = false });
			SerializerSettings.Culture = System.Globalization.CultureInfo.InvariantCulture;
			SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
			SerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;
		}

		public string Port { get; set; }

		public string Host { get; set; }

		public string User { get; set; }

		public string Password { get; set; }

		public string ServiceProviderId { get; set; }

		public string NodeId { get; set; }

		public JsonSerializerSettings SerializerSettings { get; set; }

		public int DefaultQueuePriorityMax { get; set; } = 200;
	}
}
