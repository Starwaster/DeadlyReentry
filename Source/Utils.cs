using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;

namespace RealHeat
{
	[KSPAddon(KSPAddon.Startup.MainMenu, false)] // fixed
	public class FixMaxTemps : MonoBehaviour
	{
		public void Start()
		{
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS")) {
				if(node.HasValue("ridiculousMaxTemp")) {
					float maxTemp;
					float scale = 0.5f;
					if(node.HasValue ("maxTempScale"))
						float.TryParse(node.GetValue("maxTempScale"), out scale);
					if(scale > 0 && float.TryParse(node.GetValue("ridiculousMaxTemp"), out maxTemp)) {
                        if (PartLoader.LoadedPartsList != null)
                        {
                            foreach (AvailablePart part in PartLoader.LoadedPartsList)
                            {
                                try
                                {
                                    if (part.partPrefab != null)
                                    {
                                        float oldTemp = part.partPrefab.maxTemp;
                                        bool changed = false;
                                        if (part.partPrefab.maxTemp > maxTemp)
                                        {
                                            part.partPrefab.maxTemp *= scale;
                                            changed = true;
                                        }
                                        if (part.partPrefab.maxTemp > maxTemp)
                                        {
                                            changed = true;
                                            part.partPrefab.maxTemp = maxTemp;
                                        }
                                        if (changed)
                                        {
                                            float curScale = part.partPrefab.maxTemp / oldTemp;

                                            foreach (ModuleEngines module in part.partPrefab.Modules.OfType<ModuleEngines>())
                                            {
                                                module.heatProduction *= curScale;
                                            }
                                            foreach (ModuleEnginesFX module in part.partPrefab.Modules.OfType<ModuleEnginesFX>())
                                            {
                                                module.heatProduction *= curScale;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
					}
				}
			}
		}
	}

	[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RealHeatUtils : MonoBehaviour
    {
		private static AerodynamicsFX _afx;

        public static AerodynamicsFX afx {
			get {
				if (_afx == null) {
					GameObject fx = GameObject.Find ("FXLogic");
					if (fx != null) {
						_afx = fx.GetComponent<AerodynamicsFX> ();
					}
				}
				return _afx;
			}
		}

		public static Vector3 frameVelocity;
        public static double frameDensity = 1.225;
        public static float ATMTOPA = 101325f;
        public const float CTOK = 273.15f;

        public static float shockwaveMultiplier = 1.0f;
        public static float shockwaveExponent = 1.0f;
        public static float heatMultiplier = 20.0f;
		public static float temperatureExponent = 1.0f;
		public static float densityExponent = 1.0f;

		public static float startThermal = 800.0f;
		public static float fullThermal = 1150.0f;
        public static float afxDensityExponent = 0.75f;

        public static float gToleranceMult = 2.0f;
        public static float parachuteTempMult = 0.5f;

        public static AtmTempCurve baseTempCurve = new AtmTempCurve();

        public static bool debugging = false;
        public static bool multithreadedTempCurve = true;
        protected Rect windowPos = new Rect(100, 100, 0, 0);

		public static float TemperatureDelta(double density, float shockwaveK, float partTempK)
		{
			if (shockwaveK < partTempK || density == 0 || shockwaveK < 0)
				return 0;
			return (float) ( Math.Pow (shockwaveK - partTempK, temperatureExponent) 
						   * Math.Pow (density, densityExponent)
						   * heatMultiplier * TimeWarp.fixedDeltaTime);
		}

		public void Start()
		{
            enabled = true; // 0.24 compatibility
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS")) {
                if(node.HasValue("shockwaveExponent"))
                    float.TryParse(node.GetValue("shockwaveExponent"), out shockwaveExponent);
                if (node.HasValue("shockwaveMultiplier"))
                    float.TryParse(node.GetValue("shockwaveMultiplier"), out shockwaveMultiplier);
                if(node.HasValue("heatMultiplier"))
					float.TryParse (node.GetValue ("heatMultiplier"), out heatMultiplier);
				if(node.HasValue("startThermal"))
					float.TryParse (node.GetValue ("startThermal"), out startThermal);
				if(node.HasValue("fullThermal"))
					float.TryParse (node.GetValue ("fullThermal"), out fullThermal);
                if (node.HasValue("afxDensityExponent"))
                    float.TryParse(node.GetValue("afxDensityExponent"), out afxDensityExponent);
				if(node.HasValue("temperatureExponent"))
					float.TryParse (node.GetValue ("temperatureExponent"), out temperatureExponent);
				if(node.HasValue("densityExponent"))
					float.TryParse (node.GetValue ("densityExponent"), out densityExponent);

                if (node.HasValue("gToleranceMult"))
                    float.TryParse(node.GetValue("gToleranceMult"), out gToleranceMult);

                if (node.HasValue("parachuteTempMult"))
                    float.TryParse(node.GetValue("parachuteTempMult"), out parachuteTempMult);


                if (node.HasValue("crewGClamp"))
                    double.TryParse(node.GetValue("crewGClamp"), out ModuleDamage.crewGClamp);
                if (node.HasValue("crewGPower"))
                    double.TryParse(node.GetValue("crewGPower"), out ModuleDamage.crewGPower);
                if (node.HasValue("crewGMin"))
                    double.TryParse(node.GetValue("crewGMin"), out ModuleDamage.crewGMin);
                if (node.HasValue("crewGWarn"))
                    double.TryParse(node.GetValue("crewGWarn"), out ModuleDamage.crewGWarn);
                if (node.HasValue("crewGLimit"))
                    double.TryParse(node.GetValue("crewGLimit"), out ModuleDamage.crewGLimit);
                if (node.HasValue("crewGKillChance"))
                    double.TryParse(node.GetValue("crewGKillChance"), out ModuleDamage.crewGKillChance);


				if(node.HasValue("debugging"))
					bool.TryParse (node.GetValue ("debugging"), out debugging);
                if (node.HasValue("multithreadedTempCurve"))
                    bool.TryParse(node.GetValue("multithreadedTempCurve"), out multithreadedTempCurve);
                break;
			};

            AtmDataOrganizer.LoadConfigNodes();
            UpdateTempCurve();
            GameEvents.onVesselSOIChanged.Add(UpdateTempCurve);
            GameEvents.onVesselChange.Add(UpdateTempCurve);
		}

        public void UpdateTempCurve(GameEvents.HostedFromToAction<Vessel, CelestialBody> a)
        {
            if(a.host == FlightGlobals.ActiveVessel)
                UpdateTempCurve(a.to);
        }

        public void UpdateTempCurve(Vessel v)
        {
            UpdateTempCurve(v.mainBody);
        }

        public void UpdateTempCurve(CelestialBody body)
        {
            Debug.Log("Updating temperature curve for current body.\n\rCurrent body is: " + body.bodyName);
            baseTempCurve.CalculateNewAtmTempCurve(body, false);
        }

        public void UpdateTempCurve()
        {
            Debug.Log("Updating temperature curve for current body.\n\rCurrent body is: " + FlightGlobals.currentMainBody.bodyName);
            baseTempCurve.CalculateNewAtmTempCurve(FlightGlobals.currentMainBody, false);
        }

        public static double CalculateDensity(CelestialBody body, double pressure, double temp)
        {
            float bodyGasConsant = AtmDataOrganizer.GetGasConstant(body);
            pressure *= ATMTOPA;
            return pressure / (temp * bodyGasConsant);
        }

        public void OnGUI()
        {
            if (debugging)
            {
                windowPos = GUILayout.Window("RealHeat".GetHashCode(), windowPos, DrawWindow, "RealHeat Setup");
            }
        }

		private void FixAeroFX(AerodynamicsFX aeroFX)
		{
			aeroFX.airDensity = (float)(Math.Pow(frameDensity, afxDensityExponent));
			/*aeroFX.state = Mathf.InverseLerp(0.15f, 0.1f, aeroFX.airDensity);
	        aeroFX.heatFlux = 0.5f * aeroFX.airDensity * Mathf.Pow(aeroFX.airspeed, aeroFX.fudge1);
	        aeroFX.FxScalar = Mathf.Min(1f, (aeroFX.heatFlux - aeroFX.) / (5f * aeroFX.));
	        aeroFX.transitionFade = 1f - Mathf.Sin(aeroFX.state * 3.14159274f);
	        aeroFX.FxScalar *= aeroFX.transitionFade;*/

            afx.state = Mathf.InverseLerp(startThermal, fullThermal, afx.airspeed);
		}

		public void FixedUpdate()
		{
			frameVelocity = Krakensbane.GetFrameVelocityV3f() - Krakensbane.GetLastCorrection() * TimeWarp.fixedDeltaTime;
            frameDensity = CalculateDensity(FlightGlobals.currentMainBody, FlightGlobals.ActiveVessel.staticPressure, CTOK + FlightGlobals.ActiveVessel.flightIntegrator.getExternalTemperature());
            
            FixAeroFX (afx);
		}

		public void LateUpdate()
		{
			FixAeroFX (afx);
		}

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.D))
            {
                debugging = !debugging;
            }

            if (FlightGlobals.ready)
            {
                if ((afx != null))
                {
					FixAeroFX(afx);
				
					foreach (Vessel vessel in FlightGlobals.Vessels) 
					{
						if(vessel.loaded)// && afx.FxScalar > 0)
						{
		                    foreach (Part p in vessel.Parts)
		                    {
                                if (!p.Modules.Contains("ModuleRealHeat"))
                                    p.AddModule("ModuleRealHeat"); // thanks a.g.!

							}
                        }
                    }
                }
            }
        }

        public void OnDestroy()
        {
            GameEvents.onVesselSOIChanged.Remove(UpdateTempCurve);
            GameEvents.onVesselChange.Remove(UpdateTempCurve);
        }

        public void DrawWindow(int windowID)
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(5, 5, 3, 0);
            buttonStyle.margin = new RectOffset(1, 1, 1, 1);
            buttonStyle.stretchWidth = false;
            buttonStyle.stretchHeight = false;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.wordWrap = false;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", buttonStyle))
            {
                debugging = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Current density: " + frameDensity, labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Shockwave Multiplier:", labelStyle);
            string newShockwaveMultiplier = GUILayout.TextField(shockwaveMultiplier.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Shockwave Exponent:", labelStyle);
            string newShockwaveExponent = GUILayout.TextField(shockwaveExponent.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Temperature Exponent:", labelStyle);
            string newTemperatureExponent = GUILayout.TextField(temperatureExponent.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Density Exponent:", labelStyle);
			string newDensityExponent = GUILayout.TextField(densityExponent.ToString(), GUILayout.MinWidth(100));
			GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Multiplier:", labelStyle);
            string newMultiplier = GUILayout.TextField(heatMultiplier.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("F/X Transition", labelStyle);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Begin at:", labelStyle);
			string newThermal = GUILayout.TextField(startThermal.ToString(), GUILayout.MinWidth(100));
			GUILayout.Label("m/s", labelStyle);
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Full at:", labelStyle);
			string newThermalFull = GUILayout.TextField(fullThermal.ToString(), GUILayout.MinWidth(100));
			GUILayout.Label("m/s", labelStyle);
			GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
			GUILayout.Label("FX Density exp.:", labelStyle);
            string newAFXDensityExponent = GUILayout.TextField(afxDensityExponent.ToString(), GUILayout.MinWidth(100));
			GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("G Tolerance Mult:", labelStyle);
            string newGToleranceMult = GUILayout.TextField(gToleranceMult.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Parachute Temp Mult:", labelStyle);
            string newParachuteTempMult = GUILayout.TextField(parachuteTempMult.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Max", labelStyle);
            string newcrewGClamp = GUILayout.TextField(ModuleDamage.crewGClamp.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Exponent", labelStyle);
            string newcrewGPower = GUILayout.TextField(ModuleDamage.crewGPower.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Min", labelStyle);
            string newcrewGMin = GUILayout.TextField(ModuleDamage.crewGMin.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Warn Level", labelStyle);
            string newcrewGWarn = GUILayout.TextField(ModuleDamage.crewGWarn.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Kill threshold", labelStyle);
            string newcrewGLimit = GUILayout.TextField(ModuleDamage.crewGLimit.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Kill chance per update", labelStyle);
            string newcrewGKillChance = GUILayout.TextField(ModuleDamage.crewGKillChance.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild and Dump Temp Curve"))
            {
                baseTempCurve.CalculateNewAtmTempCurve(FlightGlobals.currentMainBody, true);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            multithreadedTempCurve = GUILayout.Toggle(multithreadedTempCurve, "Multithread Temp Curve Calculation", GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button ("Save")) {
				ConfigNode node = new ConfigNode("@REENTRY_EFFECTS[Default]:Final");
				ConfigNode savenode = new ConfigNode();
                node.AddValue ("@shockwaveExponent", shockwaveExponent.ToString());
                node.AddValue ("@shockwaveMultiplier", shockwaveMultiplier.ToString());
				node.AddValue ("@heatMultiplier", heatMultiplier.ToString ());
				node.AddValue ("@startThermal", startThermal.ToString ());
				node.AddValue ("@fullThermal", fullThermal.ToString ());
                node.AddValue("@afxDensityExponent", afxDensityExponent.ToString());
				node.AddValue ("@temperatureExponent", temperatureExponent.ToString ());
				node.AddValue ("@densityExponent", densityExponent.ToString ());
                node.AddValue("@gToleranceMult", gToleranceMult.ToString());
                node.AddValue("@parachuteTempMult", gToleranceMult.ToString());

                node.AddValue("@multithreadedTempCurve", multithreadedTempCurve.ToString());

                node.AddValue("@crewGClamp", ModuleDamage.crewGClamp.ToString());
                node.AddValue("@crewGPower", ModuleDamage.crewGPower.ToString());
                node.AddValue("@crewGMin", ModuleDamage.crewGMin.ToString());
                node.AddValue("@crewGWarn", ModuleDamage.crewGWarn.ToString());
                node.AddValue("@crewGLimit", ModuleDamage.crewGLimit.ToString());
                node.AddValue("@crewGKillChance", ModuleDamage.crewGKillChance.ToString());
				
                savenode.AddNode (node);
				savenode.Save (KSPUtil.ApplicationRootPath.Replace ("\\", "/") + "GameData/RealHeat/custom.cfg");
			}
			GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (GUI.changed)
            {
                float newValue;
                if (float.TryParse(newShockwaveMultiplier, out newValue))
                {
                    shockwaveMultiplier = newValue;
                }
                if (float.TryParse(newShockwaveExponent, out newValue))
                {
                    shockwaveExponent = newValue;
                }
                if (float.TryParse(newTemperatureExponent, out newValue))
                {
                    temperatureExponent = newValue;
                }

				if (float.TryParse(newDensityExponent, out newValue))
				{
					densityExponent = newValue;
				}

                if (float.TryParse(newMultiplier, out newValue))
                {
                    heatMultiplier = newValue;
                }
				if (float.TryParse(newThermal, out newValue))
				{
					startThermal = newValue;
				}
				if (float.TryParse(newThermalFull, out newValue))
				{
					fullThermal = newValue;
				}
                if (float.TryParse(newAFXDensityExponent, out newValue))
				{
                    afxDensityExponent = newValue;
				}
                
                if (float.TryParse(newGToleranceMult, out newValue))
				{
                    gToleranceMult = newValue;
				}
                if (float.TryParse(newParachuteTempMult, out newValue))
                {
                    parachuteTempMult = newValue;
                }

                if (float.TryParse(newcrewGClamp, out newValue))
                {
                    ModuleDamage.crewGClamp = newValue;
                }

				if (float.TryParse(newcrewGPower, out newValue))
				{
                    ModuleDamage.crewGPower = newValue;
				}

                if (float.TryParse(newcrewGMin, out newValue))
                {
                    ModuleDamage.crewGMin = newValue;
                }
				if (float.TryParse(newcrewGWarn, out newValue))
				{
                    ModuleDamage.crewGWarn = newValue;
				}
				if (float.TryParse(newcrewGLimit, out newValue))
				{
                    ModuleDamage.crewGLimit = newValue;
				}
                if (float.TryParse(newcrewGKillChance, out newValue))
				{
                    ModuleDamage.crewGKillChance = newValue;
				}
			}
        }
    }
}
