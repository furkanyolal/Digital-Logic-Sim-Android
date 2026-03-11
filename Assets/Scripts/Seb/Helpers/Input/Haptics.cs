using UnityEngine;

namespace Seb.Helpers
{
	public static class Haptics
	{
#if UNITY_ANDROID && !UNITY_EDITOR
		static AndroidJavaObject vibrator;
#endif

		public static void Init()
		{
#if UNITY_ANDROID && !UNITY_EDITOR
			try
			{
				using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				using AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
				vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
			}
			catch (System.Exception e)
			{
				Debug.LogWarning("Failed to init vibrator: " + e.Message);
			}
#endif
		}

		public static void LightClick(bool enabled)
		{
			if (!enabled) return;
			
#if UNITY_ANDROID && !UNITY_EDITOR
			if (vibrator != null)
			{
				try
				{
					vibrator.Call("vibrate", 15L); // 15ms is a light tap
				}
				catch {}
			}
#endif
		}
		
		public static void HeavyClick(bool enabled)
		{
			if (!enabled) return;
			
#if UNITY_ANDROID && !UNITY_EDITOR
			if (vibrator != null)
			{
				try
				{
					vibrator.Call("vibrate", 30L); // 30ms is a heavier tap
				}
				catch {}
			}
#endif
		}
	}
}