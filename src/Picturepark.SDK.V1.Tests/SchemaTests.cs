using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Picturepark.SDK.V1.Tests.Contracts;
using Picturepark.SDK.V1.Contract;
using Picturepark.SDK.V1.Tests.Fixtures;
using Newtonsoft.Json;
using System.Reflection;

namespace Picturepark.SDK.V1.Tests
{
	public class SchemaTests : IClassFixture<SDKClientFixture>
	{
		private readonly SDKClientFixture _fixture;
		private readonly PictureparkClient _client;

		public SchemaTests(SDKClientFixture fixture)
		{
			_fixture = fixture;
			_client = _fixture.Client;
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldCreateAllTypesSchemaFromClass()
		{
			var schemas = new List<SchemaDetailViewItem>();

			var allTypes = _client.Schemas.GenerateSchemaFromPOCO(typeof(AllDataTypesContract), schemas, true);

			foreach (var schema in allTypes)
			{
				if (await _client.Schemas.ExistsAsync(schema.Id) == false)
				{
					await _client.Schemas.CreateAsync(schema, true);
				}
			}
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldCreateFromClass()
		{
			var schemas = new List<SchemaDetailViewItem>();

			var person = _client.Schemas.GenerateSchemaFromPOCO(typeof(Person), schemas, true);

			// Expect child schemas and referenced schemas to exist
			Assert.Equal(person.Count, 8);

			foreach (var schema in person)
			{
				if (await _client.Schemas.ExistsAsync(schema.Id) == false)
				{
					await _client.Schemas.CreateAsync(schema, true);
				}
			}

			var generatedPersonSchema = await _client.Schemas.GetAsync("Person");
			Assert.Contains(generatedPersonSchema.Types, i => i == SchemaType.List || i == SchemaType.Struct );

			var schemasFromPersonShot = new List<SchemaDetailViewItem>();
			var personShot = _client.Schemas.GenerateSchemaFromPOCO(typeof(PersonShot), schemasFromPersonShot, true);

			// Expect child schemas and referenced schemas to exist
			Assert.Equal(personShot.Count, 9);

			foreach (var schema in personShot)
			{
				if (await _client.Schemas.ExistsAsync(schema.Id) == false)
				{
					await _client.Schemas.CreateAsync(schema, true);
				}
			}
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldDelete()
		{
			var schemas = new List<SchemaDetailViewItem>();
			var tags = _client.Schemas.GenerateSchemaFromPOCO(typeof(Tag), schemas, true);

			Assert.Equal(tags.Count, 1);

			var tag = tags.First();
			tag.Id = "SchemaToDelete" + new Random().Next(0, 999999);

			await _client.Schemas.CreateAsync(tag, false);

			string schemaId = tag.Id;

			SchemaDetailViewItem schemaDetailViewItem = await _client.Schemas.GetAsync(schemaId);
			await _client.Schemas.DeleteAsync(schemaDetailViewItem.Id);
			SchemaDetailViewItem deletedSchemaDetailViewItem = null;

			try
			{
				deletedSchemaDetailViewItem = await _client.Schemas.GetAsync(schemaId);
			}
			catch (Exception)
			{
				// ignored
			}

			Assert.True(deletedSchemaDetailViewItem == null);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldExist()
		{
			string schemaId = _fixture.GetRandomSchemaId(20);

			bool schemaExists = await _client.Schemas.ExistsAsync(schemaId);
			Assert.True(schemaExists);

			schemaExists = await _client.Schemas.ExistsAsync("abcabcabcabc");
			Assert.False(schemaExists);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldGet()
		{
			var request = new SchemaSearchRequest() { Start = 0, Limit = 100 };
			BaseResultOfSchemaViewItem result = _client.Schemas.SearchAsync(request).Result;
			Assert.True(result.Results.Any());

			List<string> schemaIds = result.Results.Select(i => i.Id).OrderBy(i => i).ToList();
			var schemaIdsOk = new List<string>();
			var schemaIdsNotOk = new List<string>();

			foreach (var schemaId in schemaIds)
			{
				try
				{
					var schema = await _client.Schemas.GetAsync(schemaId);
					schemaIdsOk.Add(schema.Id);
				}
				catch
				{
					schemaIdsNotOk.Add(schemaId);
				}
			}

			Assert.False(schemaIdsNotOk.Count > 0);
			Assert.True(schemaIdsOk.Count > 0);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldGetJsonValidationSchema()
		{
			var schemaId = _fixture.GetRandomSchemaId(20);
			Assert.False(string.IsNullOrEmpty(schemaId));

			var result = await _client.JsonSchemas.GetAsync(schemaId);
			var stringResult = result.ToString(Formatting.Indented);
			Assert.NotNull(result);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldCreateSchemaAndValidateFilter()
		{
			await ShouldCreateFromClassGeneric(typeof(SoccerPlayer));

			var generatedSoccerPlayerSchema = await _client.Schemas.GetAsync("SoccerPlayer");

			string filterString = "{\"Kind\":\"TermFilter\",\"Field\":\"ContentType\",\"Term\":\"FC Aarau\"}";

			var jsonConvertedField = generatedSoccerPlayerSchema.Fields[0].ToJson();

			bool containsString = jsonConvertedField.Contains(filterString);

			Assert.Equal(true, containsString);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldCreateSchemaAndValidateMultiline()
		{
			await ShouldCreateFromClassGeneric(typeof(Person));

			string multilineString = "\"MultiLine\":true";

			var generatedSoccerPlayerSchema = await _client.Schemas.GetAsync("Person");

			var jsonConvertedField = generatedSoccerPlayerSchema.Fields[0].ToJson();

			bool containsString = jsonConvertedField.Contains(multilineString);

			Assert.Equal(true, containsString);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldCreateSchemaAndValidateMaxRecursion()
		{
			await ShouldCreateFromClassGeneric(typeof(Person));
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldSearch()
		{
			var searchRequest = new SchemaSearchRequest
			{
				Limit = 12,
				SearchString = "D*"
			};

			BaseResultOfSchemaViewItem result = await _client.Schemas.SearchAsync(searchRequest);
			Assert.True(result.Results.Any());
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task ShouldUpdate()
		{
			string schemaId = _fixture.GetRandomSchemaId(20);
			SchemaDetailViewItem schemaDetailViewItem = await _client.Schemas.GetAsync(schemaId);

			string outString;
			string language = "es";

			schemaDetailViewItem.Names.Remove(language);
			schemaDetailViewItem.Names.Add(language, schemaId);

			await _client.Schemas.UpdateAsync(schemaDetailViewItem);

			SchemaDetailViewItem updatedSchema = await _client.Schemas.GetAsync(schemaId);

			updatedSchema.Names.TryGetValue(language, out outString);
			Assert.True(outString == schemaId);
		}

		public async Task ShouldCreateFromClassGeneric(Type type)
		{
			var schemas = new List<SchemaDetailViewItem>();

			var childSchemas = _client.Schemas.GenerateSchemaFromPOCO(type, schemas, true);

			foreach (var schema in childSchemas)
			{
				if (await _client.Schemas.ExistsAsync(schema.Id) == false)
				{
					await _client.Schemas.CreateAsync(schema, true);
				}
			}

			var generatedPersonSchema = await _client.Schemas.GetAsync(type.Name);
			Assert.Contains(generatedPersonSchema.Types, i => i == SchemaType.List || i == SchemaType.Struct);
		}

		[Fact]
		[Trait("Stack", "Schema")]
		public async Task SchemaReferencing()
		{
			List<Type> listTypesToUpdate = new List<Type>() { typeof(Person), typeof(PersonDetails) };
			List<Type> listTypesToCreate = new List<Type>();
			List<Type> listExistingTypes = new List<Type>();

			listTypesToCreate.AddRange(listTypesToUpdate);

			Assembly assembly = typeof(Person).GetTypeInfo().Assembly;

			// Get all types from SDK
			foreach (Type type in assembly.GetTypes())
			{
				listExistingTypes.Add(type);
			}

			// Check in Picturepark
			foreach (Type type in listTypesToUpdate)
			{
				string schemaName = type.Name;

				var searchRequest = new SchemaSearchRequest
				{
					Limit = 20,
					SearchString = schemaName
				};

				BaseResultOfSchemaViewItem result = await _client.Schemas.SearchAsync(searchRequest);

				if (result.TotalResults < 1)
				{
					if (!listExistingTypes.Contains(type))
					{
						throw new Exception("Missing type exception.");
					}
				}
				else
				{
					listTypesToCreate.Remove(type);
				}

				if (!listExistingTypes.Contains(type))
				{
					listExistingTypes.Add(type);
				}
			}

			// Check if all needed types exist
			foreach (var type in listTypesToUpdate)
			{
				if (!listExistingTypes.Contains(type))
				{
					throw new Exception("Missing type exception.");
				}
			}

			// Create all necessary types
			foreach (Type type in listTypesToCreate)
			{
				await ShouldCreateFromClassGeneric(type);
			}

			// Update with references here
		}
	}
}