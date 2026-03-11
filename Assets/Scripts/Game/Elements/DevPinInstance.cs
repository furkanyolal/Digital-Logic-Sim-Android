using System;
using DLS.Description;
using DLS.Simulation;
using Seb.Helpers;
using Seb.Types;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Game
{
	public class DevPinInstance : IMoveable
	{
		public readonly PinBitCount BitCount;
		public readonly char[] decimalDisplayCharBuffer = new char[16];

		// Size/Layout info
		public readonly Vector2 faceDir;

		public readonly bool IsInputPin;
		public readonly string Name;
		public readonly PinInstance Pin;
		public readonly Vector2Int StateGridDimensions;
		public readonly Vector2 StateGridSize;

		public PinValueDisplayMode pinValueDisplayMode;

		public DevPinInstance(PinDescription pinDescription, bool isInput)
		{
			Name = pinDescription.Name;
			ID = pinDescription.ID;
			IsInputPin = isInput;
			Position = pinDescription.Position;
			Rotation = pinDescription.Rotation;
			BitCount = pinDescription.BitCount;

			Pin = new PinInstance(pinDescription, new PinAddress(ID, 0), this, isInput);
			pinValueDisplayMode = pinDescription.ValueDisplayMode;

			// Calculate layout info
			faceDir = new Vector2(IsInputPin ? 1 : -1, 0);
			StateGridDimensions = BitCount switch
			{
				PinBitCount.Bit1 => new Vector2Int(1, 1),
				PinBitCount.Bit4 => new Vector2Int(2, 2),
				PinBitCount.Bit8 => new Vector2Int(4, 2),
				_ => throw new Exception("Bit count not implemented")
			};
			StateGridSize = BitCount switch
			{
				PinBitCount.Bit1 => Vector2.one * (DevPinStateDisplayRadius * 2 + DevPinStateDisplayOutline * 2),
				_ => (Vector2)StateGridDimensions * MultiBitPinStateDisplaySquareSize + Vector2.one * DevPinStateDisplayOutline
			};
		}

		public Vector2 HandlePosition => Position;
		public Vector2 StateDisplayPosition => HandlePosition + RotateVector(faceDir * (DevPinHandleWidth / 2 + StateGridSize.x / 2 + 0.065f), Rotation);

		public Vector2 PinPosition
		{
			get
			{
				int gridDst = BitCount is PinBitCount.Bit1 or PinBitCount.Bit4 ? 6 : 9;
				return HandlePosition + RotateVector(faceDir * (GridSize * gridDst), Rotation);
			}
		}


		public Vector2 Position { get; set; }
		public int Rotation { get; set; }
		public Vector2 MoveStartPosition { get; set; }

		public static Vector2 RotateVector(Vector2 v, int rotation)
		{
			return rotation switch
			{
				1 => new Vector2(-v.y, v.x),
				2 => new Vector2(-v.x, -v.y),
				3 => new Vector2(v.y, -v.x),
				_ => v
			};
		}
		public Vector2 StraightLineReferencePoint { get; set; }
		public int ID { get; }

		public bool IsSelected { get; set; }
		public bool HasReferencePointForStraightLineMovement { get; set; }
		public bool IsValidMovePos { get; set; }

		public Bounds2D SelectionBoundingBox => CreateBoundingBox(SelectionBoundsPadding);

		public Bounds2D BoundingBox => CreateBoundingBox(0);


		public Vector2 SnapPoint => Pin.GetWorldPos();

		public bool ShouldBeIncludedInSelectionBox(Vector2 selectionCentre, Vector2 selectionSize)
		{
			Bounds2D selfBounds = SelectionBoundingBox;
			return Maths.BoxesOverlap(selectionCentre, selectionSize, selfBounds.Centre, selfBounds.Size);
		}

		public int GetStateDecimalDisplayValue()
		{
			uint rawValue = PinState.GetBitStates(Pin.State);
			int displayValue = (int)rawValue;

			if (pinValueDisplayMode == PinValueDisplayMode.SignedDecimal)
			{
				displayValue = Maths.TwosComplement(rawValue, (int)BitCount);
			}

			return displayValue;
		}

		Bounds2D CreateBoundingBox(float pad)
		{
			Vector2 p1 = HandlePosition - RotateVector(faceDir * (DevPinHandleWidth / 2), Rotation);
			Vector2 p2 = PinPosition + RotateVector(faceDir * PinRadius, Rotation);
			
			float minX = Mathf.Min(p1.x, p2.x);
			float maxX = Mathf.Max(p1.x, p2.x);
			float minY = Mathf.Min(p1.y, p2.y);
			float maxY = Mathf.Max(p1.y, p2.y);

			float h = BoundsHeight();
			if (Rotation % 2 == 0)
			{
				minY = Mathf.Min(minY, HandlePosition.y - h / 2);
				maxY = Mathf.Max(maxY, HandlePosition.y + h / 2);
			}
			else
			{
				minX = Mathf.Min(minX, HandlePosition.x - h / 2);
				maxX = Mathf.Max(maxX, HandlePosition.x + h / 2);
			}

			Vector2 centre = new((minX + maxX) / 2, (minY + maxY) / 2);
			Vector2 size = new(maxX - minX, maxY - minY);
			return Bounds2D.CreateFromCentreAndSize(centre, size + Vector2.one * pad);
		}

		public Bounds2D HandleBounds()
		{
			Vector2 size = GetHandleSize();
			if (Rotation % 2 != 0) (size.x, size.y) = (size.y, size.x);
			return Bounds2D.CreateFromCentreAndSize(HandlePosition, size);
		}

		public float BoundsHeight() => StateGridSize.y;

		public Vector2 GetHandleSize() => new(DevPinHandleWidth, BoundsHeight());

		public void ToggleState(int bitIndex)
		{
			PinState.Toggle(ref Pin.PlayerInputState, bitIndex);
		}

		public bool PointIsInInteractionBounds(Vector2 point) => PointIsInHandleBounds(point) || PointIsInStateIndicatorBounds(point);

		public bool PointIsInStateIndicatorBounds(Vector2 point) => Maths.PointInCircle2D(point, StateDisplayPosition, DevPinStateDisplayRadius);

		public bool PointIsInHandleBounds(Vector2 point) => HandleBounds().PointInBounds(point);
	}
}