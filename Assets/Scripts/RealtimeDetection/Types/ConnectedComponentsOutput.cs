

using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Class used to preallocate the results of the connected component analysis algorithm
/// and reuse the same buffers every frame
/// </summary>
public class ConnectedComponentsOutput
{
	public const int MAX_COMPONENTS = 256;

	public Dictionary<int, List<int>> labelIndices;
	public Dictionary<int, int> equivalences;
	public List<int> components;
	public byte[] labels;

	public ConnectedComponentsOutput()
	{
		labelIndices = new Dictionary<int, List<int>>(MAX_COMPONENTS);
		equivalences = new Dictionary<int, int>(MAX_COMPONENTS);
		components = new List<int>(MAX_COMPONENTS);
		labels = new byte[512 * 512];
		for (int i = 0; i < MAX_COMPONENTS; i++)
			labelIndices[i] = new List<int>(1024);
	}

	public void Reset()
	{
		components.Clear();
		equivalences.Clear();
		for (int i = 0; i < MAX_COMPONENTS; i++)
			labelIndices[i].Clear();
	}
}