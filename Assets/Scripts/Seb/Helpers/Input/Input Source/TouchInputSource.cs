using UnityEngine;

namespace Seb.Helpers.InputHandling
{
	/// <summary>
	/// Touch-based IInputSource implementation for Android.
	/// Maps single-finger touch to left mouse button, long-press to right mouse button.
	/// Falls back to UnityInputSource for keyboard keys when a physical keyboard is connected.
	/// </summary>
	public class TouchInputSource : IInputSource
	{
		const float LongPressDuration = 0.4f;
		const float LongPressMoveThreshold = 15f; // pixels - if finger moves more than this, cancel long press
		const float TouchSmoothSpeed = 25f; // Higher = more responsive, lower = smoother

		// Touch state
		Vector2 touchPosition;
		Vector2 rawTouchPosition;
		bool isTouching;
		bool touchStartedThisFrame;
		bool touchEndedThisFrame;
		float touchStartTime;
		Vector2 touchStartPosition;
		bool longPressTriggered;
		bool longPressTriggeredThisFrame;
		bool longPressEndedThisFrame;

		// S Pen state
		bool sPenButtonPressed;
		bool sPenButtonStartedThisFrame;
		bool sPenButtonEndedThisFrame;

		// S Pen hover state
		public bool IsHovering { get; private set; }
		Vector2 lastHoverPosition;

		int prevFrameCount = -1;

		// Two-finger gesture state (exposed for CameraController)
		public bool IsPinching { get; private set; }
		public float PinchDelta { get; private set; }   // change in distance between two fingers
		public bool IsTwoFingerDragging { get; private set; }
		public Vector2 TwoFingerDragDelta { get; private set; }
		
		Vector2 prevTwoFingerMidpoint;
		float prevPinchDistance;

		public Vector2 MousePosition => touchPosition;
		public bool AnyKeyOrMouseDownThisFrame => touchStartedThisFrame || longPressTriggeredThisFrame || Input.anyKeyDown;
		public bool AnyKeyOrMouseHeldThisFrame => isTouching || Input.anyKey;
		public string InputString => touchKeyboardInputThisFrame;
		public Vector2 MouseScrollDelta => Vector2.zero; // Zoom is handled via pinch
		public InputTouchType CurrentTouchType { get; private set; }

		// TouchScreenKeyboard support
		TouchScreenKeyboard touchKeyboard;
		string touchKeyboardPrevText = "";
		string touchKeyboardInputThisFrame = "";
		int pendingBackspaces;
		bool backspaceThisFrame;

		// Multi-tap detection
		public static bool TwoFingerDoubleTapThisFrame { get; private set; }
		public static bool ThreeFingerDoubleTapThisFrame { get; private set; }
		float lastTwoFingerTapTime = -1;
		float lastThreeFingerTapTime = -1;
		int maxFingersThisInteraction = 0;
		bool fingerInteractionEnded = true;

		// Palm Rejection & Stylus Tracking
		float lastStylusActivityTime = -1;
		float lastStylusHoverTime = -1;
		bool isStylusInteractionInProgress;
		const float PalmRejectionCooldown = 0.5f;

		public void UpdateTouchState()
		{
			// Prevent double-update in same frame
			if (Time.frameCount == prevFrameCount) return;
			prevFrameCount = Time.frameCount;

			touchStartedThisFrame = false;
			touchEndedThisFrame = false;
			longPressTriggeredThisFrame = false;
			longPressEndedThisFrame = false;
			sPenButtonStartedThisFrame = false;
			sPenButtonEndedThisFrame = false;
			PinchDelta = 0f;
			TwoFingerDragDelta = Vector2.zero;
			touchKeyboardInputThisFrame = "";
			backspaceThisFrame = false;
			TwoFingerDoubleTapThisFrame = false;
			ThreeFingerDoubleTapThisFrame = false;

			// Read new characters from TouchScreenKeyboard
			if (touchKeyboard != null && touchKeyboard.status == TouchScreenKeyboard.Status.Visible)
			{
				string currentText = touchKeyboard.text ?? "";
				// Check for backspace (text got shorter)
				if (currentText.Length < touchKeyboardPrevText.Length)
				{
					pendingBackspaces += (touchKeyboardPrevText.Length - currentText.Length);
				}
				else if (currentText.Length > touchKeyboardPrevText.Length)
				{
					// For Android, just append all new characters. 
					// StartsWith check can fail if the user moves the cursor and types.
					// We'll just take the difference if possible, or simplified:
					if (currentText.StartsWith(touchKeyboardPrevText))
					{
						touchKeyboardInputThisFrame = currentText.Substring(touchKeyboardPrevText.Length);
					}
					else
					{
						// If the whole string changed (e.g. autocorrect replacement), just take the new one
						// This is complex to sync perfectly without a full diff, 
						// but let's at least handle appending at the end.
						touchKeyboardInputThisFrame = currentText.Substring(Mathf.Min(currentText.Length, touchKeyboardPrevText.Length));
					}
				}
				touchKeyboardPrevText = currentText;
			}
			else
			{
				pendingBackspaces = 0;
			}

			if (pendingBackspaces > 0)
			{
				backspaceThisFrame = true;
				pendingBackspaces--;
			}

			int originalTouchCount = Input.touchCount;
			
			// Detect if current touch is explicitly a Stylus
			bool containsStylusTouch = false;
			for (int i = 0; i < originalTouchCount; i++)
			{
				if (Input.GetTouch(i).type == UnityEngine.TouchType.Stylus)
				{
					containsStylusTouch = true;
					break;
				}
			}

			// --- Stylus & Palm Rejection Pre-Processing ---
			bool isStylusHovering = NativeInputHandler.IsNativeHoveringThisFrame;
			// Don't fall back to GetMouseButton(1) unless it's explicitly a stylus touch or we're using Native Plugin.
			// Android often maps two-finger touches to GetMouseButton(1), which was accidentally triggering palm rejection.
			bool isSPenButtonNow = NativeInputHandler.IsNativeSPenButtonPressed || (containsStylusTouch && Input.GetMouseButton(1));
			
			if (isStylusHovering || isSPenButtonNow || containsStylusTouch)
			{
				lastStylusActivityTime = Time.time;
				if (isStylusHovering) lastStylusHoverTime = Time.time;
			}
			
			int touchCount = originalTouchCount;
			
			bool isFirstTouchStylusType = touchCount > 0 && Input.GetTouch(0).type == UnityEngine.TouchType.Stylus;
			
			if (touchCount > 0 && !isStylusInteractionInProgress)
			{
				Vector2 firstTouchPos = Input.GetTouch(0).position;
				float distToHover = Vector2.Distance(firstTouchPos, touchPosition);
				bool isLikelySPenTipHandoff = (Time.time - lastStylusHoverTime < 0.15f) && distToHover < 150f;

				if (isFirstTouchStylusType || isLikelySPenTipHandoff)
				{
					isStylusInteractionInProgress = true;
				}
			}

			if (isStylusInteractionInProgress)
			{
				lastStylusActivityTime = Time.time;
				if (touchCount == 0 && !isStylusHovering)
				{
					isStylusInteractionInProgress = false;
				}
			}

			// Apply Palm Rejection
			bool isPalmRejectionCooldownActive = (Time.time - lastStylusActivityTime) < PalmRejectionCooldown;
			if (isPalmRejectionCooldownActive && touchCount > 0 && !isStylusInteractionInProgress)
			{
				// Only reject if the first touch is NOT a stylus and we aren't in a confirmed stylus interaction
				touchCount = 0;
			}

			// Finger Tap Tracking
			if (touchCount > 0)
			{
				if (fingerInteractionEnded)
				{
					fingerInteractionEnded = false;
					maxFingersThisInteraction = 0;
					touchStartTime = Time.time;
				}
				if (touchCount > maxFingersThisInteraction)
				{
					maxFingersThisInteraction = touchCount;
				}
			}
			else if (!fingerInteractionEnded)
			{
				fingerInteractionEnded = true;
				
				// Evaluate tap when interaction ends
				float now = Time.time;
				float interactionDuration = now - touchStartTime;
				// Only consider it a "tap" if it was short (e.g. < 0.25s)
				bool isShortTap = interactionDuration < 0.25f;

				if (isShortTap)
				{
					if (maxFingersThisInteraction == 2)
					{
						if (now - lastTwoFingerTapTime < 0.4f)
						{
							TwoFingerDoubleTapThisFrame = true;
							lastTwoFingerTapTime = -1; // Reset to avoid triple-tap triggering it again instantly
						}
						else lastTwoFingerTapTime = now;
					}
					else if (maxFingersThisInteraction == 3)
					{
						if (now - lastThreeFingerTapTime < 0.4f)
						{
							ThreeFingerDoubleTapThisFrame = true;
							lastThreeFingerTapTime = -1;
						}
						else lastThreeFingerTapTime = now;
					}
				}
			}

			// --- Two-finger gestures ---
			if (touchCount == 2)
			{
				Touch t0 = Input.GetTouch(0);
				Touch t1 = Input.GetTouch(1);
				
				Vector2 midpoint = (t0.position + t1.position) / 2f;
				float pinchDistance = Vector2.Distance(t0.position, t1.position);

				if (!IsPinching && !IsTwoFingerDragging)
				{
					// Starting a two-finger gesture
					IsPinching = true;
					IsTwoFingerDragging = true;
					prevPinchDistance = pinchDistance;
					prevTwoFingerMidpoint = midpoint;
					
					// Cancel any single-finger interaction
					if (isTouching)
					{
						touchEndedThisFrame = true;
						isTouching = false;
						longPressTriggered = false;
					}
				}
				else
				{
					PinchDelta = pinchDistance - prevPinchDistance;
					TwoFingerDragDelta = midpoint - prevTwoFingerMidpoint;
					prevPinchDistance = pinchDistance;
					prevTwoFingerMidpoint = midpoint;
				}

				// Keep touch position at midpoint during two-finger gestures
				touchPosition = midpoint;
				return;
			}

			// Handle more than 2 fingers by doing nothing (let tap tracker handle it)
			if (touchCount > 2)
			{
				if (IsPinching || IsTwoFingerDragging)
				{
					IsPinching = false;
					IsTwoFingerDragging = false;
				}
				return;
			}

			// End two-finger gesture
			if (IsPinching || IsTwoFingerDragging)
			{
				if (touchCount < 2)
				{
					IsPinching = false;
					IsTwoFingerDragging = false;
					// Don't start a new single-finger touch immediately after a two-finger gesture ends
					return;
				}
			}

			// --- Single-finger touch ---
			if (touchCount == 1)
			{
				Touch touch = Input.GetTouch(0);
				rawTouchPosition = touch.position;
				CurrentTouchType = (touch.type == UnityEngine.TouchType.Stylus) ? InputTouchType.Stylus : InputTouchType.Direct;

				if (isSPenButtonNow && !sPenButtonPressed) sPenButtonStartedThisFrame = true;
				if (!isSPenButtonNow && sPenButtonPressed) sPenButtonEndedThisFrame = true;
				sPenButtonPressed = isSPenButtonNow;

				switch (touch.phase)
				{
					case UnityEngine.TouchPhase.Began:
						isTouching = true;
						touchStartedThisFrame = true;
						touchStartTime = Time.time;
						touchStartPosition = touch.position;
						longPressTriggered = false;
						IsHovering = false;
						touchPosition = touch.position;
						break;

					case UnityEngine.TouchPhase.Moved:
					case UnityEngine.TouchPhase.Stationary:
						if (!isTouching)
						{
							isTouching = true;
							touchStartedThisFrame = true;
							touchStartTime = Time.time;
							touchStartPosition = touch.position;
							IsHovering = false;
						}
						// Use raw touch position
						touchPosition = rawTouchPosition;
						// Check for long press
						if (!longPressTriggered && isTouching)
						{
							float fingerMoveDist = Vector2.Distance(touch.position, touchStartPosition);
							bool fingerStayedStill = fingerMoveDist < LongPressMoveThreshold;
							bool heldLongEnough = (Time.time - touchStartTime) >= LongPressDuration;

							if (heldLongEnough && fingerStayedStill)
							{
								longPressTriggered = true;
								longPressTriggeredThisFrame = true;
							}
						}
						break;

					case UnityEngine.TouchPhase.Ended:
					case UnityEngine.TouchPhase.Canceled:
						isTouching = false;
						touchEndedThisFrame = true;
						if (longPressTriggered)
						{
							longPressEndedThisFrame = true;
							longPressTriggered = false;
						}
						break;
				}
			}
			else if (touchCount == 0)
			{
				if (isTouching)
				{
					isTouching = false;
					touchEndedThisFrame = true;
					if (longPressTriggered)
					{
						longPressEndedThisFrame = true;
						longPressTriggered = false;
					}
				}

				// Force explicit reset
				IsHovering = false;
				
				// 1. Native Java Plugin Pipeline (Definitive Solution)
				if (NativeInputHandler.IsNativeHoveringThisFrame)
				{
					IsHovering = true;
					touchPosition = NativeInputHandler.NativeHoverPosition;
					CurrentTouchType = InputTouchType.Stylus;
				}
				else
				{
					// 2. Legacy execution route: Android OS maps hover to mouse position
					Vector2 currentMousePos = Input.mousePosition;
					
					// SqrMagnitude filter > 1.0f prevents microscopic OS jitter from triggering false hovers
					if ((currentMousePos - lastHoverPosition).sqrMagnitude > 1.0f)
					{
						IsHovering = true;
						touchPosition = currentMousePos;
						CurrentTouchType = InputTouchType.Stylus;
					}
					lastHoverPosition = currentMousePos;
				}

				if (isSPenButtonNow && !sPenButtonPressed) sPenButtonStartedThisFrame = true;
				if (!isSPenButtonNow && sPenButtonPressed) sPenButtonEndedThisFrame = true;
				
				sPenButtonPressed = isSPenButtonNow;
			}
		}

		public bool IsKeyDownThisFrame(KeyCode key)
		{
			// Backspace from TouchScreenKeyboard
			if (key == KeyCode.Backspace)
			{
				return backspaceThisFrame;
			}
			
			// Return/Enter from TouchScreenKeyboard (keyboard Done button)
			if (key == KeyCode.Return && touchKeyboard != null && touchKeyboard.status == TouchScreenKeyboard.Status.Done)
			{
				return true;
			}
			return Input.GetKeyDown(key);
		}
		public bool IsKeyUpThisFrame(KeyCode key) => Input.GetKeyUp(key);
		public bool IsKeyHeld(KeyCode key)
		{
			if (key == KeyCode.Backspace) return backspaceThisFrame;
			return Input.GetKey(key);
		}

		/// <summary>Open the Android software keyboard for text input.</summary>
		public void OpenKeyboard(string initialText = "")
		{
			if (touchKeyboard != null && touchKeyboard.status == TouchScreenKeyboard.Status.Visible) return;
			touchKeyboardPrevText = initialText;
			touchKeyboard = TouchScreenKeyboard.Open(initialText, TouchScreenKeyboardType.Default, false, false, false, false, "");
			TouchScreenKeyboard.hideInput = true;
		}

		/// <summary>Close the Android software keyboard.</summary>
		public void CloseKeyboard()
		{
			if (touchKeyboard != null)
			{
				touchKeyboard.active = false;
				touchKeyboard = null;
				touchKeyboardPrevText = "";
			}
		}

		public bool IsMouseDownThisFrame(MouseButton button)
		{
			return button switch
			{
				MouseButton.Left => touchStartedThisFrame,
				MouseButton.Right => longPressTriggeredThisFrame || sPenButtonStartedThisFrame,
				_ => false
			};
		}

		public bool IsMouseUpThisFrame(MouseButton button)
		{
			return button switch
			{
				MouseButton.Left => touchEndedThisFrame,
				MouseButton.Right => longPressEndedThisFrame || sPenButtonEndedThisFrame,
				_ => false
			};
		}

		public bool IsMouseHeld(MouseButton button)
		{
			return button switch
			{
				MouseButton.Left => isTouching,
				MouseButton.Right => longPressTriggered || sPenButtonPressed,
				_ => false
			};
		}
	}
}
