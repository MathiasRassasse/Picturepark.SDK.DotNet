using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Picturepark.SDK.V1.Contract;
using Picturepark.SDK.V1.Contract.Attributes;
using Picturepark.SDK.V1.Contract.Interfaces;

namespace Picturepark.SDK.V1.Tests.Contracts
{
	public class PersonDetails
	{
		[PictureparkRequired]
		public string Professtion { get; set; }

		[PictureparkRequired]
		public string Hobby { get; set; }
	}
}
