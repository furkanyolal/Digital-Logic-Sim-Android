using UnityEngine;

namespace DLS.Description
{
	public struct SubChipDescription
	{
		public string Name;
		public int ID; // Unique within parent chip. ID > 0
		public string Label;
		public Vector2 Position;
		public int Rotation; // 0: 0, 1: 90, 2: 180, 3: 270
		public OutputPinColourInfo[] OutputPinColourInfo;

		// Arbitrary data for specific chip types:
		// ROM: stores memory contents
		// BUS: stores id of linked bus pair (origin/terminus), and horizontal flip value (0 = no, 1 = yes)
		// KEY: stores bound key code
		// Otherwise is null
		public uint[] InternalData;

		public SubChipDescription(string name, int id, string label, Vector2 position, int rotation, OutputPinColourInfo[] outputPinColInfo, uint[] internalData = null)
		{
			Name = name;
			ID = id;
			Label = label;
			Position = position;
			Rotation = rotation;
			OutputPinColourInfo = outputPinColInfo;
			InternalData = internalData;
		}
	}

	public struct OutputPinColourInfo
	{
		public PinColour PinColour;
		public int PinID;

		public OutputPinColourInfo(PinColour pinColour, int pinID)
		{
			PinColour = pinColour;
			PinID = pinID;
		}
	}
}