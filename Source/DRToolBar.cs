using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;

namespace DeadlyReentry
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class DRToolbar : MonoBehaviour
	{
		#region Fields
        private static Rect windowPosition = new Rect(0,0,360,480);
        private static GUIStyle windowStyle = null;
        private static GUIStyle labelStyle = null;
        private static GUIStyle windowStyleCenter = null;

        private GUISkin skins = HighLogic.Skin;
		private int id = Guid.NewGuid().GetHashCode();
		//private bool visible = false, showing = true;
		//private Rect window = new Rect(), button = new Rect();
        private Texture2D buttonTexture = new Texture2D(32, 32);
        private Texture Melificent = (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/Melificent", false);
        private string DREVersionString = "";
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
		
		DRToolbar ()
		{
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            DREVersionString = string.Format("{0}.{1}.{2}", fileVersionInfo.FileMajorPart, fileVersionInfo.FileMinorPart, fileVersionInfo.FileBuildPart);
            //Melificent.height /= 2;
            //Melificent.width /= 2;
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
                windowStyle.stretchHeight = true;
                windowStyleCenter = new GUIStyle (HighLogic.Skin.window);
                windowStyleCenter.alignment = TextAnchor.MiddleCenter;
                labelStyle = new GUIStyle(HighLogic.Skin.label);
                labelStyle.fixedHeight = labelStyle.lineHeight + 4f;
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
                print("OnGUIAppLauncherReady fired but AppLauncher not ready or button already created!");
		}

        void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
        {

        }
		
		void OnGUIAppLauncherDestroyed()
		{
            print("onGUIAppLauncherDestroyed() called");
            if (DRToolbarButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(DRToolbarButton);
                DRToolbarButton = null;
            }
        }
		
		void onAppLaunchToggleOn()
        {
            print("onAppLaunchToggleOn() called");
            DRToolbarButton.SetTexture((Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/DR_icon_on", false));
            visible = true;
		}
		
		void onAppLaunchToggleOff()
		{
			DRToolbarButton.SetTexture((Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/DR_icon_off", false));
			visible = false;
            print("onAppLaunchToggleOff() called");

		}
		
		
		void DummyVoid()
		{
        }
		private void OnDraw()
		{
			if (visible)
			{
				//Set the GUI Skin
                //GUI.skin = HighLogic.Skin;

                windowPosition = GUILayout.Window(id, windowPosition, OnWindow, "Deadly Reentry " + DREVersionString + " - The Melificent Edition", windowStyle);
            }
        }
		private void OnDestroy()
		{
            print("OnDestroy() called - destroying button");
			// Remove the stock toolbar button
			GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
            if (DRToolbarButton != null)
                ApplicationLauncher.Instance.RemoveModApplication(DRToolbarButton);			
		}
		
        private void OnWindow(int windowID)
        {
            GUILayout.ExpandWidth(true);
            GUILayout.ExpandHeight(true);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("G Tolerance Mult:", labelStyle);
            string newGToleranceMult = GUILayout.TextField(ReentryPhysics.gToleranceMult.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("Crew G Max", labelStyle);
            string newcrewGClamp = GUILayout.TextField(ReentryPhysics.crewGClamp.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("Crew G Exponent", labelStyle);
            string newcrewGPower = GUILayout.TextField(ReentryPhysics.crewGPower.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("Crew G Min", labelStyle);
            string newcrewGMin = GUILayout.TextField(ReentryPhysics.crewGMin.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("Crew G Warn Level", labelStyle);
            string newcrewGWarn = GUILayout.TextField(ReentryPhysics.crewGWarn.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("Crew G Kill threshold", labelStyle);
            string newcrewGLimit = GUILayout.TextField(ReentryPhysics.crewGLimit.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label("Crew G Kill chance per update", labelStyle);
            string newcrewGKillChance = GUILayout.TextField(ReentryPhysics.crewGKillChance.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.Width(0);
            GUILayout.Height(0);
            GUILayout.Label("For other thermal settings, press F-12 then select Physics->Thermals.", windowStyleCenter);
            GUILayout.Label(Melificent, windowStyleCenter);

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (GUI.changed)
            {
                //print("GUI CHANGED!!!111oneone");
                float newValue;

                if (float.TryParse(newGToleranceMult, out newValue))
                {
                    ReentryPhysics.gToleranceMult = newValue;  
                }

                if (float.TryParse(newcrewGClamp, out newValue))
                {
                    ReentryPhysics.crewGClamp = newValue;
                }

                if (float.TryParse(newcrewGPower, out newValue))
                {
                    ReentryPhysics.crewGPower = newValue;
                }

                if (float.TryParse(newcrewGMin, out newValue))
                {
                    ReentryPhysics.crewGMin = newValue;
                }
                if (float.TryParse(newcrewGWarn, out newValue))
                {
                    ReentryPhysics.crewGWarn = newValue;
                }
                if (float.TryParse(newcrewGLimit, out newValue))
                {
                    ReentryPhysics.crewGLimit = newValue;
                }
                if (float.TryParse(newcrewGKillChance, out newValue))
                {
                    ReentryPhysics.crewGKillChance = newValue;
                }
                DeadlyReentry.ReentryPhysics.SaveSettings();
                DeadlyReentry.ReentryPhysics.SaveCustomSettings();
                if (!DeadlyReentryScenario.displayCrewGForceWarning)
                    ScreenMessages.RemoveMessage(ReentryPhysics.crewGWarningMsg);
            }
		}

        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.DRToolBar] " + msg);
        }
    }
}
