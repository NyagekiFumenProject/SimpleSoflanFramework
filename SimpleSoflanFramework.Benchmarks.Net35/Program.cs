using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Modules.FumenVisualEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v3.5", FrameworkDisplayName = ".NET Framework 3.5")]

namespace SimpleSoflanFramework.Benchmarks.Net35
{
    internal static class Program
    {
        private const string GamePackageDir = @"F:\SDEZ_165\Package";
        private const string GameManagedDir = GamePackageDir + @"\Sinmai_Data\Managed";
        private const string GameMonoRuntimeDll = GamePackageDir + @"\MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll";
        private const int WarmupIterations = 2_000;
        private const int MeasureIterations = 50_000;

        private sealed class BenchmarkCase
        {
            public BpmList BpmList;
            public SoflanList SoflanList;
            public double[] CurrentYs;
            public double[] ViewHeights;
        }

        private sealed class Measurement
        {
            public string Name;
            public double ElapsedMs;
            public long Checksum;
            public int Gen0Collections;
            public int Gen1Collections;
            public int Gen2Collections;
            public long MemoryDeltaBytes;
        }

        private static int Main(string[] args)
        {
            try
            {
                PrintRuntimeInfo();

                BenchmarkCase benchmarkCase = CreateBenchmarkCase();
                EnsureEquivalent(benchmarkCase);

                RunOriginal(benchmarkCase, WarmupIterations);
                RunFill(benchmarkCase, WarmupIterations);
                RunFillWithMsecConversion(benchmarkCase, WarmupIterations);
                RunFillMsec(benchmarkCase, WarmupIterations);

                Measurement original = Measure("GetVisibleRanges_PreviewMode", delegate { return RunOriginal(benchmarkCase, MeasureIterations); });
                Measurement fill = Measure("FillVisibleRangesForGamePreview", delegate { return RunFill(benchmarkCase, MeasureIterations); });
                Measurement fillWithMsecConversion = Measure("FillVisibleRangesForGamePreview + ConvertTGridToAudioTime", delegate { return RunFillWithMsecConversion(benchmarkCase, MeasureIterations); });
                Measurement fillMsec = Measure("FillVisibleMsecRangesForGamePreview", delegate { return RunFillMsec(benchmarkCase, MeasureIterations); });

                PrintMeasurement(original);
                PrintMeasurement(fill);
                PrintMeasurement(fillWithMsecConversion);
                PrintMeasurement(fillMsec);
                Console.WriteLine("TGrid fill speedup: {0:F2}x", original.ElapsedMs / fill.ElapsedMs);
                Console.WriteLine("Direct msec speedup over TGrid+convert: {0:F2}x", fillWithMsecConversion.ElapsedMs / fillMsec.ElapsedMs);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void PrintRuntimeInfo()
        {
            Console.WriteLine("Benchmark target: .NET Framework 3.5 project");
            Console.WriteLine("CLR: {0}", Environment.Version);
            Console.WriteLine("Core: {0}", typeof(SoflanList).Assembly.FullName);
            Console.WriteLine("Game mono runtime: {0} exists={1}", GameMonoRuntimeDll, File.Exists(GameMonoRuntimeDll));
            PrintAssemblyName("Game mscorlib", Path.Combine(GameManagedDir, "mscorlib.dll"));
            PrintAssemblyName("Game System", Path.Combine(GameManagedDir, "System.dll"));
            PrintAssemblyName("Game System.Core", Path.Combine(GameManagedDir, "System.Core.dll"));
            Console.WriteLine();
        }

        private static void PrintAssemblyName(string label, string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("{0}: missing ({1})", label, path);
                return;
            }

            Console.WriteLine("{0}: {1}", label, AssemblyName.GetAssemblyName(path).FullName);
        }

        private static BenchmarkCase CreateBenchmarkCase()
        {
            BpmList bpmList = new BpmList();
            bpmList.FirstBpm = 180;
            AddBpm(bpmList, 16, 0, 220);
            AddBpm(bpmList, 32, 0, 145);
            AddBpm(bpmList, 48, 192, 260);
            AddBpm(bpmList, 72, 0, 190);
            AddBpm(bpmList, 96, 0, 240);
            AddBpm(bpmList, 128, 0, 160);

            SoflanList soflanList = new SoflanList(new ISoflan[0]);
            AddKeyframe(soflanList, 0, 0, 1.0f);
            AddKeyframe(soflanList, 4, 0, 0.35f);
            AddKeyframe(soflanList, 8, 0, 2.25f);
            AddKeyframe(soflanList, 12, 192, 0.0f);
            AddKeyframe(soflanList, 16, 0, 1.0f);
            AddKeyframe(soflanList, 24, 0, -1.0f);
            AddKeyframe(soflanList, 28, 0, 1.0f);
            AddKeyframe(soflanList, 36, 0, 3.0f);
            AddKeyframe(soflanList, 44, 192, 0.5f);
            AddKeyframe(soflanList, 56, 0, -0.75f);
            AddKeyframe(soflanList, 60, 0, 1.0f);
            AddKeyframe(soflanList, 80, 0, 1.8f);
            AddKeyframe(soflanList, 96, 0, 0.2f);
            AddKeyframe(soflanList, 112, 0, 1.0f);
            AddDuration(soflanList, 124, 0, 132, 0, 2.5f);
            AddDuration(soflanList, 144, 0, 152, 0, -1.25f);
            AddKeyframe(soflanList, 160, 0, 1.0f);

            soflanList.GetCachedSoflanPositionList_PreviewMode(bpmList);
            soflanList.GetCachedSoflanSegment_PreviewMode(bpmList);

            double[] currentYs = new double[384];
            for (int i = 0; i < currentYs.Length; i++)
            {
                double audioMsec = (i * 419.0) % 140000.0;
                currentYs[i] = TGridCalculator.ConvertAudioTimeToY_PreviewMode(TimeSpan.FromMilliseconds(audioMsec), soflanList, bpmList, 1);
            }

            return new BenchmarkCase
            {
                BpmList = bpmList,
                SoflanList = soflanList,
                CurrentYs = currentYs,
                ViewHeights = new[] { 900d, 1200d, 1600d, 2200d, 3000d }
            };
        }

        private static void AddBpm(BpmList bpmList, int unit, int grid, double bpm)
        {
            bpmList.Add(new BPMChange
            {
                TGrid = new TGrid(unit, grid),
                BPM = bpm
            });
        }

        private static void AddKeyframe(SoflanList soflanList, int unit, int grid, float speed)
        {
            soflanList.Add(new KeyframeSoflan
            {
                TGrid = new TGrid(unit, grid),
                Speed = speed
            });
        }

        private static void AddDuration(SoflanList soflanList, int startUnit, int startGrid, int endUnit, int endGrid, float speed)
        {
            soflanList.Add(new Soflan
            {
                TGrid = new TGrid(startUnit, startGrid),
                EndTGrid = new TGrid(endUnit, endGrid),
                Speed = speed
            });
        }

        private static void EnsureEquivalent(BenchmarkCase benchmarkCase)
        {
            List<SoflanList.VisibleTGridRange> oldRanges;
            List<SoflanList.VisibleTGridRange> newRanges = new List<SoflanList.VisibleTGridRange>(16);
            List<SoflanList.VisibleMsecRange> msecRanges = new List<SoflanList.VisibleMsecRange>(16);
            SoflanList.VisibleRangeQueryScratch scratch = new SoflanList.VisibleRangeQueryScratch();
            int queryCount = Lcm(benchmarkCase.CurrentYs.Length, benchmarkCase.ViewHeights.Length);

            for (int i = 0; i < queryCount; i++)
            {
                double currentY = benchmarkCase.CurrentYs[i % benchmarkCase.CurrentYs.Length];
                double viewHeight = benchmarkCase.ViewHeights[i % benchmarkCase.ViewHeights.Length];

                oldRanges = benchmarkCase.SoflanList.GetVisibleRanges_PreviewMode(currentY, viewHeight, 0, benchmarkCase.BpmList, 1).ToList();
                benchmarkCase.SoflanList.FillVisibleRangesForGamePreview(currentY, viewHeight, benchmarkCase.BpmList, newRanges, scratch);

                if (!RangesEqual(oldRanges, newRanges))
                    ThrowMismatch(i, currentY, viewHeight, oldRanges, newRanges);

                benchmarkCase.SoflanList.FillVisibleMsecRangesForGamePreview(currentY, viewHeight, benchmarkCase.BpmList, msecRanges, scratch);
                if (!MsecRangesEqual(oldRanges, msecRanges, benchmarkCase.BpmList))
                    ThrowMsecMismatch(i, currentY, viewHeight, oldRanges, msecRanges, benchmarkCase.BpmList);
            }

            Console.WriteLine("Equivalence: OK ({0} game-shape queries)", queryCount);
            Console.WriteLine();
        }

        private static int Lcm(int a, int b)
        {
            return a / Gcd(a, b) * b;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }

            return a;
        }

        private static bool RangesEqual(List<SoflanList.VisibleTGridRange> left, List<SoflanList.VisibleTGridRange> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i].minTGrid.TotalGrid != right[i].minTGrid.TotalGrid)
                    return false;
                if (left[i].maxTGrid.TotalGrid != right[i].maxTGrid.TotalGrid)
                    return false;
            }

            return true;
        }

        private static bool MsecRangesEqual(List<SoflanList.VisibleTGridRange> tGridRanges, List<SoflanList.VisibleMsecRange> msecRanges, BpmList bpmList)
        {
            const double Epsilon = 0.0001;
            if (tGridRanges.Count != msecRanges.Count)
                return false;

            for (int i = 0; i < tGridRanges.Count; i++)
            {
                double expectedMin = TGridCalculator.ConvertTGridToAudioTime(tGridRanges[i].minTGrid, bpmList).TotalMilliseconds;
                double expectedMax = TGridCalculator.ConvertTGridToAudioTime(tGridRanges[i].maxTGrid, bpmList).TotalMilliseconds;

                if (Math.Abs(expectedMin - msecRanges[i].MinMsec) > Epsilon)
                    return false;
                if (Math.Abs(expectedMax - msecRanges[i].MaxMsec) > Epsilon)
                    return false;
            }

            return true;
        }

        private static void ThrowMismatch(int index, double currentY, double viewHeight, List<SoflanList.VisibleTGridRange> oldRanges, List<SoflanList.VisibleTGridRange> newRanges)
        {
            Console.Error.WriteLine("Mismatch at query {0}, currentY={1}, viewHeight={2}", index, currentY, viewHeight);
            Console.Error.WriteLine("Old: {0}", FormatRanges(oldRanges));
            Console.Error.WriteLine("New: {0}", FormatRanges(newRanges));
            throw new InvalidOperationException("FillVisibleRangesForGamePreview output differs from GetVisibleRanges_PreviewMode.");
        }

        private static void ThrowMsecMismatch(int index, double currentY, double viewHeight, List<SoflanList.VisibleTGridRange> oldRanges, List<SoflanList.VisibleMsecRange> msecRanges, BpmList bpmList)
        {
            Console.Error.WriteLine("Msec mismatch at query {0}, currentY={1}, viewHeight={2}", index, currentY, viewHeight);
            Console.Error.WriteLine("Expected grids: {0}", FormatRanges(oldRanges));
            Console.Error.WriteLine("Expected: {0}", FormatMsecFromTGridRanges(oldRanges, bpmList));
            Console.Error.WriteLine("Actual: {0}", FormatMsecRanges(msecRanges));
            throw new InvalidOperationException("FillVisibleMsecRangesForGamePreview output differs from TGrid conversion path.");
        }

        private static string FormatRanges(List<SoflanList.VisibleTGridRange> ranges)
        {
            string[] items = new string[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
                items[i] = ranges[i].minTGrid.TotalGrid + ".." + ranges[i].maxTGrid.TotalGrid;
            return string.Join(", ", items);
        }

        private static string FormatMsecFromTGridRanges(List<SoflanList.VisibleTGridRange> ranges, BpmList bpmList)
        {
            string[] items = new string[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                double minMsec = TGridCalculator.ConvertTGridToAudioTime(ranges[i].minTGrid, bpmList).TotalMilliseconds;
                double maxMsec = TGridCalculator.ConvertTGridToAudioTime(ranges[i].maxTGrid, bpmList).TotalMilliseconds;
                items[i] = minMsec.ToString("F4") + ".." + maxMsec.ToString("F4");
            }
            return string.Join(", ", items);
        }

        private static string FormatMsecRanges(List<SoflanList.VisibleMsecRange> ranges)
        {
            string[] items = new string[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
                items[i] = ranges[i].MinMsec.ToString("F4") + ".." + ranges[i].MaxMsec.ToString("F4");
            return string.Join(", ", items);
        }

        private static Measurement Measure(string name, Func<long> action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            long memoryBefore = GC.GetTotalMemory(true);

            Stopwatch stopwatch = Stopwatch.StartNew();
            long checksum = action();
            stopwatch.Stop();

            long memoryAfter = GC.GetTotalMemory(false);
            return new Measurement
            {
                Name = name,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                Checksum = checksum,
                Gen0Collections = GC.CollectionCount(0) - gen0Before,
                Gen1Collections = GC.CollectionCount(1) - gen1Before,
                Gen2Collections = GC.CollectionCount(2) - gen2Before,
                MemoryDeltaBytes = memoryAfter - memoryBefore
            };
        }

        private static long RunOriginal(BenchmarkCase benchmarkCase, int iterations)
        {
            long checksum = 0;
            for (int i = 0; i < iterations; i++)
            {
                double currentY = benchmarkCase.CurrentYs[i % benchmarkCase.CurrentYs.Length];
                double viewHeight = benchmarkCase.ViewHeights[i % benchmarkCase.ViewHeights.Length];
                foreach (SoflanList.VisibleTGridRange range in benchmarkCase.SoflanList.GetVisibleRanges_PreviewMode(currentY, viewHeight, 0, benchmarkCase.BpmList, 1))
                    checksum += range.minTGrid.TotalGrid + range.maxTGrid.TotalGrid;
            }

            return checksum;
        }

        private static long RunFill(BenchmarkCase benchmarkCase, int iterations)
        {
            long checksum = 0;
            List<SoflanList.VisibleTGridRange> ranges = new List<SoflanList.VisibleTGridRange>(16);
            SoflanList.VisibleRangeQueryScratch scratch = new SoflanList.VisibleRangeQueryScratch();

            for (int i = 0; i < iterations; i++)
            {
                double currentY = benchmarkCase.CurrentYs[i % benchmarkCase.CurrentYs.Length];
                double viewHeight = benchmarkCase.ViewHeights[i % benchmarkCase.ViewHeights.Length];
                benchmarkCase.SoflanList.FillVisibleRangesForGamePreview(currentY, viewHeight, benchmarkCase.BpmList, ranges, scratch);

                for (int r = 0; r < ranges.Count; r++)
                    checksum += ranges[r].minTGrid.TotalGrid + ranges[r].maxTGrid.TotalGrid;
            }

            return checksum;
        }

        private static long RunFillWithMsecConversion(BenchmarkCase benchmarkCase, int iterations)
        {
            long checksum = 0;
            List<SoflanList.VisibleTGridRange> ranges = new List<SoflanList.VisibleTGridRange>(16);
            SoflanList.VisibleRangeQueryScratch scratch = new SoflanList.VisibleRangeQueryScratch();

            for (int i = 0; i < iterations; i++)
            {
                double currentY = benchmarkCase.CurrentYs[i % benchmarkCase.CurrentYs.Length];
                double viewHeight = benchmarkCase.ViewHeights[i % benchmarkCase.ViewHeights.Length];
                benchmarkCase.SoflanList.FillVisibleRangesForGamePreview(currentY, viewHeight, benchmarkCase.BpmList, ranges, scratch);

                for (int r = 0; r < ranges.Count; r++)
                {
                    double minMsec = TGridCalculator.ConvertTGridToAudioTime(ranges[r].minTGrid, benchmarkCase.BpmList).TotalMilliseconds;
                    double maxMsec = TGridCalculator.ConvertTGridToAudioTime(ranges[r].maxTGrid, benchmarkCase.BpmList).TotalMilliseconds;
                    checksum += MsecChecksum(minMsec, maxMsec);
                }
            }

            return checksum;
        }

        private static long RunFillMsec(BenchmarkCase benchmarkCase, int iterations)
        {
            long checksum = 0;
            List<SoflanList.VisibleMsecRange> ranges = new List<SoflanList.VisibleMsecRange>(16);
            SoflanList.VisibleRangeQueryScratch scratch = new SoflanList.VisibleRangeQueryScratch();

            for (int i = 0; i < iterations; i++)
            {
                double currentY = benchmarkCase.CurrentYs[i % benchmarkCase.CurrentYs.Length];
                double viewHeight = benchmarkCase.ViewHeights[i % benchmarkCase.ViewHeights.Length];
                benchmarkCase.SoflanList.FillVisibleMsecRangesForGamePreview(currentY, viewHeight, benchmarkCase.BpmList, ranges, scratch);

                for (int r = 0; r < ranges.Count; r++)
                    checksum += MsecChecksum(ranges[r].MinMsec, ranges[r].MaxMsec);
            }

            return checksum;
        }

        private static long MsecChecksum(double minMsec, double maxMsec)
        {
            return (long)Math.Round(minMsec * 1000) + (long)Math.Round(maxMsec * 1000);
        }

        private static void PrintMeasurement(Measurement measurement)
        {
            Console.WriteLine("{0}", measurement.Name);
            Console.WriteLine("  Elapsed: {0:F2} ms", measurement.ElapsedMs);
            Console.WriteLine("  Checksum: {0}", measurement.Checksum);
            Console.WriteLine("  GC: Gen0={0}, Gen1={1}, Gen2={2}", measurement.Gen0Collections, measurement.Gen1Collections, measurement.Gen2Collections);
            Console.WriteLine("  Memory delta: {0} bytes", measurement.MemoryDeltaBytes);
        }
    }
}
