using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using KSP.UI;
using KSP.UI.Screens;
using KSP.Localization;

namespace DeadlyReentry
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class DRToolbar : MonoBehaviour
	{
		#region Fields
        private Rect windowPosition;
        private GUIStyle windowStyle = null;
        private GUIStyle labelStyle = null;
        private GUIStyle windowStyleCenter = null;

        private GUISkin skins = HighLogic.Skin;
		private int id = Guid.NewGuid().GetHashCode();
		//private bool visible = false, showing = true;
		//private Rect window = new Rect(), button = new Rect();
        private Texture2D buttonTexture = new Texture2D(32, 32);
        //private Texture Melificent = (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/Melificent", false);
        //private Texture Ariel = (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/Ariel1", false);
        private Texture Rachel = (Texture)GameDatabase.Instance.GetTexture("DeadlyReentry/Assets/Maat1", false);
        private string DREVersionString = "";
		#endregion
		
		#region Properties
        private static Vector3 mousePos = Vector3.zero;
        private bool weLockedInputs = false;
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

        void Start() 
		{
			// Set up the stock toolbar
            this.windowPosition = new Rect(0,0,360,480);

            windowStyle = new GUIStyle (HighLogic.Skin.window);
            windowStyle.stretchHeight = true;
            windowStyleCenter = new GUIStyle (HighLogic.Skin.window);
            windowStyleCenter.alignment = TextAnchor.MiddleCenter;
            labelStyle = new GUIStyle(HighLogic.Skin.label);
            labelStyle.fixedHeight = labelStyle.lineHeight + 4f;

            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
			GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIAppLauncherDestroyed);
            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);
            GameObject.DontDestroyOnLoad(this);
            //Texture Melificent =
		}
/*		
		void Start() 
		{
            if (!CompatibilityChecker.IsAllCompatible())
            {
                isCompatible = false;
                return;
            }
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
				//RenderingManager.AddToPostDrawQueue (0, Draw);
                OnGUIAppLauncherReady();
			}
		}
*/		
        public void OnGUI()
        {
            GUI.skin = HighLogic.Skin;

            if (HighLogic.LoadedSceneIsEditor) PreventEditorClickthrough();
            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneHasPlanetarium) PreventInFlightClickthrough();


            Draw();
        }

        void Draw()
        {
            if (visible)
            {
                //Set the GUI Skin
                //GUI.skin = HighLogic.Skin;
                this.windowPosition = GUILayout.Window(id, this.windowPosition, OnWindow, "Deadly Reentry " + DREVersionString + " - The Maat Edition", windowStyle);
            }
        }

        bool MouseIsOverWindow()
        {
            mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            return windowPosition.Contains(mousePos);
        }

        void PreventEditorClickthrough()
        {
            bool mouseOverWindow = MouseIsOverWindow();
            if (visible && !weLockedInputs && mouseOverWindow && !Input.GetMouseButton(1))
            {
                EditorLogic.fetch.Lock(true, true, true, "DREMenuLock");
                weLockedInputs = true;
            }
            if (weLockedInputs && (!mouseOverWindow || !visible))
            {
                EditorLogic.fetch.Unlock("DREMenuLock");
                weLockedInputs = false;
            }
        }

        void PreventInFlightClickthrough()
        {
            bool mouseOverWindow = MouseIsOverWindow();
            if (visible && !weLockedInputs && mouseOverWindow && !Input.GetMouseButton(1))
            {
                //InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS | ControlTypes.MAP, "DREMenuLock");
                InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS, "DREMenuLock");
                weLockedInputs = true;
            }
            if (weLockedInputs && (!mouseOverWindow || !visible))
            {
                InputLockManager.RemoveControlLock("DREMenuLock");
                weLockedInputs = false;
            }
        }

		void OnGUIAppLauncherReady()
		{
            ApplicationLauncher.AppScenes visibleInScenes = 
                ApplicationLauncher.AppScenes.FLIGHT | 
                ApplicationLauncher.AppScenes.MAPVIEW | 
                ApplicationLauncher.AppScenes.SPACECENTER | 
                ApplicationLauncher.AppScenes.TRACKSTATION;
            if (ApplicationLauncher.Ready && this.DRToolbarButton == null)
			{
                print("onGUIAppLauncherReady! Let's set up our button!"); 
                this.DRToolbarButton = ApplicationLauncher.Instance.AddModApplication(onAppLaunchToggleOn,
                    onAppLaunchToggleOff,
                    null,
                    null,
                    null,
                    null,
                    visibleInScenes,
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
            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            DeadlyReentryScenario.displayCrewGForceWarning = GUILayout.Toggle(DeadlyReentryScenario.displayCrewGForceWarning, "Warn crew G forces are becoming dangerous!");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            GUILayout.Label(Localizer.Format("#autoLOC_189833", new string[] { (HighLogic.CurrentGame.Parameters.Difficulty.ReentryHeatScale * 100f).ToString("N0") }), labelStyle);
            //string mylabel = Localizer.Format("#autoLOC_189833", new string[] { (100f).ToString("N0")});

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Height(0);
            DeadlyReentryScenario.DREReentryHeatScale = GUILayout.HorizontalSlider(DeadlyReentryScenario.DREReentryHeatScale, 0, ReentryPhysics.maxHeatScale);
            GUILayout.EndHorizontal();

            GUILayout.Width(0);
            GUILayout.Height(0);
            GUILayout.Label("For other thermal settings, press F12 then select Physics->Thermals.", windowStyleCenter);
            GUILayout.Label(Rachel, windowStyleCenter);

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
                HighLogic.CurrentGame.Parameters.Difficulty.ReentryHeatScale = DeadlyReentryScenario.DREReentryHeatScale;
            }
		}

        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.DRToolBar] " + msg);
        }
    }
}
