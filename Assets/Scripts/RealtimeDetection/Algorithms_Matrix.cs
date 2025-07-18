using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;


public static partial class Algorithms
{
	public static Vector3[] LUTFromFloats(float[,,] rawlut)
	{
		Vector3[] lut = new Vector3[512 * 512];
		for (int i = 0; i < 512; i++)
			for (int j = 0; j < 512; j++)
				lut[i * 512 + j] = new Vector3(rawlut[i, j, 0], rawlut[i, j, 1], rawlut[i, j, 2]);

		return lut;
	}

	public static Matrix4x4 MatrixFromFloats(float[,] m)
	{
		Vector4 row1 = new Vector4(m[0, 0], m[0, 1], m[0, 2], m[0, 3]);
		Vector4 row2 = new Vector4(m[1, 0], m[1, 1], m[1, 2], m[1, 3]);
		Vector4 row3 = new Vector4(m[2, 0], m[2, 1], m[2, 2], m[2, 3]);
		Vector4 row4 = new Vector4(m[3, 0], m[3, 1], m[3, 2], m[3, 3]);
		return new Matrix4x4(row1, row2, row3, row4).transpose;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Vector3 ConvertVectorToCoordinateSpace(Vector3 point, Matrix4x4 transform)
	{
		return transform * new Vector4(point.x, point.y, point.z, 1f);
	}
}