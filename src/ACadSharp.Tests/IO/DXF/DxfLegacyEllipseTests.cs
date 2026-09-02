using ACadSharp.Entities;
using ACadSharp.IO;
using CSMath;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace ACadSharp.Tests.IO.DXF;

public class DxfLegacyEllipseTests
{
	public static IEnumerable<object[]> InverseEllipseCases()
	{
		foreach (bool binary in new[] { false, true })
		foreach (bool failsafe in new[] { false, true })
		foreach (bool reverseTags in new[] { false, true })
		foreach (int shape in new[] { 0, 1, 2, 3 })
			yield return new object[] { binary, failsafe, reverseTags, shape };
	}

	public static IEnumerable<object[]> ValidEllipseCases()
	{
		foreach (bool binary in new[] { false, true })
		foreach (bool failsafe in new[] { false, true })
		foreach (bool reverseTags in new[] { false, true })
		foreach (double? ratio in new double?[] { null, 0.4, 1.0 })
			yield return new object[] { binary, failsafe, reverseTags, ratio };
	}

	public static IEnumerable<object[]> InvalidEllipseCases()
	{
		foreach (bool binary in new[] { false, true })
		foreach (bool failsafe in new[] { false, true })
		foreach (double ratio in new[] { 0.0, -0.5 })
			yield return new object[] { binary, failsafe, ratio };
	}

	[Theory]
	[MemberData(nameof(InverseEllipseCases))]
	public void InverseAxesPreserveGeometryAndFollowingEntity(bool binary, bool failsafe, bool reverseTags, int shape)
	{
		bool full = shape < 2;
		bool tilted = shape == 1 || shape == 3;
		XYZ normal = tilted ? new XYZ(1, 2, 3) : XYZ.AxisZ;
		XYZ axis = tilted ? new XYZ(3, -1.5, 0) : new XYZ(3 * Math.Cos(0.4), 3 * Math.Sin(0.4), 0);
		double start = full ? 0 : (shape == 2 ? 20 : 350) * Math.PI / 180.0;
		double end = full ? (shape == 1 ? 6.28318530717959 : 2 * Math.PI) : (shape == 2 ? 130 : 10) * Math.PI / 180.0;
		const double ratio = 2.5;
		byte[] bytes = createDxf(binary, reverseTags, axis, normal, ratio, start, end);
		List<NotificationEventArgs> notifications = new List<NotificationEventArgs>();
		CadDocument document = read(bytes, failsafe, notifications);

		Assert.DoesNotContain(notifications, notification => notification.NotificationType == NotificationType.Error);
		Ellipse ellipse = Assert.Single(document.Entities.OfType<Ellipse>());
		Assert.Equal(new XYZ(7, 11, 13), ellipse.Center);
		Assert.Equal(normal, ellipse.Normal);
		Assert.Equal(1.0 / ratio, ellipse.RadiusRatio, 12);
		XYZ unitNormal = normal / normal.GetLength();
		XYZ oldMinorAxis = XYZ.Cross(unitNormal, axis) * ratio;
		assertPoint(oldMinorAxis, ellipse.MajorAxisEndPoint);
		if (full)
		{
			Assert.True(ellipse.IsFullEllipse);
			Assert.Equal(0, ellipse.StartParameter);
			Assert.Equal(2 * Math.PI, ellipse.EndParameter);
		}
		else
		{
			Assert.False(ellipse.IsFullEllipse);
			Assert.Equal(start - Math.PI / 2, ellipse.StartParameter, 12);
			Assert.Equal(end - Math.PI / 2, ellipse.EndParameter, 12);
			Assert.Equal(positiveSweep(start, end), positiveSweep(ellipse.StartParameter, ellipse.EndParameter), 12);
		}

		XYZ newMinorAxis = XYZ.Cross(unitNormal, ellipse.MajorAxisEndPoint) * ellipse.RadiusRatio;
		double sweep = full ? 2 * Math.PI : positiveSweep(start, end);
		for (int i = 0; i <= 16; i++)
		{
			double parameter = start + sweep * i / 16.0;
			XYZ expected = ellipse.Center + axis * Math.Cos(parameter) + oldMinorAxis * Math.Sin(parameter);
			double shifted = parameter - Math.PI / 2;
			XYZ actual = ellipse.Center + ellipse.MajorAxisEndPoint * Math.Cos(shifted) + newMinorAxis * Math.Sin(shifted);
			assertPoint(expected, actual);
		}
		assertFollowingLine(document);
	}

	[Theory]
	[MemberData(nameof(ValidEllipseCases))]
	public void ValidOrOmittedRatioKeepsNativeValuesUnchanged(bool binary, bool failsafe, bool reverseTags, double? ratio)
	{
		XYZ axis = new XYZ(3, -1.5, 0);
		XYZ normal = new XYZ(1, 2, 3);
		const double start = -0.25;
		const double end = 6.28318530717959;
		List<NotificationEventArgs> notifications = new List<NotificationEventArgs>();
		CadDocument document = read(createDxf(binary, reverseTags, axis, normal, ratio, start, end), failsafe, notifications);
		Assert.DoesNotContain(notifications, notification => notification.NotificationType == NotificationType.Error);
		Ellipse ellipse = Assert.Single(document.Entities.OfType<Ellipse>());
		Assert.Equal(new XYZ(7, 11, 13), ellipse.Center);
		Assert.Equal(axis, ellipse.MajorAxisEndPoint);
		Assert.Equal(normal, ellipse.Normal);
		Assert.Equal(ratio ?? 1.0, ellipse.RadiusRatio);
		Assert.Equal(start, ellipse.StartParameter);
		Assert.Equal(end, ellipse.EndParameter);
		assertFollowingLine(document);
	}

	[Theory]
	[MemberData(nameof(InvalidEllipseCases))]
	public void ZeroAndNegativeRatioKeepExistingStrictAndFailsafeBehavior(bool binary, bool failsafe, double ratio)
	{
		byte[] bytes = createDxf(binary, false, new XYZ(3, 0, 0), XYZ.AxisZ, ratio, 0.2, 1.3);
		List<NotificationEventArgs> notifications = new List<NotificationEventArgs>();
		if (!failsafe)
		{
			Assert.ThrowsAny<Exception>(() => read(bytes, false, notifications));
			return;
		}

		CadDocument document = read(bytes, true, notifications);
		Assert.Contains(notifications, notification => notification.NotificationType == NotificationType.Error);
		Assert.Equal(1.0, Assert.Single(document.Entities.OfType<Ellipse>()).RadiusRatio);
		assertFollowingLine(document);
	}

	private static CadDocument read(byte[] bytes, bool failsafe, List<NotificationEventArgs> notifications)
	{
		byte[] original = (byte[])bytes.Clone();
		try
		{
			using (MemoryStream stream = new MemoryStream(bytes, false))
			using (DxfReader reader = new DxfReader(stream))
			{
				reader.Configuration.Failsafe = failsafe;
				reader.OnNotification += (_, notification) => notifications.Add(notification);
				return reader.Read();
			}
		}
		finally
		{
			Assert.Equal(original, bytes);
		}
	}

	private static void assertFollowingLine(CadDocument document)
	{
		Assert.Equal(2, document.Entities.Count);
		Line line = Assert.Single(document.Entities.OfType<Line>());
		Assert.Equal(new XYZ(-5, -7, 1), line.StartPoint);
		Assert.Equal(new XYZ(13, 17, 2), line.EndPoint);
	}

	private static void assertPoint(XYZ expected, XYZ actual)
	{
		Assert.Equal(expected.X, actual.X, 10);
		Assert.Equal(expected.Y, actual.Y, 10);
		Assert.Equal(expected.Z, actual.Z, 10);
	}

	private static double positiveSweep(double start, double end)
	{
		double sweep = (end - start) % (2 * Math.PI);
		return sweep < 0 ? sweep + 2 * Math.PI : sweep;
	}

	private static byte[] createDxf(bool binary, bool reverseTags, XYZ axis, XYZ normal, double? ratio, double start, double end)
	{
		List<(short Code, object Value)> tags = new List<(short, object)>
		{
			(0, "SECTION"), (2, "HEADER"), (9, "$ACADVER"), (1, "AC1015"), (0, "ENDSEC"),
			(0, "SECTION"), (2, "ENTITIES"),
			(0, "ELLIPSE"), (5, "A1"), (100, "AcDbEntity"), (8, "0"), (100, "AcDbEllipse"),
			(10, 7.0), (20, 11.0), (30, 13.0)
		};
		List<(short Code, object Value)> ellipseTags = new List<(short, object)>
		{
			(11, axis.X), (21, axis.Y), (31, axis.Z),
			(210, normal.X), (220, normal.Y), (230, normal.Z),
			(41, start), (42, end)
		};
		if (ratio.HasValue) { ellipseTags.Add((40, ratio.Value)); }
		if (reverseTags) { ellipseTags.Reverse(); }
		tags.AddRange(ellipseTags);
		tags.AddRange(new (short, object)[]
		{
			(0, "LINE"), (5, "A2"), (100, "AcDbEntity"), (8, "0"), (100, "AcDbLine"),
			(10, -5.0), (20, -7.0), (30, 1.0), (11, 13.0), (21, 17.0), (31, 2.0),
			(0, "ENDSEC"), (0, "EOF")
		});

		if (!binary)
		{
			StringBuilder text = new StringBuilder();
			foreach (var tag in tags)
			{
				text.Append(tag.Code.ToString(CultureInfo.InvariantCulture)).Append('\n');
				text.Append(tag.Value is double number ? number.ToString("R", CultureInfo.InvariantCulture) : (string)tag.Value).Append('\n');
			}
			return Encoding.ASCII.GetBytes(text.ToString());
		}

		using (MemoryStream stream = new MemoryStream())
		using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII, true))
		{
			writer.Write(Encoding.ASCII.GetBytes("AutoCAD Binary DXF\r\n\u001a\0"));
			foreach (var tag in tags)
			{
				writer.Write(tag.Code);
				if (tag.Value is double number)
				{
					writer.Write(number);
				}
				else
				{
					writer.Write(Encoding.ASCII.GetBytes((string)tag.Value));
					writer.Write((byte)0);
				}
			}
			writer.Flush();
			return stream.ToArray();
		}
	}
}
