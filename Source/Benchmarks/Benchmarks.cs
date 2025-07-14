using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Verse;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
	[MemoryDiagnoser]
	public class Benchmarks
	{
		public static void Main()
		{
			// Because the UnityEngine.CoreModule RW uses is labeld unoptimized
			ManualConfig config = ManualConfig.Create(DefaultConfig.Instance);
			config.WithOptions(ConfigOptions.DisableOptimizationsValidator);

			// Speed and Memory Benchmark
			BenchmarkRunner.Run<Benchmarks>(config);

			ValidateArray();
		}

		public static void ValidateArray()
		{
			// Validate all radii, patterns' radii, and output of NumCellsInRadius
			// The order of points with same radii may not be the same
			int errCount = 0;
			for (int i = 0; i < HarmonyCE_GenRadial.RadialPatternCount; i++)
			{
				float radius_HarmonyCE_GenRadial = HarmonyCE_GenRadial.RadialPatternRadii[i];
				float radius_HarmonyCE_GenRadial_pattern = HarmonyCE_GenRadial.RadialPattern[i].LengthHorizontal;
				float radius_GenRadialIncrease_radii = GenRadialIncrease.RadialPatternRadii[i];
				float radius_GenRadialIncrease_pattern = GenRadialIncrease.RadialPattern[i].LengthHorizontal;
				if (radius_HarmonyCE_GenRadial != radius_GenRadialIncrease_radii
					|| radius_HarmonyCE_GenRadial != radius_GenRadialIncrease_radii)
				{
					Console.WriteLine($"Different radius (" +
						$"{radius_HarmonyCE_GenRadial} " +
						$"vs {radius_HarmonyCE_GenRadial_pattern} " +
						$"vs {radius_GenRadialIncrease_radii} " +
						$"vs {radius_GenRadialIncrease_pattern}) " +
						$"in i = {i}");
					errCount++;
					if (errCount >= 10)
						break;
				}
			}
			Console.WriteLine($"Vector length: {Vector<float>.Count}");
			for (float r = 0f; r <= HarmonyCE_GenRadial.MAX_RADIUS; r += 0.1f)
			{
				HarmonyCE_GenRadial.NumCellsInRadius(out int result_HarmonyCE_GenRadial, r);
				GenRadialIncrease.Prefix_NumCellsInRadius(out int result_GenRadialIncrease, r);
				GenRadialIncrease.Simple_NumCellsInRadius(out int result_Simple, r);
				GenRadialIncrease.Analytic_NumCellsInRadius(out int result_Analytic, r);
				if (result_GenRadialIncrease != result_HarmonyCE_GenRadial ||
					result_GenRadialIncrease != result_Simple ||
					result_GenRadialIncrease != result_Analytic)
				{
					Console.WriteLine($"Different cell count " +
						$"({result_HarmonyCE_GenRadial} " +
						$"vs {result_GenRadialIncrease} " +
						$"vs {result_Simple} " +
						$"vs {result_Analytic}) " +
						$"for radius {r:f8}");
					errCount++;
					if (errCount >= 20)
						break;
				}
			}
			if (errCount == 0)
				Console.WriteLine("All cells validated to be good.");
		}

#pragma warning disable CA1822 // Benchmark methods must not be static

		[Benchmark]
		public void HarmonyCE_GenRadial_StaticConstr()
		{
			// Edits made:
			// 1. Changed namespace to Benchmark
			// 2. Commented out reference for HarmonyLib
			// 3. Commented out function Patch()
			// 4. Commented out unused private fields & debug logs
			// 5. Moved all code from static constructor into a new Init() function to be called
			// All else unchanged
			HarmonyCE_GenRadial.Init();
		}

		[Benchmark]
		public void HarmonyCE_GenRadial_NumCellsInRadius()
		{
			const float maxRadius = HarmonyCE_GenRadial.MAX_RADIUS;
			// Make the benchmark heavily lean toward lower inputs
			//for (float r = 1f, rsq; (rsq = r * r) <= maxRadius; r += 0.01f)
			//	HarmonyCE_GenRadial.NumCellsInRadius(out int _, rsq);
			for (float r = 1f; r <= maxRadius; r += 0.01f)
				HarmonyCE_GenRadial.NumCellsInRadius(out int _, r);
		}

		[Benchmark]
		public void GenRadialIncrease_StaticConstr()
		{
			// Edits made:
			// 1. Changed namespace to Benchmarks
			// 2. Commented out reference for HarmonyLib
			// 3. Commented out Harmony patching code
			// 4. Commented out unused private fields & debug logs
			// 5. Moved all code from static constructor into a new Init() function to be called
			// 6. Reduced radius to same as HarmonyCE
			// 7. Used unsafe code instead of BitConverter because i can't get it to run
			// All else unchanged
			GenRadialIncrease.InitArrays();
		}

		[Benchmark]
		public void GenRadialIncrease_NumCellsInRadius()
		{
			const float maxRadius = HarmonyCE_GenRadial.MAX_RADIUS;
			// Make the benchmark heavily lean toward lower inputs
			//for (float r = 1f, rsq; (rsq = r * r) <= maxRadius; r += 0.01f)
			//	GenRadialIncrease.Prefix_NumCellsInRadius(out int _, rsq);
			for (float r = 1f; r <= maxRadius; r += 0.01f)
				GenRadialIncrease.Prefix_NumCellsInRadius(out int _, r);
		}

		[Benchmark]
		public void Simple_NumCellsInRadius()
		{
			const float maxRadius = HarmonyCE_GenRadial.MAX_RADIUS;
			// Make the benchmark heavily lean toward lower inputs
			//for (float r = 1f, rsq; (rsq = r * r) <= maxRadius; r += 0.01f)
			//	GenRadialIncrease.Simple_NumCellsInRadius(out int _, rsq);
			for (float r = 1f; r <= maxRadius; r += 0.01f)
				GenRadialIncrease.Simple_NumCellsInRadius(out int _, r);
		}

		[Benchmark]
		public void Analytic_NumCellsInRadius()
		{
			// Full Analytic solution, no precalculated array
			const float maxRadius = HarmonyCE_GenRadial.MAX_RADIUS;
			// Make the benchmark heavily lean toward lower inputs
			//for (float r = 1f, rsq; (rsq = r * r) <= maxRadius; r += 0.01f)
			//	GenRadialIncrease.Prefix_NumCellsInRadius(out int _, rsq);
			for (float r = 1f; r <= maxRadius; r += 0.01f)
				GenRadialIncrease.Prefix_NumCellsInRadius(out int _, r);
		}

#pragma warning restore CA1822 // Methods can be marked static

	}
}
