﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using UnityEngine;
using HarmonyLib;
using Verse;

namespace GenRadialIncrease
{
	public class GenRadialIncrease(ModContentPack content) : Mod(content)
	{
		//public const int MaxRadius = 119;
		//public const int RadialPatternLength = 44469;
		public const int MaxRadius = 200;
		public const int RadialPatternLength = 125629; // Use analytical method to pre-calculate

		public static readonly Harmony harmony = new(id: "AmCh.GenRadialIncrease");
		public static IntVec3[] RadialPattern = new IntVec3[RadialPatternLength];
		public static float[] RadialPatternRadii = new float[RadialPatternLength];

		// Everything gets triggered here
		static GenRadialIncrease()
		{
			InitArrays();
			SetArrays();
			Patch();
		}

		// Populate the replacement arrays for GenRadial.RadialPattern
		// and GenRadial.RadialPatternRadii
		// Scanning and sorting only 1/8 of the circle
		// Much faster than vanilla
		public static void InitArrays()
		{
			const int MaxRadiusSquared = MaxRadius * MaxRadius;
			const float SqrtHalf = 0.707106781f;
			const int DiagonalWidth = (int)(MaxRadius * SqrtHalf);

			// Manual i == j == 0
			RadialPattern[0] = new IntVec3(0, 0, 0);
			int idx = 1;

			// Only iterate over 1/8 of the circle
			// MaxRadii >= i >= j > 0
			for (int x = 1; x <= MaxRadius; x++)
			{
				int xSquared = x * x;
				// Case 1: maxY is on diagonal
				// Case 2: maxY is on arc
				int maxZSquared;
				if (x <= DiagonalWidth)
					maxZSquared = xSquared;
				else
					maxZSquared = MaxRadiusSquared - xSquared;
				// Create one entry per (i,j) combination
				// Use the unused y entry to store radius squared
				for (int z = 0, zSquared; (zSquared = z * z) <= maxZSquared; z++)
					RadialPattern[idx++] = new IntVec3(x, xSquared + zSquared, z);
			}

			// Same sort as vanilla, but
			//   (1) Much shorter -- just over 1/8 as many points
			//   (2) Custom comparator exploiting unused y entry
			// We know the first 9 and the last 1 are guaranteed sorted
			//   (00 10 11 20 21 22 30 31 32)
			// So the length is idx - 10
			Array.Sort(RadialPattern, 9, idx - 10, new IntVec3Compare());

			// Populate the array back to front
			// Actually perform the reflections to duplicate points
			for (int i = RadialPatternLength - 1; --idx > 0;)
			{
				// Retrieve the radius squared saved in y
				float y = Mathf.Sqrt(RadialPattern[idx].y);
				// Retrieve xz
				int x = RadialPattern[idx].x;
				int z = RadialPattern[idx].z;

				// All points can be x-reflected (axis reflection)
				// And if z > 0, z can be reflected too (axis reflection)
				RadialPatternRadii[i] = y;
				RadialPatternRadii[i - 1] = y;
				RadialPattern[i] = new IntVec3(x, 0, z);
				RadialPattern[i - 1] = new IntVec3(-x, 0, z);
				i -= 2;
				if (z != 0)
				{
					RadialPatternRadii[i] = y;
					RadialPatternRadii[i - 1] = y;
					RadialPattern[i] = new IntVec3(-x, 0, -z);
					RadialPattern[i - 1] = new IntVec3(x, 0, -z);
					i -= 2;
				}
				// if x != z, we can also swap them (diagonal reflection)
				if (x == z)
					continue;
				RadialPatternRadii[i] = y;
				RadialPatternRadii[i - 1] = y;
				RadialPattern[i] = new IntVec3(z, 0, x);
				RadialPattern[i - 1] = new IntVec3(z, 0, -x);
				i -= 2;
				if (z != 0)
				{
					RadialPatternRadii[i] = y;
					RadialPatternRadii[i - 1] = y;
					RadialPattern[i] = new IntVec3(-z, 0, x);
					RadialPattern[i - 1] = new IntVec3(-z, 0, -x);
					i -= 2;
				}
			}
		}

		// Use standard reflection to set the arrays
		// Instead of using Harmony
		// - CE says that causes problems but I didn't verify
		public static void SetArrays()
		{
			if (GenRadial.RadialPattern.Length != 10000)
				return; // Another mod has already patched the array, stop
			typeof(GenRadial)
				.GetField(nameof(GenRadial.RadialPattern))
				.SetValue(null, RadialPattern);
			typeof(GenRadial)
				.GetField(nameof(GenRadial.RadialPatternRadii))
				.SetValue(null, RadialPatternRadii);
		}

		public static void Patch()
		{
			if (GenRadial.RadialPattern.Length != 10000)
				return; // Another mod has already patched the array, stop
			harmony.Patch(typeof(GenRadial)
				.GetMethod(nameof(GenRadial.NumCellsInRadius)),
				prefix: new HarmonyMethod(((Delegate)Prefix_NumCellsInRadius).Method));
		}

		// A replacement method for GenRadial.NumCellsInRadius
		// Calculates the number of cells (lattice points) within radius
		// Much faster than vanilla
		// The problem is also called "Gauss's Circle Problem"
		// Read: https://mathworld.wolfram.com/GausssCircleProblem.html
		public static bool Prefix_NumCellsInRadius(out int __result, float radius)
		{
			// Handle edge cases
			if (radius < 0f)
			{
				__result = 0;
				return false;
			}
			if (radius >= MaxRadius)
			{
				if (radius > MaxRadius)
				{
					LogNotEnoughSquaresError(radius);
				}
				__result = RadialPatternLength;
				return false;
			}

			// Estimate the result using area of a circle
			// This estimation is actually really good
			// with error <= 100 for all radius <= 200
			// See: https://www.desmos.com/calculator/qerpfljbgw
			int idx = (int)(radius * radius * Mathf.PI);

			// Apply upper bound to avoid IndexOutOfRangeError
			// Subtract 6 so the next step can't raise past the upperbound
			if (idx >= RadialPatternLength - 6)
			{
				idx = RadialPatternLength - 6;
			}

			// Since a circle has 8-way symmetry (axis + diagonals, forming the 8 octants)
			//   the final answers are always the sum of the following:
			// - 1, for the center cell
			// - 4*a, where "a = floor(radius)" is the number of cells on the +x axis
			// - 4*d, where "d = floor(radius * sqrt(1/2))" is the count on the +x+z diagonal
			// - 8*i, where "i" is the number of cells inside the x > z > 0 octant
			// Therefore, if a+d is even, the answer is guaranteed in the form "8n + 1"
			//         and if a+d is odd, the answer is guaranteed in the form "8n + 5"
			// We can determine the last three bits of the answer as 1 or 5 (0b101)
			//   and skip every 8 cells when searching
			{
				const float SqrtHalf = 0.707106781f;
				int a = (int)radius;
				int d = (int)(radius * SqrtHalf);
				idx = (idx & -7) | (((a + d) % 2 == 0) ? 1 : 5);
			}

			// Linear search every 8 cells starting from the middle of estimation
			if (RadialPatternRadii[idx] <= radius)
			{
				do { idx += 8; } // Search Upward
				while (idx < RadialPatternLength && RadialPatternRadii[idx] <= radius);
				__result = idx;
			}
			else
			{
				do { idx -= 8; } // Search Downward
				while (idx >= 0 && RadialPatternRadii[idx] > radius);
				__result = idx + 8; // We overshot, so add 8 back
			}
			return false;
		}

		// A helper subclass for InitArrays()
		// So we can use Array.Sort() with custom comparison
		// AND dictate starting and ending idx
		private sealed class IntVec3Compare : IComparer<IntVec3>
		{
			// y is already storing radius squared 
			// so compare them directly, skipping multiplications
			public int Compare(IntVec3 a, IntVec3 b)
				=> a.y - b.y;
		}

		// A separate method to handle when radius exceeds MaxRadius
		// Apparently loading all these strings for formatting is very slow
		// Even when they are not executed, likely due to branch predictions
		// Separating the rare & slow stuff to this method helps a lot
		private static void LogNotEnoughSquaresError(float radius)
		{
			Log.Error($"Not enough squares to get to radius {radius}. Max is {MaxRadius}");
		}

		// This method solves using an analytical (exact) solution
		// Read: https://mathworld.wolfram.com/GausssCircleProblem.html
		// Warning! While theoretically "exact", the method is only exact mathematically
		// And float32 (or float64) are not perfect beings with perfect precision
		// As a result, this method fails on some very specific radii, such as 90.21086
		// Luckily, all not-so-precise numbers are spared from such errors
		// And when it does err, it only errs by a couple edge cells
		public static bool Analytic_NumCellsInRadius(out int __result, float radius)
		{
			// Special cases
			if (radius < 1f || radius >= MaxRadius)
			{
				if (radius < 0f)
					__result = 0;
				else if (radius < 1f)
					__result = 1;
				else
				{
					__result = RadialPatternLength;
					if (radius > MaxRadius)
						LogNotEnoughSquaresError(radius);
				}
				return false;
			}

			// Vectorized implementation
			int rFloor = (int)radius;
			float rSquared = radius * radius;
			int sum = rFloor;
			int vectorLength = Vector<float>.Count;

			// Create increment vector [0, 1, 2, ..., vectorLength - 1]
			float[] incrementArr = new float[vectorLength];
			for (int j = 0; j < vectorLength; j++)
				incrementArr[j] = j;
			Vector<float> incrementVec = new(incrementArr);
			// Create rSquared vector [rSquared, rSquared, ...]
			Vector<float> v_rSquared = new(rSquared);

			int i = 1;
			int lastFullSeg = rFloor - vectorLength + 1;
			// Process vector
			for (; i <= rFloor; i += vectorLength)
			{
				Vector<float> vi = new Vector<float>(i) + incrementVec;
				Vector<int> vResult = Vector.ConvertToInt32(
					Vector.SquareRoot(v_rSquared - vi * vi)
				);
				int remainder = (i <= lastFullSeg) ? vectorLength : rFloor - i + 1;
				for (int j = 0; j < remainder; j++)
					sum += vResult[j];
			}

			// Return
			__result = sum * 4 + 1;
			return false;
		}
	}
}
