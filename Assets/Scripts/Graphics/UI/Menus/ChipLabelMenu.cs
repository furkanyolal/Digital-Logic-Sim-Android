using DLS.Game;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class ChipLabelMenu
	{
		const string MaxLabelLength = "MY LONG LABEL TEXT";
		static SubChipInstance subChip;
		static readonly UIHandle ID_NameField = new("ChipLabelMenu_NameField");
		static int openedFrame = -1;

		static readonly string[] CancelConfirmButtonNames =
		{
			"CANCEL", "CONFIRM"
		};

		static readonly bool[] ButtonGroupInteractStates = { true, true };

		public static void OnMenuOpened()
		{
			subChip = (SubChipInstance)ContextMenu.interactionContext;

			InputFieldState inputFieldState = UI.GetInputFieldState(ID_NameField);
			inputFieldState.SetText(subChip.Label, false); // Set text without forcing focus
			openedFrame = Time.frameCount;
			Seb.Helpers.Haptics.LightClick(Project.ActiveProject.description.Prefs_HapticFeedback);
		}

		public static void DrawMenu()
		{
			UI.DrawFullscreenPanel(DrawSettings.ActiveUITheme.MenuBackgroundOverlayCol);
			float spacing = 0.8f;

			DrawSettings.UIThemeDLS theme = DrawSettings.ActiveUITheme;
			InputFieldTheme inputTheme = DrawSettings.ActiveUITheme.ChipNameInputField;
			Draw.ID panelID = UI.ReservePanel();

			using (UI.BeginBoundsScope(true))
			{
				Vector2 unpaddedSize = Draw.CalculateTextBoundsSize(MaxLabelLength, inputTheme.fontSize, inputTheme.font);
				const float padX = 2.25f;
				Vector2 inputFieldSize = unpaddedSize + new Vector2(padX, 2.25f);
				Vector2 pos = UI.Centre + Vector2.up * 5;

				// Draw input field (forceFocus set to false)
				InputFieldState inputFieldState = UI.InputField(ID_NameField, inputTheme, pos, inputFieldSize, subChip.Label, Anchor.Centre, padX / 2, ValidateNameInput, false);
				Bounds2D inputFieldBounds = UI.PrevBounds;
				string newName = inputFieldState.text;

				// Draw cancel/confirm buttons
				Vector2 buttonsTopLeft = UI.PrevBounds.BottomLeft + Vector2.down * spacing;
				int buttonIndex = UI.HorizontalButtonGroup(CancelConfirmButtonNames, ButtonGroupInteractStates, theme.ButtonTheme, buttonsTopLeft, inputFieldBounds.Width, DrawSettings.DefaultButtonSpacing, 0, Anchor.TopLeft);

				MenuHelper.DrawReservedMenuPanel(panelID, UI.GetCurrentBoundsScope());

				// Safety: Ignore input for 2 frames after opening to prevent click bleed-through
				bool isSafe = (Time.frameCount - openedFrame) > 2;

				// Keyboard shortcuts and UI input
				bool cancelTriggered = (KeyboardShortcuts.CancelShortcutTriggered && isSafe) || (buttonIndex == 0 && isSafe);
				bool confirmTriggered = (KeyboardShortcuts.ConfirmShortcutTriggered && isSafe) || (buttonIndex == 1 && isSafe);

				if (cancelTriggered) Cancel();
				else if (confirmTriggered) Confirm(newName);
			}
		}

		static void Confirm(string newName)
		{
			subChip.Label = newName;
			Seb.Helpers.InputHelper.TouchSource?.CloseKeyboard();
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}

		static void Cancel()
		{
			Seb.Helpers.InputHelper.TouchSource?.CloseKeyboard();
			UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
		}

		static bool ValidateNameInput(string name) => name.Length <= MaxLabelLength.Length;
	}
}