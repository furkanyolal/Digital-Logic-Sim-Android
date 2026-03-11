using UnityEngine;

namespace DLS.Description
{
	public struct PinDescription
	{
		public string Name;
		public int ID;
		public Vector2 Position;
		public int Rotation; // 0: 0, 1: 90, 2: 180, 3: 270
		public PinBitCount BitCount;
		public PinColour Colour;
		public PinValueDisplayMode ValueDisplayMode;

		public PinDescription(string name, int id, Vector2 position, int rotation, PinBitCount bitCount, PinColour colour, PinValueDisplayMode valueDisplayMode)
		{
			Name = name;
			ID = id;
			Position = position;
			Rotation = rotation;
			BitCount = bitCount;
			Colour = colour;
			ValueDisplayMode = valueDisplayMode;
		}
	}

	public enum PinBitCount
	{
		Bit1 = 1,
		Bit4 = 4,
		Bit8 = 8
	}

	public enum PinColour
	{
		Red,
		Orange,
		Yellow,
		Green,
		Blue,
		Violet,
		Pink,
		White
	}

	public enum PinValueDisplayMode
	{
		Off,
		UnsignedDecimal,
		SignedDecimal,
		HEX
	}
}