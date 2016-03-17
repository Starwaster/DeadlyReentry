using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
<<<<<<< HEAD
using KSP.UI.Screens;
=======
using System.IO;
using KSP.IO;
using Debug = UnityEngine.Debug;
using File = KSP.IO.File;
>>>>>>> refs/remotes/origin/master

namespace DeadlyReentry
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class DRToolbar : MonoBehaviour
	{
		private static Rect windowPosition = new Rect(0,0,360,480);
		private static GUIStyle windowStyle = null;
        private static GUIStyle windowStyleCenter = null;

		#region Fields
        private static ApplicationLauncherButton DRToolbarButton = new ApplicationLauncherButton();
        private static bool addButton = true;
        private static bool visible = false;

        private GUISkin skins = HighLogic.Skin;
		private int id = Guid.NewGuid().GetHashCode();
		//private bool visible = false, showing = true;
		//private Rect window = new Rect(), button = new Rect();
		private Texture2D buttonTexture = new Texture2D(1, 1);
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
		
		
		
		DRToolbar ()
		{
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            DREVersionString = string.Format("{0}.{1}.{2}", fileVersionInfo.FileMajorPart, fileVersionInfo.FileMinorPart, fileVersionInfo.FileBuildPart);
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
			if (ApplicationLauncher.Ready && addButton)
			{
				DRToolbarButton = ApplicationLauncher.Instance.AddModApplication(onAppLaunchToggleOn,
                                                                                      onAppLaunchToggleOff,
                                                                                      null,
                                                                                      null,
                                                                                      null,
                                                                                      null,
                                                                                      ApplicationLauncher.AppScenes.ALWAYS,
                                                                                      (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/DR_icon_off", false));
                addButton = false;
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
            GUILayout.Label("G Tolerance Mult:", windowStyle);
            string newGToleranceMult = GUILayout.TextField(ReentryPhysics.gToleranceMult.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Max", windowStyle);
            string newcrewGClamp = GUILayout.TextField(ModuleAeroReentry.crewGClamp.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Exponent", windowStyle);
            string newcrewGPower = GUILayout.TextField(ModuleAeroReentry.crewGPower.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Min", windowStyle);
            string newcrewGMin = GUILayout.TextField(ModuleAeroReentry.crewGMin.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Warn Level", windowStyle);
            string newcrewGWarn = GUILayout.TextField(ModuleAeroReentry.crewGWarn.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Kill threshold", windowStyle);
            string newcrewGLimit = GUILayout.TextField(ModuleAeroReentry.crewGLimit.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Kill chance per update", windowStyle);
            string newcrewGKillChance = GUILayout.TextField(ModuleAeroReentry.crewGKillChance.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.Label("For other thermal settings, press F-12 then select Physics->Thermals.", windowStyleCenter);
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
<<<<<<< HEAD

=======
>>>>>>> refs/remotes/origin/master
		}

        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.DRToolBar] " + msg);
        }
    }
}
