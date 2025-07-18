

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using System;


public static partial class Algorithms
{
	public static (int rowStart, int rowEnd, int colStart, int colEnd) GenerateLabelHints(List<IRDisk> previousFrameDisks)
	{
		Debug.Assert(previousFrameDisks != null && previousFrameDisks.Count == 4);
		int rowStart = (int)previousFrameDisks[0].imgCenter.x;
		int rowEnd = (int)previousFrameDisks[0].imgCenter.x;
		int colStart = (int)previousFrameDisks[0].imgCenter.y;
		int colEnd = (int)previousFrameDisks[0].imgCenter.y;

		for (int i = 0; i < 3; i++)
		{
			int r = (int)previousFrameDisks[i].imgCenter.x;
			int c = (int)previousFrameDisks[i].imgCenter.y;
			if (r < rowStart)
				rowStart = r;
			if (c < colStart)
				colStart = c;
			if (rowEnd < r)
				rowEnd = r;
			if (colEnd < c)
				colEnd = c;
		}

		//Debug.LogWarning($"{(int)(100 * ((rowEnd - rowStart + 128) * (colEnd - colStart + 128) / 166400f))}% of the original work");
		return (rowStart - 64, rowEnd + 64, colStart - 64, colEnd + 64);
	}
/*
	public static void ConnectedComponentAnalysis_4(DepthData depthData, DepthData irData, ConnectedComponentsOutput output, int rowStart = 64, int rowEnd = 512-48, int colStart = 48, int colEnd = 512 - 48)
	{
		Profiler.BeginSample("ConnectedComponents");
		output.Reset();

		// first pass labeling simple connections and finding equivalences
		byte leftLabel = 0;
		byte nextLabel = 1;

		if (rowStart < 2)
			rowStart = 2;
		if (rowEnd > 510)
			rowEnd = 510;
		if (colStart < 2)
			colStart = 2;
		if (colEnd > 510)
			colEnd = 510;

		Profiler.BeginSample("ConnectedComponents.InitialLabels");
		for (int r = rowStart; r < rowEnd; r++)
		{
			int i = r * 512 + colStart;
			for (int c = colStart; c < colEnd; c++, i++)
			{
				ushort d = depthData.pixels[i];
				ushort ir = irData.pixels[i];
				if (d < Settings.min_depth || d > Settings.max_depth || ir < Settings.min_ir || ir > Settings.max_ir)
				{
					output.labels[i] = 0;
					leftLabel = 0;
					continue;
				}

				byte upLabel = output.labels[i - 512];
				if (leftLabel > 0) // west connectivity
				{
					if (upLabel > 0 && upLabel != leftLabel) // both west and north
					{
						if (upLabel < leftLabel)
						{
							output.equivalences[leftLabel] = upLabel;
							leftLabel = upLabel;
						}
						else
						{
							output.equivalences[upLabel] = leftLabel;
						}
					}
					output.labels[i] = leftLabel;
				}
				else if (upLabel > 0) // north connectivity
				{
					leftLabel = output.labels[i - 512];
					output.labels[i] = leftLabel;
				}
				else
				{
					output.labels[i] = nextLabel;
					leftLabel = nextLabel;
					nextLabel++;
					if (nextLabel == 255) // too many labels, probably dirty data from sensor, better to abort
					{
						output.components.Clear();
						Profiler.EndSample();
						return;
					}
				}

				if (!output.labelIndices.ContainsKey(leftLabel))
					output.labelIndices[leftLabel] = new List<int>(128);
				output.labelIndices[leftLabel].Add(i);
			}
		}

		// merging equivalent regions until we get single islands
		Profiler.EndSample();
		Profiler.BeginSample("ConnectedComponents.Merging");
		foreach (var kvp in output.equivalences)
		{
			int target = kvp.Value;
			while (output.equivalences.ContainsKey(target))
				target = output.equivalences[target];

			output.labelIndices[target].AddRange(output.labelIndices[kvp.Key]);
			output.labelIndices[kvp.Key].Clear();
			if (!output.components.Contains(target))
				output.components.Add(target);
		}

		// pruning small components
		Profiler.EndSample();
		Profiler.BeginSample("ConnectedComponents.Pruning");
		for (int i = output.components.Count - 1; i >= 0; i--)
		{
			int c = output.components[i];
			if (output.labelIndices[c].Count <= Settings.cutoff_area)
			{
				output.labelIndices[c].Clear();
				output.components.RemoveAt(i);
			}
		}
		Profiler.EndSample();
		Profiler.EndSample();
	}
*/
	private readonly static byte[] _neighbours = new byte[4];
	public static void ConnectedComponentAnalysis_8(DepthData depthData, DepthData irData, ConnectedComponentsOutput output, int rowStart = 64, int rowEnd = 512 - 48, int colStart = 48, int colEnd = 512 - 48)
	{
		Profiler.BeginSample("ConnectedComponents");
		output.Reset();

		// first pass labeling simple connections and finding equivalences
		byte leftLabel = 0;
		byte nextLabel = 1;

		if (rowStart < 2)
			rowStart = 2;
		if (rowEnd > 510)
			rowEnd = 510;
		if (colStart < 2)
			colStart = 2;
		if (colEnd > 510)
			colEnd = 510;

		Profiler.BeginSample("ConnectedComponents.InitialLabels");
		for (int r = rowStart; r < rowEnd; r++)
		{
			int i = r * 512 + colStart;
			for (int c = colStart; c < colEnd; c++, i++)
			{
				ushort d = depthData.pixels[i];
				ushort ir = irData.pixels[i];
				if (d < Settings.min_depth || d > Settings.max_depth || ir < Settings.min_ir || ir > Settings.max_ir)
				{
					output.labels[i] = 0;
					leftLabel = 0;
					continue;
				}

				byte best = 255;
				int nn = 0;
				if (leftLabel > 0)
				{
					_neighbours[0] = leftLabel;
					best = leftLabel;
					nn++;
				}

				byte nlLabel = output.labels[i - 513];
				if (nlLabel > 0)
				{
					_neighbours[nn] = nlLabel;
					best = nlLabel < best ? nlLabel : best;
					nn++;
				}

				byte upLabel = output.labels[i - 512];
				if (upLabel > 0)
				{
					_neighbours[nn] = upLabel;
					best = upLabel < best ? upLabel : best;
					nn++;
				}

				byte nrLabel = output.labels[i - 511];
				if (nrLabel > 0)
				{
					_neighbours[nn] = nrLabel;
					best = nrLabel < best ? nrLabel : best;
					nn++;
				}

				if (nn == 0)
				{
					output.labels[i] = nextLabel;
					leftLabel = nextLabel;
					output.components.Add(leftLabel);
					nextLabel++;
					if (nextLabel == 255) // too many labels, probably dirty data from sensor, better to abort
					{
						output.components.Clear();
						Profiler.EndSample();
						return;
					}
				}
				else
				{
					output.labels[i] = best;
					leftLabel = best;
					for (int n = 0; n < nn; n++)
						if (_neighbours[n] != best)
							output.equivalences[_neighbours[n]] = best;
				}

				if (!output.labelIndices.ContainsKey(leftLabel))
					output.labelIndices[leftLabel] = new List<int>(128);
				output.labelIndices[leftLabel].Add(i);
			}
		}

		// merging equivalent regions until we get single islands
		Profiler.EndSample();
		Profiler.BeginSample("ConnectedComponents.Merging");
		foreach (var kvp in output.equivalences)
		{
			int target = kvp.Value;
			while (output.equivalences.ContainsKey(target))
				target = output.equivalences[target];

			output.labelIndices[target].AddRange(output.labelIndices[kvp.Key]);
			output.labelIndices[kvp.Key].Clear();
			if (output.components.Contains(kvp.Key))
				output.components.Remove(kvp.Key);
		}

		// pruning small components
		Profiler.EndSample();
		Profiler.BeginSample("ConnectedComponents.Pruning");
		for (int i = output.components.Count - 1; i >= 0; i--)
		{
			int c = output.components[i];
			if (output.labelIndices[c].Count <= Settings.cutoff_area)
			{
				output.labelIndices[c].Clear();
				output.components.RemoveAt(i);
			}
		}

		//if (output.components.Count == 0)
		//{
		//	Texture2D tex = irData.AsTexture(800);
		//	System.IO.File.WriteAllBytes($"found nothing {Time.frameCount}.png", tex.EncodeToPNG());
		//	Debug.LogError("Found nothing");
		//}

		Profiler.EndSample();
		Profiler.EndSample();
	}

	public static void FindIRDisks(List<IRDisk> findings, List<int> components, Dictionary<int, List<int>> labelIndices, Vector3[] lut, DepthData depthData, Matrix4x4 cam2world, Camera camera)
	{
		Profiler.BeginSample("FindHandle");
		Profiler.BeginSample("FindHandle.AggregatingStats");
		findings.Clear();

		int properDetections = 0;
		for (int c = 0; c < components.Count; c++)
		{
			List<int> indices = labelIndices[components[c]];
			int area = indices.Count;
			float radius = Mathf.Sqrt(area);
			float meanDepth = 0f;
			float cx = 0f;
			float cy = 0f;
			Vector3 p3D = new(0f, 0f, 0f);
			//NativeArray<float3> _points = new(area, Allocator.Temp);
			for (int idx = 0; idx < area; idx++)
			{
				int px = indices[idx];
				int i = px / 512;
				int j = px % 512;
				cx += px / 512;
				cy += px % 512;
				meanDepth += depthData.pixels[px];
				Vector3 p = lut[i * 512 + j] * depthData.pixels[i * 512 + j] / 1000f;
				//if (RealtimeDetector.UseWorldSpace)
				//	p = Algorithms.ConvertVectorToCoordinateSpace(p, cam2world);
				//_points[idx] = new float3(p.x, p.y, p.z);
				p3D += p;
			}

			meanDepth /= area;
			cx /= area;
			cy /= area;
			p3D /= area;
			if (RealtimeDetector.UseWorldSpace)
				p3D = Algorithms.ConvertVectorToCoordinateSpace(p3D, cam2world);

			float factor = meanDepth * radius;
			bool isProper = factor > Settings.depth_radius_min && factor < Settings.depth_radius_max;
			if (isProper)
				properDetections++;

			//Profiler.BeginSample("FindHandle.Normal");
			//Vector3 n = FindNormal(_points, p3D, camera.transform.forward, area);
			//Profiler.EndSample();
			Vector3 n = Vector3.up;

			findings.Add(new IRDisk(components[c], area, radius, meanDepth, isProper, cx, cy, p3D, n));
		}
		Profiler.EndSample();

		if (findings.Count < 4)
		{
			Profiler.EndSample();
			//Debug.LogError($"Only {findings.Count} components");
			return;
		}
		if (findings.Count == 4 && properDetections < 3)
		{
			Profiler.EndSample();
			//Debug.LogError($"Found {findings.Count} components but only {properDetections} proper depth-radius detections");
			return;
		}

		if (findings.Count == 4 && properDetections >= 3)
		{
			Profiler.EndSample();
			return;
		}

		Profiler.BeginSample("FindHandle.DistanceMatrix");
		Span<float> dists = stackalloc float[findings.Count];
		for (int i = 0; i < findings.Count; i++)
		{
			var finding = findings[i];
			float dist = 0f;
			for (int j = 0; j < findings.Count; j++)
			{
				if (i == j)
					continue;

				dist += Vector2.Distance(finding.imgCenter, findings[j].imgCenter);
			}

			dists[i] = dist;
		}
		Profiler.EndSample();

		Profiler.BeginSample("FindHandle.Pruning");
		float maxDist = dists[0];
		int maxIndex = 0;
		while (findings.Count > 4)
		{
			for (int i = 0; i < findings.Count; i++)
			{
				if (dists[i] > maxDist)
				{
					maxDist = dists[i];
					maxIndex = i;
				}
			}

			findings.RemoveAt(maxIndex);
			for (int i = maxIndex; i < dists.Length - 1; i++)
				dists[i] = dists[i + 1];

			maxDist = dists[0];
			maxIndex = 0;
		}
		Profiler.EndSample();
		Profiler.EndSample();
	}

/*
	[BurstCompile(FloatMode = FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
	private static float3 FindNormal(NativeArray<float3> points, float3 mean, float3 camForward, int n)
	{
		float3x3 covariance = new(0f);

		for (int i = 0; i < n; i++)
		{
			float3 diff = points[i] - mean;
			float3 row1 = diff.x * diff;
			float3 row2 = diff.y * diff;
			float3 row3 = diff.z * diff;
			covariance += new float3x3(row1, row2, row3);
		}

		covariance /= n - 1;

		float3 eigenVals = custom_svd.singularValuesDecomposition(covariance, out quaternion u, out quaternion v);
		float3x3 eigenVectors = new(v);
		float3 normal = eigenVectors.c2;
		float sign = math.sign(math.dot(normal, camForward));
		normal *= -sign; // the normal must point toward the camera

		return math.normalize(normal);
	}
	*/
}