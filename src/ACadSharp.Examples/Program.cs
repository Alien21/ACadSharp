using ACadSharp.IO;
using ACadSharp.Tables;
using ACadSharp.Tables.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ACadSharp.Entities;
using ACadSharp.Tests.Common;
using CSMath;

namespace ACadSharp.Examples
{
	class Program
	{
		const string _file = "../../../../../samples/sample_AC1032.dwg";

		static void Main(string[] args)
		{
			CadDocument doc;
			
			doc = new CadDocument();
			doc.Header.Version = ACadVersion.AC1015;

			string path = @"t:\acadsharp-test-AC1015.dxf";

			// List<Entity> entities = new List<Entity>
			// {
			// 	EntityFactory.Create<Point>(),
			// 	EntityFactory.Create<Line>(),
			// 	EntityFactory.Create<Polyline2D>(),
			// 	EntityFactory.Create<Polyline3D>(),
			// 	EntityFactory.Create<Line>(),
			// 	EntityFactory.Create<Arc>(),
			// 	EntityFactory.Create<LwPolyline>(),
			// };
			//
			//
			// doc.Entities.AddRange(entities);

			var polyline2d = new Polyline2D
			{
				IsClosed = true,
				Vertices = {
					new Vertex2D(new XY(0, 0)),
					new Vertex2D(new XY(10, 0)),
					new Vertex2D(new XY(10, 10)),
					new Vertex2D(new XY(0, 10)),
				}
			};

			var circle = new Circle
			{
				Center = new XYZ(25, 15, 0),
				Radius = 10
			};

			var ellipse = new Ellipse
			{
				Center = new XYZ(50, 15, 0),
				EndPoint = new XYZ(10, 15, 0),
				RadiusRatio = 0.5
			};
			
			doc.Entities.Add(polyline2d);
			doc.Entities.Add(circle);
			doc.Entities.Add(ellipse);
			
			using (var wr = new DxfWriter(path, doc, false))
			{
				wr.OnNotification += onNotification;
				wr.Write();
			}

			Console.WriteLine(string.Empty);
			Console.WriteLine("Writer successful");
			Console.WriteLine(string.Empty);

			using (var re = new DxfReader(path, onNotification))
			{
				CadDocument readed = re.Read();
			}

			
			using (DwgReader reader = new DwgReader(_file))
			{
				doc = reader.Read();
			}

			exploreDocument(doc);
		}

		/// <summary>
		/// Logs in the console the document information
		/// </summary>
		/// <param name="doc"></param>
		static void exploreDocument(CadDocument doc)
		{
			Console.WriteLine();
			Console.WriteLine("SUMMARY INFO:");
			Console.WriteLine($"\tTitle: {doc.SummaryInfo.Title}");
			Console.WriteLine($"\tSubject: {doc.SummaryInfo.Subject}");
			Console.WriteLine($"\tAuthor: {doc.SummaryInfo.Author}");
			Console.WriteLine($"\tKeywords: {doc.SummaryInfo.Keywords}");
			Console.WriteLine($"\tComments: {doc.SummaryInfo.Comments}");
			Console.WriteLine($"\tLastSavedBy: {doc.SummaryInfo.LastSavedBy}");
			Console.WriteLine($"\tRevisionNumber: {doc.SummaryInfo.RevisionNumber}");
			Console.WriteLine($"\tHyperlinkBase: {doc.SummaryInfo.HyperlinkBase}");
			Console.WriteLine($"\tCreatedDate: {doc.SummaryInfo.CreatedDate}");
			Console.WriteLine($"\tModifiedDate: {doc.SummaryInfo.ModifiedDate}");

			exploreTable(doc.AppIds);
			exploreTable(doc.BlockRecords);
			exploreTable(doc.DimensionStyles);
			exploreTable(doc.Layers);
			exploreTable(doc.LineTypes);
			exploreTable(doc.TextStyles);
			exploreTable(doc.UCSs);
			exploreTable(doc.Views);
			exploreTable(doc.VPorts);
		}

		static void exploreTable<T>(Table<T> table)
			where T : TableEntry
		{
			Console.WriteLine($"{table.ObjectName}");
			foreach (var item in table)
			{
				Console.WriteLine($"\tName: {item.Name}");

				if (item.Name == BlockRecord.ModelSpaceName && item is BlockRecord model)
				{
					Console.WriteLine($"\t\tEntities in the model:");
					foreach (var e in model.Entities.GroupBy(i => i.GetType().FullName))
					{
						Console.WriteLine($"\t\t{e.Key}: {e.Count()}");
					}
				}
			}
		}

		private static void onNotification(object sender, NotificationEventArgs e)
		{
			if (e.NotificationType == NotificationType.Error)
			{
				throw e.Exception;
			}

			Console.WriteLine(e.Message);
			if (e.Exception != null)
			{
				Console.WriteLine(e.Exception.ToString());
			}
		}

	}
}