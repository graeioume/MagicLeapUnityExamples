
using System.Collections.Generic;

public struct DepthMetaData
{
	public float[,,] depth_mesh;
	public float[,] rig2cam;
	public float[,] cam2rig;
	public Dictionary<long, float[,]> rig2world;
}
