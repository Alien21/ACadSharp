using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace ACadSharp.Tests.IO.DXF;

public class DxfLegacyPolylineTests
{
	public static IEnumerable<object[]> LegacyPolylineCases()
	{
		// null omits HEADER; an empty value keeps HEADER without $ACADVER.
		string[] versions = { null, "", "AC1002", "AC1003", "AC1004", "AC1006", "AC1009" };
		foreach (string version in versions)
		{
			foreach (bool withHandles in new[] { false, true })
			{
				foreach (bool failsafe in new[] { false, true })
				{
					yield return new object[] { version, withHandles, failsafe };
				}
			}
		}
	}

	public static IEnumerable<object[]> LegacyNon2DPolylineCases()
	{
		string[] versions = { null, "", "AC1002", "AC1003", "AC1004", "AC1006", "AC1009" };
		// Include the closed variants to verify a type-bit mask, not exact flag equality.
		int[] flags = { 8, 9, 16, 17, 64, 65 };
		foreach (string version in versions)
		{
			foreach (int flag in flags)
			{
				foreach (bool failsafe in new[] { false, true })
				{
					yield return new object[] { version, flag, failsafe };
				}
			}
		}
	}

	[Theory]
	[MemberData(nameof(LegacyNon2DPolylineCases))]
	public void ReadLegacyNon2DPolylineIsDiscardedWithoutLosingFollowingEntity(string version, int flags, bool failsafe)
	{
		List<NotificationEventArgs> notifications = new List<NotificationEventArgs>();
		CadDocument document = read(createPolylineDxf(version, true, false, flags: flags), failsafe, notifications);

		Line line = Assert.Single(document.Entities.OfType<Line>());
		Assert.Equal(new XYZ(-5, -7, 0), line.StartPoint);
		Assert.Equal(new XYZ(13, 17, 0), line.EndPoint);
		Assert.Contains(notifications, notification =>
			notification.NotificationType == NotificationType.Warning
			&& notification.Message.Contains("POLYLINE")
			&& notification.Message.Contains("not supported")
			&& notification.Message.Contains("discarded"));

		// Reject before consuming VERTEX/SEQEND records, as the existing
		// discarded-parent path does. Never expose them as a false 2D polyline.
		Assert.Empty(document.Entities.OfType<IPolyline>());
		Assert.Equal(5, document.Entities.Count);
		Assert.Equal(3, document.Entities.OfType<Vertex>().Count());
		Assert.Single(document.Entities.OfType<Seqend>());
		Assert.Equal(new ulong[] { 0xA2, 0xA3, 0xA4 },
			document.Entities.OfType<Vertex>().Select(vertex => vertex.Handle));
		Assert.DoesNotContain(document.Entities, entity => entity.Handle == 0xA1UL);
	}

	[Theory]
	[MemberData(nameof(LegacyPolylineCases))]
	public void ReadLegacyPolylinePreservesVerticesAndFollowingEntity(string version, bool withHandles, bool failsafe)
	{
		string dxf = createPolylineDxf(version, withHandles, false);
		CadDocument document = read(dxf, failsafe);

		assertPolyline2D(document);
		if (withHandles)
		{
			Polyline2D polyline = Assert.Single(document.Entities.OfType<Polyline2D>());
			Assert.Equal(0xA1UL, polyline.Handle);
			Assert.Equal(new ulong[] { 0xA2, 0xA3, 0xA4 }, polyline.Vertices.Select(v => v.Handle));
			Assert.Equal(0xA5UL, polyline.Vertices.Seqend.Handle);
			Assert.Equal(0xA6UL, Assert.Single(document.Entities.OfType<Line>()).Handle);
		}
	}

	[Theory]
	[InlineData("AC1012")]
	[InlineData("AC1015")]
	[InlineData("AC1032")]
	public void ReadModernPolylinePreservesSubclassAndVertices(string version)
	{
		CadDocument document = read(createPolylineDxf(version, true, true), false);

		assertPolyline2D(document);
	}

	[Theory]
	[InlineData("AC1012")]
	[InlineData("AC1015")]
	[InlineData("AC1032")]
	public void ReadModernPolylinePreserves3DSubclass(string version)
	{
		CadDocument document = read(createPolylineDxf(version, true, true, true), false);

		assertFollowingLine(document);
		Polyline3D polyline = Assert.Single(document.Entities.OfType<Polyline3D>());
		Assert.Empty(document.Entities.OfType<Polyline2D>());
		Assert.False(polyline.IsClosed);
		Assert.Equal(new[] { new XYZ(1, 2, 3), new XYZ(11, 4, 5), new XYZ(6, 12, 7) },
			polyline.Vertices.Select(v => v.Location));
		Assert.All(polyline.Vertices, vertex => Assert.Same(polyline, vertex.Owner));
		Assert.NotNull(polyline.Vertices.Seqend);
		Assert.Same(polyline, polyline.Vertices.Seqend.Owner);
	}

	private static void assertPolyline2D(CadDocument document)
	{
		assertFollowingLine(document);
		Polyline2D polyline = Assert.Single(document.Entities.OfType<Polyline2D>());
		Assert.True(polyline.IsClosed);
		Assert.Equal(2.5, polyline.Elevation);
		Assert.Equal(1.25, polyline.StartWidth);
		Assert.Equal(2.5, polyline.EndWidth);
		Assert.Equal(new[] { new XYZ(1, 2, 0), new XYZ(11, 4, 0), new XYZ(6, 12, 0) },
			polyline.Vertices.Select(v => v.Location));
		Assert.Equal(new[] { 0.5, 0.0, -0.25 }, polyline.Vertices.Select(v => v.Bulge));
		Assert.Equal(new[] { 0.25, 0.0, 1.5 }, polyline.Vertices.Select(v => v.StartWidth));
		Assert.Equal(new[] { 0.5, 0.0, 2.0 }, polyline.Vertices.Select(v => v.EndWidth));
		Assert.All(polyline.Vertices, vertex => Assert.Same(polyline, vertex.Owner));
		Assert.NotNull(polyline.Vertices.Seqend);
		Assert.Same(polyline, polyline.Vertices.Seqend.Owner);
	}

	private static void assertFollowingLine(CadDocument document)
	{
		Assert.Equal(2, document.Entities.Count);
		Assert.Empty(document.Entities.OfType<Vertex>());
		Assert.Empty(document.Entities.OfType<Seqend>());
		Line line = Assert.Single(document.Entities.OfType<Line>());
		Assert.Equal(new XYZ(-5, -7, 0), line.StartPoint);
		Assert.Equal(new XYZ(13, 17, 0), line.EndPoint);
	}

	private static CadDocument read(string dxf, bool failsafe, List<NotificationEventArgs> notifications = null)
	{
		notifications = notifications ?? new List<NotificationEventArgs>();
		CadDocument document;
		using (MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(dxf)))
		using (DxfReader reader = new DxfReader(stream))
		{
			reader.Configuration.Failsafe = failsafe;
			reader.OnNotification += (_, notification) => notifications.Add(notification);
			document = reader.Read();
		}

		// Missing HEADER/version and omitted tables may legitimately produce warnings.
		Assert.DoesNotContain(notifications, notification => notification.NotificationType == NotificationType.Error);
		return document;
	}

	private static string createPolylineDxf(string version, bool withHandles, bool modern, bool polyline3D = false, int? flags = null)
	{
		List<string> codes = new List<string>();
		if (version != null)
		{
			codes.AddRange(new[] { "0", "SECTION", "2", "HEADER" });
			if (version.Length > 0)
			{
				codes.AddRange(new[] { "9", "$ACADVER", "1", version });
			}
			codes.AddRange(new[] { "0", "ENDSEC" });
		}

		codes.AddRange(new[] { "0", "SECTION", "2", "ENTITIES" });
		addEntityHeader(codes, "POLYLINE", "A1", withHandles, modern);
		if (modern)
		{
			codes.AddRange(new[] { "100", polyline3D ? "AcDb3dPolyline" : "AcDb2dPolyline" });
		}
		codes.AddRange(new[]
		{
			"66", "1",
			"10", "0", "20", "0", "30", polyline3D ? "0" : "2.5",
			"70", flags?.ToString(CultureInfo.InvariantCulture) ?? (polyline3D ? "8" : "1")
		});
		if (!polyline3D)
		{
			codes.AddRange(new[] { "40", "1.25", "41", "2.5" });
		}

		addVertex(codes, "A2", withHandles, modern, polyline3D, "1", "2", "3", "0.5", "0.25", "0.5");
		addVertex(codes, "A3", withHandles, modern, polyline3D, "11", "4", "5", "0", "0", "0");
		addVertex(codes, "A4", withHandles, modern, polyline3D, "6", "12", "7", "-0.25", "1.5", "2");

		addEntityHeader(codes, "SEQEND", "A5", withHandles, modern);
		addEntityHeader(codes, "LINE", "A6", withHandles, modern);
		if (modern)
		{
			codes.AddRange(new[] { "100", "AcDbLine" });
		}
		codes.AddRange(new[]
		{
			"10", "-5", "20", "-7", "30", "0",
			"11", "13", "21", "17", "31", "0",
			"0", "ENDSEC", "0", "EOF"
		});
		return string.Join("\n", codes);
	}

	private static void addEntityHeader(List<string> codes, string type, string handle, bool withHandles, bool modern)
	{
		codes.AddRange(new[] { "0", type });
		if (withHandles)
		{
			codes.AddRange(new[] { "5", handle });
		}
		if (modern)
		{
			codes.AddRange(new[] { "100", "AcDbEntity" });
		}
		codes.AddRange(new[] { "8", "0" });
	}

	private static void addVertex(List<string> codes, string handle, bool withHandles, bool modern, bool polyline3D,
		string x, string y, string z, string bulge, string startWidth, string endWidth)
	{
		addEntityHeader(codes, "VERTEX", handle, withHandles, modern);
		if (modern)
		{
			codes.AddRange(new[] { "100", "AcDbVertex", "100", polyline3D ? "AcDb3dPolylineVertex" : "AcDb2dVertex" });
		}
		codes.AddRange(new[]
		{
			"10", x, "20", y, "30", polyline3D ? z : "0",
			"70", polyline3D ? "32" : "0"
		});
		if (!polyline3D)
		{
			codes.AddRange(new[] { "40", startWidth, "41", endWidth, "42", bulge });
		}
	}
}
