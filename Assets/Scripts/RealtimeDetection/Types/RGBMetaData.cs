
using System.Collections.Generic;

public struct RGBMetaData
{
	public Intrinsics intrinsics;
	public Dictionary<long, FocusPoint> focusPoints;
	public Dictionary<long, float[,]> rig2world;
}

public struct Intrinsics
{
	public float principal_point_x;
	public float principal_point_y;
	public int width;
	public int height;
}

public struct FocusPoint
{
	public float fx;
	public float fy;
}
