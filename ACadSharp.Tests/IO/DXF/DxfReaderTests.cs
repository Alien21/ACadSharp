﻿using ACadSharp.IO;
using Xunit;
using Xunit.Abstractions;

namespace ACadSharp.Tests.IO.DXF
{
	public class DxfReaderTests : CadReaderTestsBase<DxfReader>
	{
		public DxfReaderTests(ITestOutputHelper output) : base(output) { }

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		public void ReadHeaderAciiTest(string test)
		{
			base.ReadHeaderTest(test);
		}

		[Theory]
		[MemberData(nameof(DxfBinaryFiles))]
		public void ReadHeaderBinaryTest(string test)
		{
			base.ReadHeaderTest(test);
		}

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		public void ReadAsciiTest(string test)
		{
			base.ReadTest(test);
		}

		[Theory]
		[MemberData(nameof(DxfBinaryFiles))]
		public void ReadBinaryTest(string test)
		{
			base.ReadTest(test);
		}

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		[MemberData(nameof(DxfBinaryFiles))]
		public override void AssertDocumentDefaults(string test)
		{
			base.AssertDocumentDefaults(test);
		}

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		[MemberData(nameof(DxfBinaryFiles))]
		public override void AssertTableHirearchy(string test)
		{
			base.AssertTableHirearchy(test);
		}

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		[MemberData(nameof(DxfBinaryFiles))]
		public override void AssertBlockRecords(string test)
		{
			base.AssertBlockRecords(test);
		}

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		[MemberData(nameof(DxfBinaryFiles))]
		public override void AssertDocumentContent(string test)
		{
			base.AssertDocumentContent(test);
		}

		[Theory(Skip = "Dxf files need to be refactor to support the Unknown entities")]
		[MemberData(nameof(DxfAsciiFiles))]
		[MemberData(nameof(DxfBinaryFiles))]
		public override void AssertDocumentTree(string test)
		{
			DxfReaderConfiguration configuration = new DxfReaderConfiguration();
			configuration.KeepUnknownEntities = true;

			CadDocument doc;
			using (DxfReader reader = new DxfReader(test))
			{
				reader.Configuration = configuration;
				doc = reader.Read();
			}

			this._docIntegrity.AssertDocumentTree(doc);
		}

		[Theory]
		[MemberData(nameof(DxfAsciiFiles))]
		[MemberData(nameof(DxfBinaryFiles))]
		public void IsBinaryTest(string test)
		{
			if (test.Contains("ascii", System.StringComparison.OrdinalIgnoreCase))
			{
				Assert.False(DxfReader.IsBinary(test));
			}
			else
			{
				Assert.True(DxfReader.IsBinary(test));
			}

			using (DxfReader reader = new DxfReader(test))
			{
				if (test.Contains("ascii", System.StringComparison.OrdinalIgnoreCase))
				{
					Assert.False(reader.IsBinary());
				}
				else
				{
					Assert.True(reader.IsBinary());
				}
			}
		}
	}
}