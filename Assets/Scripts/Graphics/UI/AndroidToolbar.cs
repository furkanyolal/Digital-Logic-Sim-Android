using DLS.Game;
using Seb.Helpers;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	/// <summary>
	/// On-screen toolbar for Android that provides buttons for actions
	/// that normally require keyboard shortcuts (Undo, Redo, Delete, Cancel, Confirm).
	/// Only renders on touch platforms.
	/// </summary>
	public static class AndroidToolbar
	{
		// Flags read by KeyboardShortcuts — reset each frame
		public static bool UndoPressed { get; private set; }
		public static bool RedoPressed { get; private set; }
		public static bool DeletePressed { get; private set; }
		public static bool CancelPressed { get; private set; }
		public static bool ConfirmPressed { get; private set; }
		public static bool PausePressed { get; private set; }
		public static bool StepPressed { get; private set; }

		const float marginRight = 0.5f;
		const float marginTop = 0.5f;
		const float spacing = 0.25f;

		public static void Draw()
		{
			if (!InputHelper.IsTouchPlatform) return;

			// Reset flags at the start of each draw
			UndoPressed = false;
			RedoPressed = false;
			DeletePressed = false;
			CancelPressed = false;
			ConfirmPressed = false;
			PausePressed = false;
			StepPressed = false;

			// Don't draw toolbar in main menu or blocking menus
			if (UIDrawer.ActiveMenu is UIDrawer.MenuType.MainMenu) return;
			if (UIDrawer.InInputBlockingMenu()) return;

			ButtonTheme theme = DrawSettings.ActiveUITheme.MenuButtonTheme;

			bool isTopLeft = Project.ActiveProject != null && Project.ActiveProject.description.Prefs_ToolbarPlacement == 1;
			Anchor anchor = isTopLeft ? Anchor.TopLeft : Anchor.TopRight;
			float posX = isTopLeft ? marginRight : UI.Width - marginRight;
			float posY = UI.Height - marginTop;

			// Helper to draw a button and update posX
			bool DrawToolbarButton(string text)
			{
				bool pressed = UI.Button(text, theme, new Vector2(posX, posY), true, anchor);
				float offset = UI.PrevBounds.Width + spacing;
				posX += isTopLeft ? offset : -offset;
				if (pressed) Seb.Helpers.Haptics.LightClick(Project.ActiveProject.description.Prefs_HapticFeedback);
				return pressed;
			}

			if (DrawToolbarButton("X")) CancelPressed = true;
			if (DrawToolbarButton("OK")) ConfirmPressed = true;
			if (DrawToolbarButton("DEL")) DeletePressed = true;
			if (DrawToolbarButton(">>")) RedoPressed = true;
			if (DrawToolbarButton("<<")) UndoPressed = true;

			if (Project.ActiveProject != null)
			{
				bool isPaused = Project.ActiveProject.simPaused;
				string pauseLabel = isPaused ? "PLAY" : "PAUSE";
				if (DrawToolbarButton(pauseLabel))
				{
					Project.ActiveProject.description.Prefs_SimPaused = !isPaused;
				}
				
				if (Project.ActiveProject.simPaused)
				{
					if (DrawToolbarButton("STEP"))
					{
						Project.ActiveProject.advanceSingleSimStep = true;
					}
				}
			}

			if (DrawToolbarButton("FPS") && Project.ActiveProject != null)
			{
				Project.ActiveProject.showDebugOverlay = !Project.ActiveProject.showDebugOverlay;
			}
		}
	}
}
