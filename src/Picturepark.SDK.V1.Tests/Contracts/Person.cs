﻿using Newtonsoft.Json;
using Picturepark.SDK.V1.Contract;
using Picturepark.SDK.V1.Contract.Attributes;
using Picturepark.SDK.V1.Contract.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Picturepark.SDK.V1.Tests.Contracts
{
	[JsonConverter(typeof(JsonInheritanceConverter), "Kind")]
	[KnownType(typeof(SoccerPlayer))]
	[KnownType(typeof(SoccerTrainer))]
	[PictureparkSchemaType(SchemaType.List)]
	[PictureparkSchemaType(SchemaType.Struct)]
	[PictureparkDisplayPattern(Contract.DisplayPatternType.Name, Contract.TemplateEngine.DotLiquid, "{{Metadata.Person.Firstname}} {{Metadata.Person.LastName}}")]
	[PictureparkDisplayPattern(Contract.DisplayPatternType.List, Contract.TemplateEngine.DotLiquid, "{{Metadata.Person.Firstname}} {{Metadata.Person.LastName}}, {{Metadata.Person.EmailAddress}}")]
	[PictureparkDisplayPattern(Contract.DisplayPatternType.Thumbnail, Contract.TemplateEngine.DotLiquid, "{{Metadata.Person.Firstname}} {{Metadata.Person.LastName}}")]
	[PictureparkDisplayPattern(Contract.DisplayPatternType.Detail, Contract.TemplateEngine.DotLiquid, "{{Metadata.Person.Firstname}} {{Metadata.Person.LastName}}")]
	public class Person : IReference
	{
		[PictureparkSearch(Index = true, SimpleSearch = true, Boost = 10)]
		[PictureparkString(MultiLine = true)]
		[PictureparkRequired]
		public string Firstname { get; set; }

		[PictureparkSearch(Index = true, SimpleSearch = true, Boost = 10)]
		[PictureparkRequired]
		public string LastName { get; set; }

		[PictureparkPattern(@"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z")]
		[PictureparkRequired]
		public string EmailAddress { get; set; }

		[PictureparkSearch(Index = true, SimpleSearch = false, Boost = 0)]
		public DateTime BirthDate { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string refId { get; set; }

		// Usage maily for recursions
		[PictureparkSearch(Index = true, SimpleSearch = true, Boost = 10)]
		[PictureparkContentRelation("RelationName", "{ 'Kind': 'TermFilter', 'Field': 'AssetType', Term: 'Bitmap' }")]
		[PictureparkMaximumRecursion(MaxRecursion = 2)]
		public Person Child { get; set; }

		// Usage maily for recursions
		[PictureparkSearch(Index = true, SimpleSearch = true, Boost = 10)]
		[PictureparkMaximumRecursion(MaxRecursion = 3)]
		public List<Person> Siblings { get; set; }
	}

	public class SoccerPlayer : Person
	{
		[PictureparkSchemaItem("{ 'Kind': 'TermFilter', 'Field': 'ContentType', Term: 'FC Aarau' }")]
		public Club Club { get; set; }

		[PictureparkSchemaItem("{ 'Kind': 'TermFilter', 'Field': 'ContentType', Term: 'Krummbein' }")]
		public List<Pet> OwnsPets { get; set; }

		public List<Addresses> Addresses { get; set; }
	}

	public class SoccerTrainer : Person
	{
		public DateTime TrainerSince { get; set; }

		public List<Club> PreviousClubs { get; set; }
	}

	[PictureparkSchemaType(SchemaType.Struct)]
	public class Addresses
	{
		[PictureparkRequired]
		public string Name { get; set; }

		public Pet SecurityPet { get; set; }
	}

	[PictureparkSchemaType(SchemaType.List)]
	public class Club : IReference
	{
		[MaxLength(10)]
		public string Name { get; set; }

		public string Country { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string refId { get; set; }
	}

	[JsonConverter(typeof(JsonInheritanceConverter), "Kind")]
	[PictureparkSchemaType(SchemaType.List)]
	public class Pet : IReference
	{
		public string Name { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string refId { get; set; }
	}

	public class Dog : Pet
	{
		public bool PlaysCatch { get; set; }
	}

	public class Cat : Pet
	{
		public bool ChasesLaser { get; set; }
	}
}
