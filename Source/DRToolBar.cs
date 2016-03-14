using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace DeadlyReentry
{
    [KSPAddon(KSPAddon.Startup.EveryScene, true)]
	public class DRToolbar : UnityEngine.MonoBehaviour
	{
		private static Rect windowPosition = new Rect(0,0,360,480);
		private static GUIStyle windowStyle = null;
        private static GUIStyle windowStyleCenter = null;

		#region Fields
		private GUISkin skins = HighLogic.Skin;
		private int id = Guid.NewGuid().GetHashCode();
		//private bool visible = false, showing = true;
		//private Rect window = new Rect(), button = new Rect();
		private Texture2D buttonTexture = new Texture2D(1, 1);
        private Texture Melificent = (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/Melificent", false);
		#endregion
		
		#region Properties
		private GUIStyle _buttonStyle = null;
		private GUIStyle buttonStyle
		{
			get
			{
				if (_buttonStyle == null)
				{
					_buttonStyle = new GUIStyle(skins.button);
					_buttonStyle.onNormal = _buttonStyle.hover;
				}
				return _buttonStyle;
			}
		}
		#endregion
		
		private ApplicationLauncherButton DRToolbarButton = null;
		private bool visible = false;
		
		public static DRToolbar Instance
		{
			get;
			private set;
		}
		
		DRToolbar ()
		{
			if (Instance == null) 
			{
				Instance = this;
			}
		}
		void Awake() 
		{
			// Set up the stock toolbar
			GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
            //Texture Melificent =
		}
		
		void Start() 
		{
            print("Start method called Initializing GUIs");
			if (HighLogic.LoadedScene >= GameScenes.SPACECENTER
				&& HighLogic.LoadedScene <= GameScenes.TRACKSTATION)
            {
				windowStyle = new GUIStyle (HighLogic.Skin.window);
                windowStyleCenter = new GUIStyle (HighLogic.Skin.window);
                windowStyleCenter.alignment = TextAnchor.MiddleCenter;
				RenderingManager.AddToPostDrawQueue (0, OnDraw);
                OnGUIAppLauncherReady();
			}
		}
		
		void OnGUIAppLauncherReady()
		{
			if (ApplicationLauncher.Ready && this.DRToolbarButton == null)
			{
				this.DRToolbarButton = ApplicationLauncher.Instance.AddModApplication(onAppLaunchToggleOn,
                                                                                      onAppLaunchToggleOff,
                                                                                      null,
                                                                                      null,
                                                                                      null,
                                                                                      null,
                                                                                      ApplicationLauncher.AppScenes.ALWAYS,
                                                                                      (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/DR_icon_off", false));
			}
            else
                print("OnGUIAppLauncherReady fired but AppLauncher not ready!");
		}

        void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
        {

        }
		
		void OnGUIAppLauncherDestroyed()
		{
            print("onGUIAppLauncherDestroyed() called");
            if (this.DRToolbarButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(this.DRToolbarButton);
				this.DRToolbarButton = null;
            }
		}
		
		void onAppLaunchToggleOn()
		{
            print("onAppLaunchToggleOn() called");
			this.DRToolbarButton.SetTexture((Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/DR_icon_on", false));
			this.visible = true;
		}
		
		void onAppLaunchToggleOff()
		{
			this.DRToolbarButton.SetTexture((Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/DR_icon_off", false));
			this.visible = false;
            print("onAppLaunchToggleOff() called");

		}
		
		
		void DummyVoid()
		{
		}
		private void OnDraw()
		{
			if (this.visible)
			{
				//Set the GUI Skin
				//GUI.skin = HighLogic.Skin;
				
				windowPosition = GUILayout.Window(id, windowPosition, OnWindow, "Deadly Reentry 7.3.1 - The Melificent Edition", windowStyle);
			}
		}
		private void OnDestroy()
		{
            print("OnDestroy() called - destroying button");
			// Remove the stock toolbar button
			GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
            if (this.DRToolbarButton != null)
				ApplicationLauncher.Instance.RemoveModApplication(DRToolbarButton);			
		}
		
		private void OnWindow(int windowID)
		{
            GUILayout.ExpandWidth(true);
            GUILayout.ExpandHeight(true);
            GUILayout.BeginVertical();

            GUILayout.Label("This space intentionally left blank.", windowStyleCenter);
            GUILayout.Label("For now, use alt-F12 then select Physics->Thermals to adjust reentry heating.", windowStyleCenter);
            GUILayout.Label(Melificent, windowStyleCenter);

            GUILayout.Width(0);
            GUILayout.Height(0);

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (GUI.changed)
            {
                DeadlyReentry.ReentryPhysics.SaveSettings();
                DeadlyReentry.ReentryPhysics.SaveCustomSettings();
                if (!DeadlyReentryScenario.Instance.displayCrewGForceWarning)
                    ScreenMessages.RemoveMessage(ReentryPhysics.crewGWarningMsg);
            }

		}

        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.DRToolBar] " + msg);
        }
    }
}
