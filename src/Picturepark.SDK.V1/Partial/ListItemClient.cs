﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Picturepark.SDK.V1.Contract;
using Picturepark.SDK.V1.Contract.Authentication;
using Picturepark.SDK.V1.Contract.Extensions;
using Picturepark.SDK.V1.Contract.Interfaces;

namespace Picturepark.SDK.V1
{
	public partial class ListItemClient
	{
		private readonly TransferClient _transferClient;

		public ListItemClient(TransferClient transferClient, IAuthClient authClient) : this(authClient)
		{
			_transferClient = transferClient;
			BaseUrl = transferClient.BaseUrl;
		}

		/// <exception cref="ApiException">A server side error occurred.</exception>
		public async Task<ListItemDetailViewItem> CreateAsync(ListItemCreateRequest listItem, bool resolve = false, int timeout = 60000)
		{
			return await CreateAsync(listItem, resolve, timeout, null);
		}

		public ListItemDetailViewItem Create(ListItemCreateRequest listItem, bool resolve = false, int timeout = 60000)
		{
			return Task.Run(async () => await CreateAsync(listItem, resolve: resolve, timeout: timeout)).GetAwaiter().GetResult();
		}

		// TODO(rsu): Rename
		public async Task<ListItemViewItem> CreateAbcAsync(ListItemCreateRequest createRequest)
		{
			var result = await CreateManyAsync(new List<ListItemCreateRequest> { createRequest });
			return result.First();
		}

		public async Task DeleteAsync(string objectId, CancellationToken cancellationToken = default(CancellationToken))
		{
			await DeleteAsync(objectId, 60000, cancellationToken);
		}

		public void Delete(string objectId)
		{
			Task.Run(async () => await DeleteAsync(objectId)).GetAwaiter().GetResult();
		}

		public ListItemDetailViewItem Update(string objectId, ListItemUpdateRequest updateRequest, bool resolve = false, List<string> patterns = null, int timeout = 60000)
		{
			return Task.Run(async () => await UpdateAsync(objectId, updateRequest, resolve, timeout, patterns)).GetAwaiter().GetResult();
		}

		/// <exception cref="ApiException">A server side error occurred.</exception>
		public async Task<ListItemDetailViewItem> UpdateAsync(string objectId, ListItemUpdateRequest updateRequest, bool resolve = false, List<string> patterns = null, int timeout = 60000, CancellationToken cancellationToken = default(CancellationToken))
		{
			return await UpdateAsync(objectId, updateRequest, resolve, timeout, patterns, cancellationToken);
		}

		public async Task<List<ListItemViewItem>> CreateFromPOCO(object obj, string schemaId)
		{
			var listItems = new List<ListItemCreateRequest>();
			var metadata = new MetadataDictionary();
			metadata[schemaId] = obj;

			var referencedObjects = await CreateReferencedObjects(obj);

			listItems.Add(new ListItemCreateRequest
			{
				SchemaId = schemaId,
				Metadata = metadata
			});
			var objectResult = await CreateManyAsync(listItems);

			var allResults = objectResult.Concat(referencedObjects).ToList();
			return allResults;
		}

		public IEnumerable<ListItemViewItem> CreateMany(IEnumerable<ListItemCreateRequest> objects)
		{
			return Task.Run(async () => await CreateManyAsync(objects)).GetAwaiter().GetResult();
		}

		/// <exception cref="ApiException">A server side error occurred.</exception>
		public async Task<IEnumerable<ListItemViewItem>> CreateManyAsync(IEnumerable<ListItemCreateRequest> listItems, CancellationToken? cancellationToken = null)
		{
			if (listItems.Any())
			{
				var createRequest = await CreateManyCoreAsync(listItems, cancellationToken ?? CancellationToken.None);
				var result = await createRequest.Wait4MetadataAsync(this);

				var bulkResult = result.BusinessProcess as BusinessProcessBulkResponseViewItem;
				if (bulkResult.Response.Rows.Any(i => i.Succeeded == false))
					throw new Exception("Could not save all objects");

				// Fetch created objects
				var searchResult = await SearchAsync(new ListItemSearchRequest
				{
					Start = 0,
					Limit = 1000,
					Filter = new TermsFilter
					{
						Field = "Id",
						Terms = bulkResult.Response.Rows.Select(i => i.Id).ToList()
					}
				});

				return searchResult.Results;
			}
			else
			{
				return new List<ListItemViewItem>();
			}
		}

		public async Task UpdateListItemAsync(ListItemUpdateRequest updateRequest)
		{
			var result = await UpdateManyAsync(new List<ListItemUpdateRequest>() { updateRequest });
			var wait = await result.Wait4MetadataAsync(this);
		}

		public async Task UpdateListItemAsync(ListItemDetailViewItem listItem, object obj, string schemaId)
		{
			var convertedObject = new ListItemViewItem
			{
				ContentSchemaId = listItem.ContentSchemaId,
				EntityType = listItem.EntityType,
				Id = listItem.Id,
				Metadata = listItem.Metadata,
				SchemaIds = listItem.SchemaIds
			};
			await UpdateListItemAsync(convertedObject, obj, schemaId);
		}

		public async Task UpdateListItemAsync(ListItemViewItem listItem, object obj, string schemaId)
		{
			var metadata = new MetadataDictionary();
			metadata[schemaId] = obj;

			var request = new ListItemUpdateRequest()
			{
				Id = listItem.Id,
				Metadata = metadata,
				SchemaIds = listItem.SchemaIds
			};

			await UpdateListItemAsync(request);
		}

		public async Task<T> GetObjectAsync<T>(string objectId, string schemaId)
		{
			var metadataViewItem = await GetAsync(objectId, true);
			return metadataViewItem.Metadata.Get<T>(schemaId);
		}

		public async Task ImportFromJsonAsync(string jsonFilePath, bool includeObjects)
		{
			var filePaths = new List<string>() { jsonFilePath };

			var batchName = "Metadata import: " + Path.GetFileName(jsonFilePath);
			List<string> fileNames = filePaths.Select(file => Path.GetFileName(file)).ToList();

			// Create batch
			TransferViewItem transfer = await _transferClient.CreateBatchAsync(fileNames, batchName);

			// Upload files
			string directoryPath = Path.GetDirectoryName(filePaths.First());

			await _transferClient.UploadFilesAsync(
				filePaths,
				directoryPath,
				transfer,
				successDelegate: (file) => { Debug.WriteLine(file); },
				errorDelegate: (error) => { Debug.WriteLine(error); }
				);

			// Import metadata
			string fileTransferId = await _transferClient.GetFileTransferIdFromBatchTransferId(transfer.Id);
			await ImportAsync(null, fileTransferId, includeObjects);
		}

		private bool IsSimpleType(Type type)
		{
			return
				type.GetTypeInfo().IsValueType ||
				type.GetTypeInfo().IsPrimitive ||
				new Type[]
				{
			typeof(string),
			typeof(decimal),
			typeof(DateTime),
			typeof(DateTimeOffset),
			typeof(TimeSpan),
			typeof(Guid)
				}.Contains(type) ||
				Convert.GetTypeCode(type) != TypeCode.Object;
		}

		private async Task<IEnumerable<ListItemViewItem>> CreateReferencedObjects(object obj)
		{
			var referencedListItems = new List<ListItemCreateRequest>();
			BuildReferencedListItems(obj, referencedListItems);

			// Assign Ids on ObjectCreation
			foreach (var referencedObject in referencedListItems)
			{
				referencedObject.ListItemId = Guid.NewGuid().ToString("N");
			}

			var results = await CreateManyAsync(referencedListItems);

			foreach (var result in results)
			{
				var object2Update = referencedListItems.SingleOrDefault(i => i.ListItemId == result.Id);
				var reference = (object2Update.Metadata as Dictionary<string, object>)[object2Update.SchemaId] as IReference;
				reference.refId = result.Id;
			}

			return results;
		}

		private void BuildReferencedListItems(object obj, List<ListItemCreateRequest> referencedListItems)
		{
			// Scan child properties for references
			var nonReferencedProperties = obj.GetType().GetProperties().Where(i => !typeof(IReference).IsAssignableFrom(i.PropertyType.GenericTypeArguments.FirstOrDefault()) && !typeof(IReference).IsAssignableFrom(i.PropertyType));
			foreach (var property in nonReferencedProperties.Where(i => !IsSimpleType(i.PropertyType)))
			{
				if (property.PropertyType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IList)))
				{
					foreach (var value in (IList)property.GetValue(obj))
					{
						BuildReferencedListItems(value, referencedListItems);
					}
				}
				else
				{
					BuildReferencedListItems(property.GetValue(obj), referencedListItems);
				}
			}

			var referencedProperties = obj.GetType().GetProperties().Where(i => typeof(IReference).IsAssignableFrom(i.PropertyType.GenericTypeArguments.FirstOrDefault()) || typeof(IReference).IsAssignableFrom(i.PropertyType));
			foreach (var referencedProperty in referencedProperties)
			{
				if (referencedProperty.PropertyType.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IList)))
				{
					// SchemaItems
					var values = (IList)referencedProperty.GetValue(obj);
					if (values != null)
					{
						foreach (var value in values)
						{
							var refIdValue = (string)value.GetType().GetProperty("refId").GetValue(value);
							if (string.IsNullOrEmpty(refIdValue))
							{
								var schemaId = value.GetType().Name;
								var metadata = new MetadataDictionary();
								metadata[schemaId] = value;

								// Add metadata object if it does not already exist
								if (referencedListItems.Where(i => i.SchemaId == schemaId).Select(i => i.Metadata).All(i => i[schemaId] != value))
								{
									referencedListItems.Insert(0, new ListItemCreateRequest
									{
										SchemaId = schemaId,
										Metadata = metadata
									});
								}
							}
						}
					}
				}
				else
				{
					// SchemaItem
					// TODO(rsu): Always false?
					if (referencedProperty.GetType().GetProperty("refId") == null)
					{
						var value = referencedProperty.GetValue(obj);
						if (value != null)
						{
							var schemaId = value.GetType().Name;
							var metadata = new MetadataDictionary();
							metadata[schemaId] = value;

							// Add metadata object if it does not already exist
							if (referencedListItems.Where(i => i.SchemaId == schemaId).Select(i => i.Metadata).All(i => i[schemaId] != value))
							{
								referencedListItems.Insert(0, new ListItemCreateRequest
								{
									SchemaId = schemaId,
									Metadata = metadata
								});
							}
						}
					}
				}
			}
		}
	}
}
