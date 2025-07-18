

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public readonly struct IRDisk
{
	public readonly int index;
	public readonly int area;
	public readonly float radius;
	public readonly float meanDepth;
	public readonly Vector2 imgCenter;
	public readonly Vector3 worldCenter;
	public readonly Vector3 normal;
	public readonly bool isProper;

	public IRDisk(int index, int area, float radius, float meanDepth, bool isProper, float cx, float cy, Vector3 worldCenter, Vector3 normal)
	{
		this.index = index;// components[c];
		this.area = area;
		this.radius = radius;
		this.meanDepth = meanDepth;
		this.isProper = isProper;
		this.imgCenter = new Vector2(cx, cy);
		this.worldCenter = worldCenter;
		this.normal = normal;
	}

	public IRDisk(IRDisk copy, Vector3 newPos)
	{
		this.index = copy.index;// components[c];
		this.area = copy.area;
		this.radius = copy.radius;
		this.meanDepth = copy.meanDepth;
		this.isProper = false;
		this.imgCenter = copy.imgCenter;
		this.worldCenter = newPos;
		this.normal = copy.normal;
	}

	public override string ToString()
	{
		return $"[{worldCenter.x:0.000}, {worldCenter.y:0.000}, {worldCenter.z:0.000}]";
	}
}

public readonly struct Detection
{
	public readonly Vector3 center;
	public readonly IRDisk south;
	public readonly IRDisk west;
	public readonly IRDisk north;
	public readonly IRDisk east;
		    
	public readonly Vector3 forward;
	public readonly Vector3 left;
	public readonly Vector3 up;
	public readonly Quaternion orientation;

	public readonly bool isValid;

	public IRDisk this[int i]
	{
		get 
		{ 
			switch (i % 4)
			{
				case 0: return south;
				case 1: return west;
				case 2: return north;
				case 3: return east;
				default: throw new ArgumentException();
			} 
		}
	}

	public Detection(Vector3 center, IRDisk south, IRDisk west, IRDisk north, IRDisk east, Vector3 forward, Vector3 left, Vector3 up)
	{
		this.center = center; 
		this.south = south; 
		this.west = west;
		this.north = north;
		this.east = east;
		this.forward = forward;
		this.left = left; 
		this.up = up;
		this.orientation = Quaternion.LookRotation(left, up);
		this.isValid = true;
	}

	public override string ToString()
	{
		if (!isValid)
			return "Invalid";
		else
			return $"S: {south}, W: {west}, N: {north}, E: {east}";
	}


	public static Detection FromDetections(List<IRDisk> detections, Transform camTransform)
	{
		//detections.Shuffle();

		Debug.Assert(detections != null && detections.Count == 4);
		Vector3 center = new(0f, 0f, 0f);
		for (int f = 0; f < detections.Count; f++)
			center += detections[f].worldCenter;
		center /= detections.Count;

		(IRDisk south, int indexA) = FindFurtherFrom(center, detections);
		detections.RemoveAt(indexA);
		Vector3 forward = (south.worldCenter - center).normalized;

		// estimate the position where the left disk might be so we can search for it
		Vector3 guessedWest = Vector3.Cross(forward, (camTransform.position - center).normalized).normalized;
		(IRDisk west, int indexB) = FindFurtherFrom(center + guessedWest, detections);
		detections.RemoveAt(indexB);

		(IRDisk north, int indexC) = FindFurtherFrom(south.worldCenter, detections);
		detections.RemoveAt(indexC);

		IRDisk east = detections[0];
		detections.RemoveAt(0);

		Vector3 left = (west.worldCenter - center).normalized;
		Vector3 up = Vector3.Cross(forward, left).normalized;
		if (Vector3.Dot(up, camTransform.position - center) < 0)
			up *= -1f;


		detections.Add(south);
		detections.Add(west);
		detections.Add(north);
		detections.Add(east);

		return new Detection(center, south, west, north, east, forward, left, up);
	}

	private static float mean_neg = 0f;
	private static float mean_diff = 0f;
	private static float mean_diffrot = 0f;
	public static Detection Update(in Detection previous, List<IRDisk> detections, Transform camTransform)
	{
		Detection nd = FromDetections(detections, camTransform);
		//float truth = Vector3.Distance(nd.south.worldCenter, previous.north.worldCenter);

		//if (truth > 0f)
		//{
		//	float d = Vector3.Distance(previous.south.worldCenter, previous.center);
		//	Vector3 assump_neg = nd.center + d * (nd.center - nd.north.worldCenter).normalized;
		//	float score = (nd.south.worldCenter - assump_neg).magnitude / truth;
		//	if (!float.IsNaN(score))
		//		mean_neg = mean_neg * 0.99f + score * 0.01f;

		//	Vector3 co = (previous.west.worldCenter + previous.north.worldCenter + previous.east.worldCenter) / 3;
		//	Vector3 cn = (nd.west.worldCenter + nd.north.worldCenter + nd.east.worldCenter) / 3;
		//	Vector3 assump_diff = previous.south.worldCenter + (cn - co);
		//	score = (nd.south.worldCenter - assump_diff).magnitude / truth;
		//	if (!float.IsNaN(score))
		//		mean_diff = mean_diff * 0.99f + score * 0.01f;

		//	Vector3 no = Vector3.Cross(previous.north.worldCenter - previous.west.worldCenter, previous.east.worldCenter - previous.west.worldCenter);
		//	Vector3 nn = Vector3.Cross(nd.north.worldCenter - nd.west.worldCenter, nd.east.worldCenter - nd.west.worldCenter);
		//	Quaternion q = Quaternion.FromToRotation(no, nn);
		//	Vector3 assump_diffrot = cn + q * (previous.south.worldCenter - co);
		//	score = (nd.south.worldCenter - assump_diffrot).magnitude / truth;
		//	if (!float.IsNaN(score))
		//		mean_diffrot = mean_diffrot * 0.99f + score * 0.01f;
		//}

		//Debug.Log($"{mean_neg:0.0000} | {mean_diff:0.0000} | {mean_diffrot:0.0000}");
		return nd;
		//Debug.Assert(detections != null && detections.Count == 4);
		//Vector3 center = new(0f, 0f, 0f);
		//for (int f = 0; f < detections.Count; f++)
		//	center += detections[f].worldCenter;
		//center /= detections.Count;

		//Vector3 diff = center - previous.center; // to properly compare points, we need to move them to about the same space

		//(IRDisk south, int indexA) = FindClosestTo(previous.south.worldCenter + diff, detections);
		//detections.RemoveAt(indexA);
		//(IRDisk west, int indexB) = FindClosestTo(previous.west.worldCenter + diff, detections);
		//detections.RemoveAt(indexB);
		//(IRDisk north, int indexC) = FindClosestTo(previous.north.worldCenter + diff, detections);
		//detections.RemoveAt(indexC);
		//IRDisk east = detections[0];
		//detections.RemoveAt(0);

		//Vector3 forward = (south.worldCenter - center).normalized;
		//Vector3 left = (west.worldCenter - center).normalized;
		//Vector3 up = Vector3.Cross(forward, left).normalized;
		//if (Vector3.Dot(up, camTransform.position - center) < 0)
		//	up *= -1f;

		//detections.Add(south);
		//detections.Add(west);
		//detections.Add(north);
		//detections.Add(east);
		//return new Detection(center, south, west, north, east, forward, left, up);
	}
	
	
	public static (Detection, int) UpdateFrom3(in Detection previous, List<IRDisk> detections, Transform camTransform)
	{
		float targetScore = previous.EvaluateAssumption(previous);

		//Debug.Log("3 Frame Update");
		Debug.Assert(detections != null && detections.Count == 3);
		IRDisk a = detections[0];
		IRDisk b = detections[1];
		IRDisk c = detections[2];
		detections.Add(previous.ExtrapolateDisk(0, detections));
		Detection assumingSouth = Update(previous, detections, camTransform);
		Detection best = assumingSouth;
		float bestScore = AbsoluteError(targetScore, assumingSouth.EvaluateAssumption(previous));
		int assumption = 0;

		detections[0] = a;
		detections[1] = b;
		detections[2] = c; // faking west
		detections[3] = previous.ExtrapolateDisk(1, detections);
		Detection assumingWest = Update(previous, detections, camTransform);
		float score = AbsoluteError(targetScore, assumingWest.EvaluateAssumption(previous));
		if (score < bestScore)
		{
			best = assumingWest;
			bestScore = score;
			assumption = 1;
		}

		detections[0] = a;
		detections[1] = b;
		detections[2] = c;
		detections[3] = previous.ExtrapolateDisk(2, detections);
		Detection assumingNorth = Update(previous, detections, camTransform);
		score = AbsoluteError(targetScore, assumingNorth.EvaluateAssumption(previous));
		if (score < bestScore)
		{
			best = assumingNorth;
			bestScore = score;
			assumption = 2;
		}

		detections[0] = a;
		detections[1] = b;
		detections[2] = c;
		detections[3] = previous.ExtrapolateDisk(3, detections);
		Detection assumingEast = Update(previous, detections, camTransform);
		score = AbsoluteError(targetScore, assumingEast.EvaluateAssumption(previous));
		if (score < bestScore)
		{
			best = assumingEast;
			bestScore = score;
			assumption = 3;
		}

		detections[0] = best.south;
		detections[1] = best.west;
		detections[2] = best.north;
		detections[3] = best.east;
		return (best, assumption);
	}

	private static (IRDisk point, int index) FindClosestTo(Vector3 disk, List<IRDisk> detections)
	{
		int index = 0;
		float value = (detections[0].worldCenter - disk).sqrMagnitude;
		for (int i = 1; i < detections.Count; i++)
		{
			float v = (detections[i].worldCenter - disk).sqrMagnitude;
			if (v < value)
			{
				index = i;
				value = v;
			}
		}

		return (detections[index], index);
	}

	private static (IRDisk point, int index) FindFurtherFrom(Vector3 center, List<IRDisk> detections)
	{
		int index = 0;
		float value = (detections[0].worldCenter - center).sqrMagnitude;
		for (int i = 1; i < detections.Count; i++)
		{
			float v = (detections[i].worldCenter - center).sqrMagnitude;
			if (v > value) // find farthest away from center
			{
				index = i;
				value = v;
			}
		}

		return (detections[index], index);
	}

	private readonly IRDisk ExtrapolateDisk(int index, List<IRDisk> otherDisks)
	{
		// E.g., if we are missing south on current frame, find out the center of west/north/east on the past and current frame
		Vector3 _previous3Center = new(0f, 0f, 0f);
		for (int i = 0; i < 4; i++)
			if (i != index)
				_previous3Center += this[i].worldCenter;
		_previous3Center /= 3f;
		Vector3 _current3Center = (otherDisks[0].worldCenter + otherDisks[1].worldCenter + otherDisks[2].worldCenter) / 3f;

		// Given we know the past and current centers of the non missing disks, we can compute the relative position of the missing disk to the old 3-point center
		// and place it at that distance from the new 3-point center
		IRDisk lastKnownPosition = this[index];
		Vector3 currentPos = lastKnownPosition.worldCenter + (_current3Center - _previous3Center);
		return new IRDisk(lastKnownPosition, currentPos);
	}

	private readonly float EvaluateAssumption(Detection previous)
	{
		float score = 0;
		//score += Vector3.Magnitude(south.worldCenter - west.worldCenter)  > 0.018f ? 0 : 0.15f;
		//score += Vector3.Magnitude(south.worldCenter - north.worldCenter) > 0.018f ? 0 : 0.15f;
		//score += Vector3.Magnitude(south.worldCenter - east.worldCenter)  > 0.018f ? 0 : 0.15f;
		//score += Vector3.Magnitude(west.worldCenter  - north.worldCenter) > 0.018f ? 0 : 0.15f;
		//score += Vector3.Magnitude(west.worldCenter  - east.worldCenter)  > 0.018f ? 0 : 0.15f;
		//score += Vector3.Magnitude(north.worldCenter - east.worldCenter)  > 0.018f ? 0 : 0.15f;

		Vector3 correction = previous.center - center;
		score += Vector3.Magnitude(correction + south.worldCenter - previous.south.worldCenter);
		score += Vector3.Magnitude(correction + west.worldCenter - previous.west.worldCenter);
		score += Vector3.Magnitude(correction + north.worldCenter - previous.north.worldCenter);
		score += Vector3.Magnitude(correction + east.worldCenter - previous.east.worldCenter);

		//Vector3 s = (south.worldCenter - center).normalized;
		//Vector3 w = (west.worldCenter - center).normalized;
		//Vector3 n = (north.worldCenter - center).normalized;
		//Vector3 e = (east.worldCenter - center).normalized;
		//score += (Vector3.Dot(s, w)); // ideally, all will be close to 0
		//score += (Vector3.Dot(w, n));
		//score += (Vector3.Dot(n, e));
		//score += (Vector3.Dot(e, s));

		return score;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float AbsoluteError(float truth, float score)
	{
		//float error = score - truth;
		//return error < 0 ? -error : error;
		return score;
	}
}