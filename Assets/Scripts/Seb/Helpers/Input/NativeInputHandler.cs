using UnityEngine;
using System;

public class NativeInputHandler : MonoBehaviour
{
    public static Vector2 NativeHoverPosition;
    public static bool IsNativeHoveringThisFrame;
    public static bool IsNativeSPenButtonPressed;
    static int lastHoverFrame = -1;

    void Awake()
    {
        // Must persist to catch events in all scenes
        DontDestroyOnLoad(gameObject);
    }

    // Called asynchronously by the Java plugin
    public void OnHoverEvent(string coords)
    {
        string[] split = coords.Split(',');
        if (split.Length >= 2)
        {
            if (float.TryParse(split[0], out float x) && float.TryParse(split[1], out float y))
            {
                NativeHoverPosition = new Vector2(x, y);
                IsNativeHoveringThisFrame = true;
                lastHoverFrame = Time.frameCount;
            }
        }
        if (split.Length >= 3)
        {
            if (int.TryParse(split[2], out int btn))
            {
                IsNativeSPenButtonPressed = btn == 1;
            }
        }
    }

    public void OnHoverExit(string data)
    {
        IsNativeHoveringThisFrame = false;
        IsNativeSPenButtonPressed = false;
    }

    public void OnTouchButtonEvent(string data)
    {
        IsNativeSPenButtonPressed = data == "1";
    }

    void LateUpdate()
    {
        // We no longer reset IsNativeSPenButtonPressed every frame
        // to avoid "vanishing" during stationary hover.
        // It's reset by OnHoverExit or updated by events.
        if (Time.frameCount > lastHoverFrame)
        {
            IsNativeHoveringThisFrame = false;
        }
    }
}
