package com.seblague.input;

import android.os.Build;
import android.os.Bundle;
import android.view.MotionEvent;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.view.WindowInsetsController;
import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

public class HoverActivity extends UnityPlayerActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Window window = getWindow();

        // Render content behind display cutouts (notch / punch-hole) — eliminates black bars
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            WindowManager.LayoutParams lp = window.getAttributes();
            lp.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            window.setAttributes(lp);
        }

        // Draw behind system bars (status bar, navigation bar)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            window.setDecorFitsSystemWindows(false);
            WindowInsetsController controller = window.getInsetsController();
            if (controller != null) {
                controller.hide(android.view.WindowInsets.Type.systemBars());
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else {
            // Fallback for API < 30: immersive sticky flags
            window.getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
            );
        }
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        // Re-apply immersive mode whenever the window regains focus
        if (hasFocus && Build.VERSION.SDK_INT < Build.VERSION_CODES.R) {
            getWindow().getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
            );
        }
    }

    @Override
    public boolean dispatchGenericMotionEvent(MotionEvent ev) {
        int action = ev.getActionMasked();
        if (ev.getToolType(0) == MotionEvent.TOOL_TYPE_STYLUS) {
            if (action == MotionEvent.ACTION_HOVER_MOVE || 
                action == MotionEvent.ACTION_HOVER_ENTER || 
                action == MotionEvent.ACTION_BUTTON_PRESS || 
                action == MotionEvent.ACTION_BUTTON_RELEASE) 
            {
                float x = ev.getX();
                float y = getWindowManager().getDefaultDisplay().getHeight() - ev.getY();
                int buttonState = ev.getButtonState();
                boolean isPrimaryPressed = (buttonState & MotionEvent.BUTTON_STYLUS_PRIMARY) != 0;
                UnityPlayer.UnitySendMessage("NativeInputHandler", "OnHoverEvent", x + "," + y + "," + (isPrimaryPressed ? "1" : "0"));
            } else if (action == MotionEvent.ACTION_HOVER_EXIT) {
                UnityPlayer.UnitySendMessage("NativeInputHandler", "OnHoverExit", "");
            }
        }
        return super.dispatchGenericMotionEvent(ev);
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent ev) {
        if (ev.getToolType(0) == MotionEvent.TOOL_TYPE_STYLUS) {
            int buttonState = ev.getButtonState();
            boolean isPrimaryPressed = (buttonState & MotionEvent.BUTTON_STYLUS_PRIMARY) != 0;
            UnityPlayer.UnitySendMessage("NativeInputHandler", "OnTouchButtonEvent", isPrimaryPressed ? "1" : "0");
        }
        return super.dispatchTouchEvent(ev);
    }
}
