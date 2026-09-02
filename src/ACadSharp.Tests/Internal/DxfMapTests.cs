using ACadSharp.Attributes;
using ACadSharp.Entities;
using ACadSharp.Tables;
using ACadSharp.Tests.Common;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ACadSharp.Tests.Internal;

public class DxfMapTests
{
	public static readonly TheoryData<Type> Types;

	static DxfMapTests()
	{
		Types = new TheoryData<Type>();

		var d = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.ManifestModule.Name == "ACadSharp.dll");

		foreach (var item in d.GetTypes().Where(i => !i.IsAbstract && i.IsPublic))
		{
			if (item.IsSubclassOf(typeof(Entity))
				|| item.IsSubclassOf(typeof(TableEntry)))
			{
				if (item == typeof(UnknownEntity) 
					|| item == typeof(PdfUnderlay))
				{
					continue;
				}

				Types.Add(item);
			}
		}
	}

	[Fact]
	public async Task CreateMapsRemainCompleteWhileCacheIsCleared()
	{
		const int workerCount = 4;
		const int iterations = 1000;
		ConcurrentQueue<Exception> failures = new ConcurrentQueue<Exception>();
		int completedCreators = 0;
		int cacheClearCount = 0;
		using (CancellationTokenSource stop = new CancellationTokenSource())
		using (ManualResetEventSlim start = new ManualResetEventSlim(false))
		{
			// There is no public gate between cache publication and lookup. Exercise
			// that race with bounded concurrent public API calls, without test hooks.
			Task clearer = Task.Factory.StartNew(() =>
			{
				start.Wait();
				while (!stop.IsCancellationRequested)
				{
					DxfMap.ClearCache();
					Interlocked.Increment(ref cacheClearCount);
					Thread.Yield();
				}
			}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
			Task[] creators = Enumerable.Range(0, workerCount).Select(_ => Task.Factory.StartNew(() =>
			{
				start.Wait();
				try
				{
					for (int i = 0; i < iterations; i++)
					{
						if (stop.IsCancellationRequested)
							return;

						DxfMap layer = DxfMap.Create<Layer>();
						Assert.NotNull(layer);
						Assert.True(layer.DxfProperties.ContainsKey(5));
						Assert.True(layer.SubClasses.ContainsKey(DxfSubclassMarker.Layer));
						Assert.True(layer.SubClasses[DxfSubclassMarker.Layer].DxfProperties.ContainsKey(62));

						DxfMap line = DxfMap.Create<Line>();
						Assert.NotNull(line);
						Assert.True(line.DxfProperties.ContainsKey(5));
						Assert.True(line.SubClasses.ContainsKey(DxfSubclassMarker.Line));
						Assert.True(line.SubClasses[DxfSubclassMarker.Line].DxfProperties.ContainsKey(10));
					}

					Interlocked.Increment(ref completedCreators);
				}
				catch (Exception ex)
				{
					failures.Enqueue(ex);
					stop.Cancel();
				}
			}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

			Task[] allTasks = creators.Concat(new[] { clearer }).ToArray();
			bool finished;
			bool stopped;
			start.Set();
			try
			{
				Task creations = Task.WhenAll(creators);
				finished = await Task.WhenAny(creations, Task.Delay(TimeSpan.FromSeconds(10))) == creations;
			}
			finally
			{
				stop.Cancel();
				Task all = Task.WhenAll(allTasks);
				stopped = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(5))) == all;
				if (stopped)
					await all;
			}

			Assert.True(stopped, "Map cache stress workers did not stop within the timeout.");
			Assert.Empty(failures);
			Assert.True(finished, "Concurrent map creation did not finish within the timeout.");
			Assert.Equal(workerCount, completedCreators);
			Assert.True(cacheClearCount > 0);
		}
	}

	[Fact]
	public void CreateKeepsMapDictionariesIndependent()
	{
		DxfMap.ClearCache();
		DxfMap first = DxfMap.Create<Layer>();
		DxfMap second = DxfMap.Create<Layer>();
		Assert.NotSame(first, second);
		Assert.NotSame(first.DxfProperties, second.DxfProperties);
		Assert.NotSame(first.SubClasses, second.SubClasses);

		first.DxfProperties.Remove(5);
		first.SubClasses.Remove(DxfSubclassMarker.Layer);

		Assert.True(second.DxfProperties.ContainsKey(5));
		Assert.True(second.SubClasses.ContainsKey(DxfSubclassMarker.Layer));
		DxfMap next = DxfMap.Create<Layer>();
		Assert.True(next.DxfProperties.ContainsKey(5));
		Assert.True(next.SubClasses.ContainsKey(DxfSubclassMarker.Layer));
	}

	[Theory]
	[MemberData(nameof(Types))]
	public void CreateMapTest(Type t)
	{
		DxfNameAttribute att = t.GetCustomAttribute<DxfNameAttribute>();
		DxfSubClassAttribute subclass = t.GetCustomAttribute<DxfSubClassAttribute>();
		CadObject obj = Factory.CreateObject(t);

		Assert.NotNull(att);

		if (subclass != null && !obj.HasDynamicSubclass)
		{
			Assert.True(obj.SubclassMarker == subclass.ClassName);
		}

		DxfMap map = DxfMap.Create(t);
	}

	[Fact]
	public void TableEntryMapTest()
	{
		var map = DxfMap.Create<AppId>();

		Assert.True(map.SubClasses.ContainsKey(DxfSubclassMarker.TableRecord));
		Assert.True(map.SubClasses.ContainsKey(DxfSubclassMarker.ApplicationId));
	}

	[Fact]
	public void PolylineMapTest()
	{
		var map = DxfMap.Create<Polyline2D>();

		Assert.True(map.SubClasses.ContainsKey(DxfSubclassMarker.Entity));
		Assert.True(map.SubClasses.ContainsKey(DxfSubclassMarker.Polyline));
	}
}
