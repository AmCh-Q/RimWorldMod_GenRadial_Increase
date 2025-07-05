using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using System.Reflection;

namespace GenRadialIncrease
{
	public class GenRadialIncrease(ModContentPack content) : Mod(content)
	{
		public const int MaxRadii = 200;
		public const int RadialPatternLength = 125629;
		public static readonly Harmony harmony = new(id: "AmCh.GenRadialIncrease");
		public static IntVec3[] RadialPattern;
		public static float[] RadialPatternRadii;

		static GenRadialIncrease()
		{
			// Reserve Capacity to RadialPatternLength
			List<IntVec3> list = new(RadialPatternLength);

			// Only iterate over 1/8 of the circle
			// MaxRadii >= i >= j >= 0 
			for (int i = 0; i <= MaxRadii; i++)
			{
				int max_j_squared = Mathf.Min(i * i, MaxRadii * MaxRadii - i * i);
				for (int j = 0; j * j <= max_j_squared; j++)
				{
					list.Add(new IntVec3(i, 0, j));
					if (i != 0)
						list.Add(new IntVec3(-i, 0, j));
					if (i != 0 && j != 0)
						list.Add(new IntVec3(-i, 0, -j));
					if (j != 0)
						list.Add(new IntVec3(i, 0, -j));
					if (i == j)
						continue;
					list.Add(new IntVec3(j, 0, i));
					if (j != 0)
						list.Add(new IntVec3(-j, 0, i));
					if (i != 0 && j != 0)
						list.Add(new IntVec3(-j, 0, -i));
					if (i != 0)
						list.Add(new IntVec3(j, 0, -i));
				}
			}
			// Same sort as vanilla
			list.Sort((IntVec3 A, IntVec3 B)
				=> A.LengthHorizontalSquared - B.LengthHorizontalSquared
			);

			// Convert to arrays then set
			RadialPattern = [.. list];
			RadialPatternRadii = [.. list.Select(x => x.LengthHorizontal)];
			typeof(GenRadial)
				.GetField(nameof(GenRadial.RadialPattern))
				.SetValue(null, RadialPattern);
			typeof(GenRadial)
				.GetField(nameof(GenRadial.RadialPatternRadii))
				.SetValue(null, RadialPatternRadii);

			// Patches
			harmony.Patch(typeof(GenRadial)
				.GetProperty(nameof(GenRadial.MaxRadialPatternRadius))
				.GetGetMethod(),
				prefix: new HarmonyMethod(((Delegate)Prefix_MaxRadialPatternRadius).Method));
			harmony.Patch(typeof(GenRadial)
				.GetMethod(nameof(GenRadial.NumCellsInRadius)),
				prefix: new HarmonyMethod(((Delegate)Prefix_NumCellsInRadius).Method));

			// Debug sanity check
			Log.Message($"[GenRadial Increase]: RadialPatternLength = {RadialPattern.Length}");
		}
		public static bool Prefix_MaxRadialPatternRadius(out float __result)
		{
			// Forcibly replace the method so vanilla doesn't get confused by the original length
			// (which may've been inlined in the original method)
			__result = RadialPatternRadii[^1];
			return false;
		}
		public static bool Prefix_NumCellsInRadius(out int __result, float radius)
		{
			// Trivial cases
			if (radius < 0f)
			{
				__result = 0;
				return false;
			}
			if (radius < 1f)
			{
				__result = 1;
				return false;
			}
			if (RadialPattern.Length != RadialPatternLength)
			{
				// Another mod has modified the RadialPattern
				// Let them handle it for compatibility
				Log.WarningOnce(
					"[GenRadial Increase]: Another Mod has modified RadialPattern, skipping to avoid conflicts.",
					unchecked((int)0xFDF6E429)); // CRC32 hash of above message
				__result = RadialPattern.Length;
				return true;
			}
			if (radius >= MaxRadii)
			{
				Log.Error($"Not enough squares to get to radius {radius}. Max is {MaxRadii}");
				__result = RadialPatternLength;
				return false;
			}

			// Estimate of the number of cells
			float cellEst = Mathf.PI * radius * radius;
			/* Read: M. N. Huxley (2003)
				4 is chosen to be the coefficient because it's large enough
				as well as being a power of 2/**/
			// float errorBound = 4f * Mathf.Pow(radius, 131f / 208f);
			float errorBound = BitConverter.Int32BitsToSingle(
				(BitConverter.SingleToInt32Bits(radius) >> 16)
				* 0xA13B + 0x187FF1C9); // WTF
			/* More seriously: this is what it did
			static float FastPow(float a, float b)
			{
				int x = BitConverter.SingleToInt32Bits(a);
				x = (int)(b * (x - 0x3F7893F8)) + 0x3F7893F8;
				// Line above gets simplified when either a or b are constant
				// x = (int)(b * x) - (int)(b * 0x3F7893F8) + 0x3F7893F8
				// x = (x >> 16) + (int)(b * (1 << 16)) + (int)((1f - b) * (float)0x3F7893F8)
				
				// Here, (b * (1 << 16)) is chosen to be 0xA13B
				// And for ((1f - b) * (float)0x3F7893F8)
				// 0x177FF1C9 has the lowest max error percentage
				// When scanning "a" among "1:0.00001:100"
				// Then to multiply by 4, it becomes 0x187FF1C9
				return BitConverter.Int32BitsToSingle(x);
			}/**/

			int lowerBound = Mathf.Max(Mathf.FloorToInt(cellEst - errorBound), 1);
			int upperBound = Mathf.Min(Mathf.CeilToInt(cellEst + errorBound), RadialPatternLength);
			// If the gap is large, perform binary search
			while (upperBound - lowerBound > 64)
			{
				int mid = (upperBound + lowerBound) / 2;
				if (RadialPatternRadii[mid] > radius)
					upperBound = mid;
				else
					lowerBound = mid;
			}
			// Less than 64 floats in gap, no more than 5 cache lines
			// So linear search is faster
			int cellCount = lowerBound;
			while (cellCount < upperBound &&
				RadialPatternRadii[cellCount] <= radius)
				cellCount++;
			__result = cellCount;
			return false;
		}
	}
}
