using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;

namespace DeadlyReentry
{
	public class ModuleAeroReentry: PartModule
	{
        public const float CTOK = 273.15f;

        protected bool isCompatible = true;

        EventData<GameEvents.ExplosionReaction> ReentryReaction = GameEvents.onPartExplode;

		UIPartActionWindow _myWindow = null; 
		UIPartActionWindow myWindow 
        {
			get {
				if(_myWindow == null) {
					foreach(UIPartActionWindow window in FindObjectsOfType (typeof(UIPartActionWindow))) {
						if(window.part == part) _myWindow = window;
					}
				}
				return _myWindow;
			}
		}

		static string FormatTime(double time)
		{
			int iTime = (int) time % 3600;
			int seconds = iTime % 60;
			int minutes = (iTime / 60) % 60;
			int hours = (iTime / 3600);
			return hours.ToString ("D2") 
				   + ":" + minutes.ToString ("D2") + ":" + seconds.ToString ("D2");
		}

		public static void PlaySound(FXGroup fx, float volume)
		{
			if(fx.audio.isPlaying) {
				if(fx.audio.volume < volume)
					fx.audio.volume = volume;
			} else {
				fx.audio.volume = volume;
				fx.audio.Play ();
			}
			if(ReentryPhysics.debugging)
				print (fx.audio.clip.name);

		}
		FXGroup _gForceFX = null;
		FXGroup gForceFX 
        {
			get {
				if(_gForceFX == null) {
					_gForceFX = new FXGroup (part.partName + "_Crushing");
					_gForceFX.audio = gameObject.AddComponent<AudioSource>();
					_gForceFX.audio.clip = GameDatabase.Instance.GetAudioClip("DeadlyReentry/Sounds/gforce_damage");
					_gForceFX.audio.volume = GameSettings.SHIP_VOLUME;
					_gForceFX.audio.Stop ();
				}
				return _gForceFX;

			}
		}
		FXGroup _ablationSmokeFX = null;
		FXGroup ablationSmokeFX 
        {
			get {
				if(_ablationSmokeFX == null) {
					_ablationSmokeFX = new FXGroup (part.partName + "_Smoking");
					_ablationSmokeFX.fxEmitters.Add (Emitter("fx_smokeTrail_medium").GetComponent<ParticleEmitter>());
				}
				return _ablationSmokeFX;
			}
		}

		FXGroup _ablationFX = null;
		FXGroup ablationFX 
        {
			get {
				if(_ablationFX == null) {
					_ablationFX = new FXGroup (part.partName + "_Burning");
					_ablationFX.fxEmitters.Add (Emitter("fx_exhaustFlame_yellow").GetComponent<ParticleEmitter>());
                    _ablationFX.fxEmitters.Add(Emitter("fx_exhaustSparks_yellow").GetComponent<ParticleEmitter>());
					_ablationFX.audio = gameObject.AddComponent<AudioSource>();
					_ablationFX.audio.clip = GameDatabase.Instance.GetAudioClip("DeadlyReentry/Sounds/fire_damage");
                    _ablationFX.audio.volume = GameSettings.SHIP_VOLUME;
					_ablationFX.audio.Stop ();

				}
				return _ablationFX;
			}
		}

		[KSPField(isPersistant = false, guiActive = false, guiName = "Shockwave", guiUnits = "",   guiFormat = "G")]
		public string displayShockwave;

		[KSPField(isPersistant = false, guiActive = false, guiName = "Density", guiUnits = "",   guiFormat = "G")]
		public string displayAtmDensity;
		
		[KSPField(isPersistant = false, guiActive = true, guiName = "Temperature", guiUnits = "C",   guiFormat = "F0")]
		public float displayTemperature;

		[KSPField(isPersistant = false, guiActive = false, guiName = "Acceleration", guiUnits = "G",   guiFormat = "F3")]
		public float displayGForce;

		[KSPField(isPersistant = false, guiActive = false, guiName = "Damage", guiUnits = "",   guiFormat = "G")]
		public string displayDamage;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Cumulative G", guiUnits = "", guiFormat = "F0")]
        public double gExperienced = 0;

		[KSPField(isPersistant = true)]
		public float adjustCollider = 0;

		[KSPField(isPersistant = true)]
		public float crashTolerance = 8;

		[KSPField(isPersistant = true)]
		public float damage = 0;

		[KSPField(isPersistant = true)]
		public bool dead = false;

        [KSPField]
        public float gTolerance = -1;

        // counters etc
		private double lastGForce = 0;
        public static double crewGClamp = 30;
        public static double crewGPower = 4;
        public static double crewGMin = 5;
        public static double crewGWarn = 300000;
        public static double crewGLimit = 600000;
        public static float crewGKillChance = 0.75f;

        protected float deltaTime = 0f;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Ambient", guiUnits = "C", guiFormat = "F2")]
        protected float ambient = 0f; // ambient temperature (C)
        protected float density = 1.225f; // ambient density (kg/m^3)
        protected float shockwave; // shockwave temperature (C)
        protected Vector3 velocity; // velocity vector in local reference space (m/s)
        protected float speed; // velocity magnitude (m/s)
        private double counter = 0;

        private bool is_debugging = false;
        private bool is_on_fire = false;
        private bool is_gforce_fx_playing = false;

        private bool is_engine = false;
        private bool is_eva = false;
        private ModuleParachute parachute = null;
        private PartModule realChute = null;
        private Type rCType = null;
        private bool hasParachute = false;

        protected PartModule FARPartModule = null;
        protected FieldInfo FARField = null;
        protected bool FARSearched = false;

		[KSPEvent (guiName = "No Damage", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = false, unfocusedRange = 4f)]
		public void RepairDamage()
		{
			if (damage > 0) {
				damage = damage - UnityEngine.Random.Range (0.0f, 0.1f);
				if(damage < 0)
					damage = 0;
			}
			SetDamageLabel ();
			if (myWindow != null)
				myWindow.displayDirty = true;
		}

		public void SetDamageLabel() 
		{
            if (Events == null)
                return;
			if (damage > 0.5)
				Events["RepairDamage"].guiName = "Repair Critical Damage";
			else if (damage > 0.25)
				Events["RepairDamage"].guiName = "Repair Heavy Damage";
			else if (damage > 0.125)
				Events["RepairDamage"].guiName = "Repair Moderate Damage";
			else if (damage > 0)
				Events["RepairDamage"].guiName = "Repair Light Damage";
			else
				Events["RepairDamage"].guiName = "No Damage";
		}

        public override void OnAwake()
        {
            base.OnAwake();
            if (!CompatibilityChecker.IsAllCompatible())
            {
                isCompatible = false;
                return;
            }
            if (part && part.Modules != null) // thanks, FlowerChild!
            {
                is_engine = (part.Modules.Contains("ModuleEngines") || part.Modules.Contains("ModuleEnginesFX"));
                is_eva = part.Modules.Contains("KerbalEVA");
                if (part.Modules.Contains("ModuleParachute"))
                    parachute = (ModuleParachute)part.Modules["ModuleParachute"];
                if (part.Modules.Contains("RealChuteModule"))
                {
                    realChute = part.Modules["RealChuteModule"];
                    rCType = realChute.GetType();
                }
                hasParachute = (parachute != null) || (realChute != null);
            }
        }

		private bool GetShieldedStateFromFAR()
        {
            // Check if this part is shielded by fairings/cargobays according to FAR's information...
			if ((object)FARPartModule != null)
			{
				//Debug.Log("[DREC] Part has FAR module.");
				try
				{
					bool isShieldedFromFAR = ((bool)(FARField.GetValue(FARPartModule)));
					//Debug.Log("[DREC] Found FAR isShielded: " + isShieldedFromFAR.ToString());
					return isShieldedFromFAR;
				}
				catch (Exception e)
				{
					Debug.Log("[DREC]: " + e.Message);
					return false;
				}
			}
			else
			{
				//Debug.Log("[DREC] No FAR module.");
				return false;
			}
		}

		public virtual void Start()
		{
            if (!isCompatible)
                return;
            counter = 0;
			if (!HighLogic.LoadedSceneIsFlight)
				return;
			SetDamageLabel ();
			if ((object)myWindow != null)
				myWindow.displayDirty = true;
			// moved part detection logic to OnAWake


                // exception: FAR
            try
            {
                if (part.Modules.Contains("FARBasicDragModel"))
                {
                    FARPartModule = part.Modules["FARBasicDragModel"];
                }
                else if (part.Modules.Contains("FARWingAerodynamicModel"))
                {
                    FARPartModule = part.Modules["FARWingAerodynamicModel"];
                }
                if ((object)FARPartModule != null)
                    FARField = FARPartModule.GetType().GetField("isShielded");
            }
            catch (Exception e)
            {
                Debug.Log("[DRE] Error in Start() initializing FAR support");
                Debug.Log(e.Message);
            }
        }

		public virtual float AdjustedHeat(float temp)
		{
			return temp;
		}

		public bool IsShielded(Vector3 direction)
		{   
            if (part.ShieldedFromAirstream || GetShieldedStateFromFAR())
            	return true;
            
            Ray ray = new Ray(part.transform.position + direction.normalized * (adjustCollider), direction.normalized);
			RaycastHit[] hits = Physics.RaycastAll (ray, 10);
			foreach (RaycastHit hit in hits) 
			{
				if(hit.rigidbody != null && hit.collider != part.collider && hit.collider.attachedRigidbody != part.Rigidbody) 
				{
					return true;
				}
			}
			return false;
		}

        public float AdjustedDensity()
        {
            if (ReentryPhysics.useAlternateDensity)
                return (float)Math.Pow(density, ReentryPhysics.densityExponent) / 10f;
            else
                return (float)Math.Pow(density, ReentryPhysics.densityExponent);
        }

        public float ReentryHeat()
        {
            if ((object)vessel == null || (object)vessel.flightIntegrator == null)
                return 0;
            shockwave = speed - CTOK;
            if (shockwave > 0)
            {
                shockwave = Mathf.Pow(shockwave, ReentryPhysics.shockwaveExponent);
                shockwave *= ReentryPhysics.shockwaveMultiplier;
            }
            
            if (is_debugging)
                displayAtmDensity = density.ToString ("F4") + " kg/m3";

            ambient = vessel.flightIntegrator.getExternalTemperature();
            if (ambient < -CTOK)
                ambient = -CTOK;
            if (shockwave < ambient)
            {
                shockwave = ambient;
                if (is_debugging)
                    displayShockwave = shockwave.ToString("F0") + "C (Ambient)";
            }
            else if (density == 0)
            {
                shockwave = 0;
                displayShockwave = "None (vacuum)";
            }
            else
            {
                // deal with parachutes here
                if (hasParachute)
                {
                    //bool cut = ambient + Math.Pow(density, ReentryPhysics.densityExponent) * shockwave * 10f
                    //    > part.maxTemp * ReentryPhysics.parachuteTempMult;
                    // ambient term as it doesn't contribute meaningfully
                    bool cut = Math.Pow(density, ReentryPhysics.parachuteDifficulty) * shockwave * 10f
                        > part.maxTemp * ReentryPhysics.parachuteTempMult;
					if (cut)
					{
                        if (DeadlyReentryScenario.Instance.displayParachuteWarning && (object)ReentryPhysics.chuteWarningMsg != null)
                            ScreenMessages.PostScreenMessage(ReentryPhysics.chuteWarningMsg, false);

	                    if ((object)parachute != null)
	                    {
	                        ModuleParachute p = parachute;
	                        if (p.deploymentState == ModuleParachute.deploymentStates.DEPLOYED || p.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED)
							{
	                            p.CutParachute();
                                FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " chute failure! (too fast or too hot)");
                            }

						}
						if ((object)realChute != null)
	                    {
	                        if ((bool)rCType.GetProperty("anyDeployed").GetValue(realChute, null))
							{
	                            rCType.GetMethod("GUICut").Invoke(realChute, null);
								FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " chute failure! (too fast or too hot)");
							}
	                    }
					}
                }
                if (IsShielded(velocity))
                {
                    displayShockwave = "Shielded";
                    if (part.ShieldedFromAirstream)
                        displayShockwave += " (stock)";
                }
                else
                {
                    if (is_debugging)
					{
                        displayShockwave = shockwave.ToString("F0") + "C";
					}
                    if (ReentryPhysics.useAlternateHeatModel)
                        return AdjustedHeat(ReentryPhysics.AltTemperatureDelta(AdjustedDensity(), shockwave + CTOK, part.temperature + CTOK));
                    else
                        return AdjustedHeat(ReentryPhysics.TemperatureDelta(AdjustedDensity(), shockwave + CTOK, part.temperature + CTOK));

                }
            }
            return 0;
        }

		public void FixedUpdate()
		{
            if (!HighLogic.LoadedSceneIsFlight || !isCompatible)
                return;
			//Rigidbody rb = part.Rigidbody;
            deltaTime = TimeWarp.fixedDeltaTime;

            density = (float)ReentryPhysics.frameDensity;
			/*if (!rb || part.physicalSignificance == Part.PhysicalSignificance.NONE)
				return;*/

			if (is_debugging != ReentryPhysics.debugging)
			{
				is_debugging = ReentryPhysics.debugging;
				Fields["displayShockwave"].guiActive = ReentryPhysics.debugging;
				Fields["displayGForce"].guiActive = ReentryPhysics.debugging;
	            Fields["gExperienced"].guiActive = ReentryPhysics.debugging;
				Fields["displayAtmDensity"].guiActive = ReentryPhysics.debugging;
			}

            // fix for parts that animate/etc: all parts will use vessel velocity.
            velocity = part.vessel.orbit.GetVel() - part.vessel.mainBody.getRFrmVel(part.vessel.vesselTransform.position);
            speed = velocity.magnitude;

            if (counter < 5.0)
            {
                counter += deltaTime;
                lastGForce = 0;
                return;
            }
            if (!FARSearched)
            {
                if (part.Modules.Contains("FARBasicDragModel"))
                {
                    FARPartModule = part.Modules["FARBasicDragModel"];
                    Debug.Log("*DRE* Found FAR basic drag model for part " + part.name);
                }
                else if (part.Modules.Contains("FARWingAerodynamicModel"))
                {
                    FARPartModule = part.Modules["FARWingAerodynamicModel"];
                    Debug.Log("*DRE* Found FAR wing model for part " + part.name);
                }
                else if (part.Modules.Contains("FARControllableSurface"))
                {
                    FARPartModule = part.Modules["FARControllableSurface"];
                    Debug.Log("*DRE* Found FAR control surface model for part " + part.name);
                }
                else
                {
                    Debug.Log("*DRE* No FAR module found for part " + part.name);
                }
                if ((object)FARPartModule != null)
                    FARField = FARPartModule.GetType().GetField("isShielded");

                FARSearched = true;
            }

			part.temperature += ReentryHeat();
            if (part.temperature < -CTOK || float.IsNaN (part.temperature)) // clamp to Absolute Zero
                part.temperature = -CTOK;
			displayTemperature = part.temperature;
            if (!(part.Modules.Contains("ModuleEngines") || part.Modules.Contains("ModuleEnginesFX")) && this.part.vessel == FlightGlobals.ActiveVessel && part.temperature > part.maxTemp * 0.85f)
            {
                float severity = part.temperature / (part.maxTemp * 0.85f);
                ReentryReaction.Fire(new GameEvents.ExplosionReaction(0f, severity));
            }
			CheckForFire();
			CheckGeeForces();
		}

        public void LateUpdate()
        {
            if (!isCompatible)
                return;
            if (is_on_fire)
            {
                foreach (ParticleEmitter fx in ablationFX.fxEmitters)
                {
                    fx.gameObject.SetActive(true);
                    fx.gameObject.transform.LookAt(part.transform.position + velocity);
                    fx.gameObject.transform.Rotate(90, 0, 0);
                }
                foreach (ParticleEmitter fx in ablationSmokeFX.fxEmitters)
                {
                    fx.gameObject.SetActive(density > 0.02);
                    fx.gameObject.transform.LookAt(part.transform.position + velocity);
                    fx.gameObject.transform.Rotate(90, 0, 0);
                }
            }
        }

		public void AddDamage(float dmg)
		{
			if (dead || part == null || part.partInfo == null || part.partInfo.partPrefab == null)
				return;
			if(ReentryPhysics.debugging)
				print (part.partInfo.title + ": +" + dmg + " damage");
			damage += dmg;
			part.maxTemp = part.partInfo.partPrefab.maxTemp * (1 - 0.15f * damage);
			part.breakingForce = part.partInfo.partPrefab.breakingForce * (1 - damage);
			part.breakingTorque = part.partInfo.partPrefab.breakingTorque * (1 - damage);
			part.crashTolerance = part.partInfo.partPrefab.crashTolerance * (1 - 0.5f * damage);
			SetDamageLabel ();
		}
		public void CheckGeeForces()
		{
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)
            {
                if (dead || (object)vessel == null || deltaTime > 0.5 || deltaTime <= 0)
                    return; // don't check G-forces in warp

                double geeForce = vessel.geeForce_immediate;
                if (geeForce > 40 && geeForce > lastGForce)
                {
                    // G forces over 40 are probably a Kraken twitch unless they last multiple frames
                    displayGForce = displayGForce * (1 - deltaTime) + (float)(lastGForce * deltaTime);
                }
                else
                {
                    //keep a running average of G force over 1s, to further prevent absurd spikes (mostly decouplers & parachutes)
                    displayGForce = displayGForce * (1 - deltaTime) + (float)(geeForce * deltaTime);
                }
                if (displayGForce < crewGMin)
                    gExperienced = 0;

                //double gTolerance;
                if (gTolerance < 0)
                {
                    if (is_engine && damage < 1)
                        gTolerance = (float)Math.Pow(UnityEngine.Random.Range(11.9f, 12.1f) * part.crashTolerance, 0.5);
                    else
                        gTolerance = (float)Math.Pow(UnityEngine.Random.Range(5.9f, 6.1f) * part.crashTolerance, 0.5);

                    gTolerance *= ReentryPhysics.gToleranceMult;
                }
                if (gTolerance >= 0 && displayGForce > gTolerance)
                { // G tolerance is based roughly on crashTolerance
                    AddDamage(deltaTime * (float)(displayGForce / gTolerance - 1));
                    if (!is_eva)
                    { // kerbal bones shouldn't sound like metal when they break.
                        gForceFX.audio.pitch = (float)(displayGForce / gTolerance);
                        PlaySound(gForceFX, damage * 0.3f + 0.7f);
                        is_gforce_fx_playing = true;
                    }
                }
                else if (is_gforce_fx_playing)
                {
                    float new_volume = (gForceFX.audio.volume *= 0.8f);
                    if (new_volume < 0.001f)
                    {
                        gForceFX.audio.Stop();
                        is_gforce_fx_playing = false;
                    }
                }
                if (damage >= 1.0f && !dead)
                {
                    dead = true;
                    FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] "
                                                + part.partInfo.title + " exceeded g-force tolerance.");

                    if ( part is StrutConnector )
                    {
                        ((StrutConnector)part).BreakJoint();
                    }

                    part.explode();
                    return;
                }
                if (Math.Max(displayGForce, geeForce) >= crewGMin)
                {
                    gExperienced += Math.Pow(Math.Min(Math.Abs(Math.Max(displayGForce, geeForce)), crewGClamp), crewGPower) * deltaTime;
                    List<ProtoCrewMember> crew = part.protoModuleCrew; //vessel.GetVesselCrew();
                    if (gExperienced > crewGWarn && crew.Count > 0)
                    {
                        if (DeadlyReentryScenario.Instance.displayCrewGForceWarning)
                            ScreenMessages.PostScreenMessage(ReentryPhysics.crewGWarningMsg, false);
                        if (gExperienced > crewGLimit)
                        {
                            // borrowed from TAC Life Support
                            if (UnityEngine.Random.Range(0f, 1f) < crewGKillChance)
                            {
                                int crewMemberIndex = UnityEngine.Random.Range(0, crew.Count - 1);
                                if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                                {
                                    CameraManager.Instance.SetCameraFlight();
                                }
								ProtoCrewMember member = crew[crewMemberIndex];

                                ScreenMessages.PostScreenMessage(vessel.vesselName + ": Crewmember " + member.name + " died of G-force damage!", 30.0f, ScreenMessageStyle.UPPER_CENTER);
                                FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] "
                                                + member.name + " died of G-force damage.");
                                Debug.Log("*DRE* [" + Time.time + "]: " + vessel.vesselName + " - " + member.name + " died of G-force damage.");

                                if (!vessel.isEVA)
                                {
                                    part.RemoveCrewmember(member);
                                    member.Die();
                                }
                            }
                        }
                    }
                }
                lastGForce = vessel.geeForce_immediate;
            }
		}

		public void CheckForFire()
		{
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)
            {
                if (FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH || FlightGlobals.ActiveVessel.missionTime > 2.0)
                {
                    if (dead)
                        return;
                    float damageThreshold;

                    if (is_engine && damage < 1)
                        damageThreshold = part.maxTemp * 0.975f;
                    else if (is_eva)
                    {
                        damageThreshold = 800 * (1 - damage) * (1 - damage) - CTOK;
                        part.maxTemp = 900;
                    }
                    else
                        damageThreshold = part.maxTemp * 0.85f;
                    if (part.temperature > damageThreshold)
                    {
                        // Handle client-side fire stuff.
                        // OH GOD IT'S ON FIRE.
                        float tempRatio = (part.temperature / damageThreshold) - 1f;
                        tempRatio *= (part.temperature / part.maxTemp) * (part.temperature / part.maxTemp) * 4f;
                        AddDamage(deltaTime * (damage + 1.0f) * tempRatio);
                        float soundTempRatio = part.temperature / part.maxTemp;
                        PlaySound(ablationFX, soundTempRatio * soundTempRatio);

                        if (is_engine && damage < 1)
                            part.temperature = UnityEngine.Random.Range(0.97f + 0.05f * damage, 0.98f + 0.05f * damage) * part.maxTemp;
                        else if (damage < 1)// non-engines can keep burning
                            part.temperature += UnityEngine.Random.Range(0.5f + 0.5f * damage, 1.0f + 0.5f * damage) * (tempRatio * 0.04f * part.maxTemp * deltaTime);

                        if (part.temperature > part.maxTemp || damage >= 1.0f)
                        { // has it burnt up completely?
                            foreach (ParticleEmitter fx in ablationFX.fxEmitters)
                                GameObject.DestroyImmediate(fx.gameObject);
                            foreach (ParticleEmitter fx in ablationSmokeFX.fxEmitters)
                                GameObject.DestroyImmediate(fx.gameObject);
                            if (!dead)
                            {
                                dead = true;
                                FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] "
                                                           + part.partInfo.title + " burned up from overheating.");

                                if ( part is StrutConnector )
                                {
                                    ((StrutConnector)part).BreakJoint();
                                }

                                part.explode();
                                return;
                            }
                        }
                        else
                        {
                            is_on_fire = true;
                            float severity = (this.part.maxTemp * 0.85f) / this.part.maxTemp;
                            float distance = Vector3.Distance(this.part.partTransform.position, FlightGlobals.ActiveVessel.vesselTransform.position);
                            ReentryReaction.Fire(new GameEvents.ExplosionReaction(distance, severity));
                        }
                    }
                    else if (is_on_fire)
                    { // not on fire.
                        is_on_fire = false;
                        foreach (ParticleEmitter fx in ablationFX.fxEmitters)
                            fx.gameObject.SetActive(false);
                        foreach (ParticleEmitter fx in ablationSmokeFX.fxEmitters)
                            fx.gameObject.SetActive(false);
                    }
                }
            }
		}
		public GameObject Emitter(string fxName)
        {
			GameObject fx = (GameObject)UnityEngine.Object.Instantiate (UnityEngine.Resources.Load ("Effects/" + fxName));

			fx.transform.parent = part.transform;
			fx.transform.localPosition = new Vector3 (0, 0, 0);
			fx.transform.localRotation = Quaternion.identity;
			fx.SetActive (false);
			return fx;

		}

	}

	public class ModuleHeatShield: ModuleAeroReentry
	{
		/* Sample configNode:

	MODULE
	{
		name = ModuleHeatShield
		direction = 0, -1, 0 // bottom of pod
		reflective = 0.05 // 5% of heat is ignored at correct angle
		ablative = AblativeShielding
		loss
		{ // loss is based on the shockwave temperature
			key = 450 0 // start ablating at 450 degrees C
			key = 550 2 // peak ablation at 550 degrees C
			key = 3000 2.5 // max ablation at 3000 degrees C
		}
		dissipation
		{ // dissipation is based on the part's current temperature
				key = 300 0 // begin ablating at 300 degrees C
				key = 500 330 // maximum dissipation at 500 degrees C
		}
	}
	
	RESOURCE
	{
		name = AblativeShielding
		amount = 250
		maxAmount = 250
	}
	
	*/

		[KSPField(isPersistant = false, guiActive = false, guiName = "angle", guiUnits = " ", guiFormat = "F3")]
		public float dot;

		[KSPField(isPersistant = false)]
		public int deployAnimationController;

		[KSPField(isPersistant = false)]
		public Vector3 direction;

		[KSPField(isPersistant = false)]
		public float reflective;

		[KSPField(isPersistant = false)]
		public string ablative;

		[KSPField(isPersistant = false)]
		public float area;

		[KSPField(isPersistant = false)]
		public float thickness;

		[KSPField(isPersistant = false)]
		public FloatCurve loss = new FloatCurve();

		[KSPField(isPersistant = false)]
		public FloatCurve dissipation = new FloatCurve();

        [KSPField(isPersistant = false)]
        public float conductivity = 0.12f;

        [KSPField]
        public string techRequired = "";

        protected bool canShield = true;

		public override void Start()
		{
            base.Start();
			if (ablative == null)
				ablative = "None";

            part.heatConductivity = conductivity;
            if (ReentryPhysics.dissipationCap)
                // key = 1350 3600.0 14.06186 0 - Maybe increase value higher...
                dissipation.Add(this.part.maxTemp * 0.85f, this.part.maxTemp * 2.0f, 14.06186f, 0f);

            if (HighLogic.CurrentGame.Mode != Game.Modes.SANDBOX && !techRequired.Equals("") && ResearchAndDevelopment.GetTechnologyState(techRequired) != RDTech.State.Available)
                canShield = false;
		}
		public override string GetInfo()
		{
			string s = "Active Heat Shield";
			if (direction.x != 0 || direction.y != 0 || direction.z != 0)
				s += " (directional)";
            if (techRequired != "")
                s += " NOTE: Requires technology " + techRequired;
			return s;
		}

		public override float AdjustedHeat(float temp)
		{
			if (direction.magnitude == 0) // an empty vector means the shielding exists on all sides
				dot = 1; 
			else // check the angle between the shock front and the shield
				dot = Vector3.Dot (velocity.normalized, part.transform.TransformDirection(direction).normalized);
			
			if (canShield && dot > 0 && temp > 0) {
				//radiate away some heat
				float rad = temp * dot * reflective;
				temp -= rad  * (1 - damage) * (1 - damage);
				//if(loss.Evaluate(shockwave) > 0
                if(loss.Evaluate(part.temperature) > 0
                   && part.Resources.Contains (ablative)) {
					// ablate away some shielding
                    float ablation = (float) (loss.Evaluate(part.temperature)
                                              //loss.Evaluate((float) Math.Pow (shockwave, ReentryPhysics.temperatureExponent)) 
                                              //* dot
                                              //* AdjustedDensity()
					                          * deltaTime
                                              * ReentryPhysics.ablationMetric); // This last is to scale down ablation rate to survive up to x minutes  of reentry (ablationMetric = 1 / x * 60)

                    float disAmount = dissipation.Evaluate(part.temperature) * ablation * (1 - damage) * (1 - damage);
                    if (disAmount > 0)
                    {
                        if (part.Resources[ablative].amount < ablation)
                            ablation = (float)part.Resources[ablative].amount;
                        // wick away some heat with the shielding
                        part.Resources[ablative].amount -= ablation;
                        //dissipation.evaluate(dissipation.maxtime)
                        //temp -= dissipation.Evaluate(dissipation.maxTime) * ablation * (1 - damage) * (1 - damage);
                        temp -= dissipation.Evaluate(part.temperature) * ablation * (1 - damage) * (1 - damage);
                    }
				}
			}
			return temp;
		}
	}

	[KSPAddon(KSPAddon.Startup.MainMenu, false)] // fixed
	public class FixMaxTemps : MonoBehaviour
	{
        //protected PartModule RFEngineConfig = null;
        //protected FieldInfo[] RFEConfigs = null;
        
        public void Start()
		{
            if (!CompatibilityChecker.IsAllCompatible())
                return;
            Debug.Log("FixMaxTemps: Fixing Temps");
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS"))
            {
                if(node.HasValue("name") && node.GetValue("name") == "Default" && node.HasValue("ridiculousMaxTemp"))
                {
					float maxTemp;
					float scale = 0.5f;
					if(node.HasValue ("maxTempScale"))
						float.TryParse(node.GetValue("maxTempScale"), out scale);
					if(scale > 0 && float.TryParse(node.GetValue("ridiculousMaxTemp"), out maxTemp))
                    {
                        Debug.Log("Using ridiculousMaxTemp = " + maxTemp.ToString() + " / maxTempScale =" + scale.ToString());
                        if (PartLoader.LoadedPartsList != null)
                        {
                            foreach (AvailablePart part in PartLoader.LoadedPartsList)
                            {
                                try
                                {
                                    // allow heat sinks. Also ignore engines until RF engine situation is finally sorted
                                    if (part.partPrefab != null && !part.partPrefab.Modules.Contains("ModuleHeatShield"))
                                    {
                                        float oldTemp = part.partPrefab.maxTemp;
                                        bool changed = false;
                                        if (part.partPrefab.maxTemp > maxTemp)
                                        {
                                            part.partPrefab.maxTemp = Mathf.Min(part.partPrefab.maxTemp * scale, maxTemp);
                                            changed = true;
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
                                catch (Exception e)
                                {
                                    Debug.Log(e.Message);
                                }
                            }
                        }
					}
				}
			}
		}
	}

	[KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ReentryPhysics : MonoBehaviour
    {
        static System.Version DREVersion = Assembly.GetExecutingAssembly().GetName().Version;


        protected bool isCompatible = true;
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

        public static GUIStyle warningMessageStyle = new GUIStyle();

        public static ScreenMessage chuteWarningMsg = new ScreenMessage("Warning: Chute deployment unsafe!", 1f, ScreenMessageStyle.UPPER_CENTER, warningMessageStyle);
        public static ScreenMessage crewGWarningMsg = new ScreenMessage("Reaching Crew G limit!", 1f, ScreenMessageStyle.UPPER_CENTER, warningMessageStyle);

        public static double specificGasConstant = 287.058;

        public static float shockwaveMultiplier = 1.0f;
        public static float shockwaveExponent = 1.0f;
        public static float heatMultiplier = 20.0f;
		public static float temperatureExponent = 1.0f;
		public static float densityExponent = 1.0f;

		public static float startThermal = 800.0f;
		public static float fullThermal = 1150.0f;
        public static float afxDensityExponent = 0.7f;
        public static float ablationMetric = 0.0066666666666667f;

        public static float gToleranceMult = 6.0f;
        public static float parachuteTempMult = 0.25f;

        public static bool legacyAero = false;
        public static bool dissipationCap = true;
        public static bool debugging = false;
        public static bool useAlternateDensity;
        public static bool useAlternateHeatModel = true;
        public static float parachuteDifficulty = 1f;

        public static float frameDensity = 0f;

        public void BodyChanged(GameEvents.FromToAction<CelestialBody, CelestialBody> body)
        {
            FindSpecificGasConstant(body.to);
        }
        
        public void FindSpecificGasConstant(CelestialBody body)
        {
            switch(body.bodyName)
            {
                case "Kerbin":
                    specificGasConstant = 287.058;
                    break;
                case "Duna":
                    specificGasConstant = 831.2;
                    break;
                case "Jool":
                    specificGasConstant = 3745.18;
                    break;
                case "Eve":
                    specificGasConstant = 850.1;
                    break;
                case "Laythe":
                    specificGasConstant = 287.058;
                    break;
                default:
                    specificGasConstant = 287.058;
                    break;
            }
        }
        
        public bool LegacyAero
        {
            get
            {
                return legacyAero;
            }
            set
            {
                legacyAero = value;
            }
        }
        public bool DissipationCap
        {
            get
            {
                return dissipationCap;
            }
            set
            {
                dissipationCap = value;
            }
        }
        public bool Debugging
        {
            get
            {
                return debugging;
            }
            set
            {
                debugging = value;
            }
        }

        protected Rect windowPos = new Rect(100, 100, 0, 0);

        public static float AltTemperatureDelta(double density, float shockwaveK, float partTempK)
        {
            if (shockwaveK < partTempK || density == 0 || shockwaveK < 0)
                return 0;
            double temp = 0.000183 * Math.Pow(frameVelocity.magnitude, 3) * Math.Sqrt(density);
            return (float) (((temp * 0.0005265650665) - partTempK) * heatMultiplier * TimeWarp.fixedDeltaTime);
        }

        public static float TemperatureDelta(double density, float shockwaveK, float partTempK)
		{
			if (shockwaveK < partTempK || density == 0 || shockwaveK < 0)
				return 0;
			return (float) ( Math.Pow (Math.Abs(shockwaveK - partTempK), temperatureExponent) 
                           * density
						   * heatMultiplier * TimeWarp.fixedDeltaTime);
		}


		public void Start()
		{
            if (!CompatibilityChecker.IsAllCompatible())
            {
                isCompatible = false;
                return;
            }
            enabled = true; // 0.24 compatibility
            Debug.Log("[DRE] - ReentryPhysics.Start(): LoadSettings(), Difficulty: " + DeadlyReentryScenario.Instance.DifficultyName);
            LoadSettings(); // Moved loading of REENTRY_EFFECTS into a generic loader which uses new difficulty settings

            warningMessageStyle.normal.textColor = Color.red;
            warningMessageStyle.fontSize = 20;
            warningMessageStyle.alignment = TextAnchor.UpperCenter;

            if (HighLogic.LoadedSceneIsFlight && (object)FlightGlobals.currentMainBody != null)
                FindSpecificGasConstant(FlightGlobals.currentMainBody);
            
            GameEvents.onDominantBodyChange.Add(BodyChanged);
        }

        public static void LoadSettings()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS"))
            {
                if (node.HasValue("name") && node.GetValue("name") == DeadlyReentryScenario.Instance.DifficultyName)
                {
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
                        double.TryParse(node.GetValue("crewGClamp"), out ModuleAeroReentry.crewGClamp);
                    if (node.HasValue("crewGPower"))
                        double.TryParse(node.GetValue("crewGPower"), out ModuleAeroReentry.crewGPower);
                    if (node.HasValue("crewGMin"))
                        double.TryParse(node.GetValue("crewGMin"), out ModuleAeroReentry.crewGMin);
                    if (node.HasValue("crewGWarn"))
                        double.TryParse(node.GetValue("crewGWarn"), out ModuleAeroReentry.crewGWarn);
                    if (node.HasValue("crewGLimit"))
                        double.TryParse(node.GetValue("crewGLimit"), out ModuleAeroReentry.crewGLimit);
                    if (node.HasValue("crewGKillChance"))
                        float.TryParse(node.GetValue("crewGKillChance"), out ModuleAeroReentry.crewGKillChance);
                    if (node.HasValue("ablationMetric"))
                        float.TryParse(node.GetValue("ablationMetric"), out ablationMetric);
                    if (node.HasValue("parachuteDifficulty"))
                        float.TryParse(node.GetValue("parachuteDifficulty"), out parachuteDifficulty);

                    
                    if(node.HasValue("debugging"))
                        bool.TryParse (node.GetValue ("debugging"), out debugging);
                    if(node.HasValue("legacyAero"))
                        bool.TryParse(node.GetValue("legacyAero"), out legacyAero);
                    if (node.HasValue("dissipationCap"))
                        bool.TryParse(node.GetValue("dissipationCap"), out dissipationCap);
                    if (node.HasValue("useAlternateDensity"))
                        bool.TryParse(node.GetValue("useAlternateDensity"), out useAlternateDensity);
                    //useAlternateHeatModel
                    if (node.HasValue("useAlternateHeatModel"))
                        bool.TryParse(node.GetValue("useAlternateHeatModel"), out useAlternateHeatModel);

                    break;
                }
            }
        }

        public static void SaveSettings()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS"))
            {
                if (node.HasValue("name") && node.GetValue("name") == DeadlyReentryScenario.Instance.DifficultyName)
                {
                    if(node.HasValue("shockwaveExponent"))
                        node.SetValue("shockwaveExponent", shockwaveExponent.ToString());
                    if (node.HasValue("shockwaveMultiplier"))
                        node.SetValue("shockwaveMultiplier", shockwaveMultiplier.ToString());
                    if(node.HasValue("heatMultiplier"))
                        node.SetValue ("heatMultiplier", heatMultiplier.ToString());
                    if(node.HasValue("startThermal"))
                        node.SetValue ("startThermal", startThermal.ToString());
                    if(node.HasValue("fullThermal"))
                        node.SetValue ("fullThermal", fullThermal.ToString());
                    if (node.HasValue("afxDensityExponent"))
                        node.SetValue("afxDensityExponent", afxDensityExponent.ToString());
                    if(node.HasValue("temperatureExponent"))
                        node.SetValue ("temperatureExponent", temperatureExponent.ToString());
                    if(node.HasValue("densityExponent"))
                        node.SetValue ("densityExponent", densityExponent.ToString());
                    
                    if (node.HasValue("gToleranceMult"))
                        node.SetValue("gToleranceMult", gToleranceMult.ToString());
                    
                    if (node.HasValue("parachuteTempMult"))
                        node.SetValue("parachuteTempMult", parachuteTempMult.ToString());
                    
                    
                    if (node.HasValue("crewGClamp"))
                        node.SetValue("crewGClamp", ModuleAeroReentry.crewGClamp.ToString());
                    if (node.HasValue("crewGPower"))
                        node.SetValue("crewGPower", ModuleAeroReentry.crewGPower.ToString());
                    if (node.HasValue("crewGMin"))
                        node.SetValue("crewGMin", ModuleAeroReentry.crewGMin.ToString());
                    if (node.HasValue("crewGWarn"))
                        node.SetValue("crewGWarn", ModuleAeroReentry.crewGWarn.ToString());
                    if (node.HasValue("crewGLimit"))
                        node.SetValue("crewGLimit", ModuleAeroReentry.crewGLimit.ToString());
                    if (node.HasValue("crewGKillChance"))
                        node.SetValue("crewGKillChance", ModuleAeroReentry.crewGKillChance.ToString());
                    
                    if(node.HasValue("debugging"))
                        node.SetValue("debugging", debugging.ToString());
                    if(node.HasValue("legacyAero"))
                        node.SetValue("legacyAero", legacyAero.ToString());
                    if(node.HasValue("dissipationCap"))
                        node.SetValue("dissipationCap", dissipationCap.ToString());
                    if(node.HasValue("useAlternateDensity"))
                        node.SetValue("useAlternateDensity", useAlternateDensity.ToString());
                    if(node.HasValue("useAlternateHeatModel"))
                        node.SetValue("useAlternateHeatModel", useAlternateHeatModel.ToString());
                    if(node.HasValue("parachuteDifficulty"))
                        node.SetValue("parachuteDifficulty", parachuteDifficulty.ToString());

                    break;
                }
            }
            SaveCustomSettings();
        }

        public void OnGUI()
        {
            if (isCompatible && debugging)
            {
                windowPos = GUILayout.Window("DeadlyReentry".GetHashCode(), windowPos, DrawWindow, "Deadly Reentry 6.4.0 Debug Menu");
            }
        }

        //public IEnumerator AeroFixer ()
        //{
         //   yield return new WaitForFixedUpdate();
         //   FixAeroFX();
         //   yield break;
        //}

        public IEnumerator FixAeroFX()
		{
            yield return new WaitForFixedUpdate();
            afx.airDensity = (float)(Math.Pow(frameDensity, afxDensityExponent));
			if (afx.velocity.magnitude < startThermal) // approximate speed where shockwaves begin visibly glowing
				afx.state = 0;
			else if (afx.velocity.magnitude >= fullThermal)
				afx.state = 1;
			else
				afx.state = (afx.velocity.magnitude - startThermal) / (fullThermal - startThermal);
		}

        public void FixedUpdate()
        {
            if (!this.isCompatible)
            {
                return;
            }
            ReentryPhysics.frameVelocity = Krakensbane.GetFrameVelocityV3f () - Krakensbane.GetLastCorrection () * (double)TimeWarp.fixedDeltaTime;
            if (FlightGlobals.ActiveVessel != null)
            {
                if (!ReentryPhysics.legacyAero)
                {
                    ReentryPhysics.frameDensity = (float)(FlightGlobals.ActiveVessel.staticPressure * 101325.0 / (specificGasConstant * (Math.Max (-160.0, (double)FlightGlobals.ActiveVessel.flightIntegrator.getExternalTemperature()) + 273.15)));
                }
                else
                {
                    ReentryPhysics.frameDensity = (float)FlightGlobals.ActiveVessel.atmDensity;
                }
            }
            StartCoroutine (this.FixAeroFX());
        }

		public void LateUpdate()
		{
            if (!isCompatible)
                return;
		}

        public void Update()
        {
            if (!isCompatible)
                return;
            //if (Input.GetKeyDown(KeyCode.R) && Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.D))
            //{
            //    debugging = !debugging;
            //}

            if (FlightGlobals.ready)
            {
                if ((afx != null))
                {
                    // Only do this in FixedUpdate() from now on. (new system should be immune from flickering)
  //                  AeroFixer();
				
					foreach (Vessel vessel in FlightGlobals.Vessels) 
					{
						if(vessel.loaded)// && afx.FxScalar > 0)
						{
                            foreach (Part p in vessel.Parts)
                            {
                                if (!(p.Modules.Contains("ModuleAeroReentry") || p.Modules.Contains("ModuleHeatShield")))
                                    p.AddModule("ModuleAeroReentry"); // thanks a.g.!
                            }
                        }
                    }
                }
            }
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
            string newcrewGClamp = GUILayout.TextField(ModuleAeroReentry.crewGClamp.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Exponent", labelStyle);
            string newcrewGPower = GUILayout.TextField(ModuleAeroReentry.crewGPower.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Min", labelStyle);
            string newcrewGMin = GUILayout.TextField(ModuleAeroReentry.crewGMin.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Warn Level", labelStyle);
            string newcrewGWarn = GUILayout.TextField(ModuleAeroReentry.crewGWarn.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Kill threshold", labelStyle);
            string newcrewGLimit = GUILayout.TextField(ModuleAeroReentry.crewGLimit.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Crew G Kill chance per update", labelStyle);
            string newcrewGKillChance = GUILayout.TextField(ModuleAeroReentry.crewGKillChance.ToString(), GUILayout.MinWidth(100));
            GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			if (GUILayout.Button ("Save"))
            {
                SaveSettings();
			}
			GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (GUI.changed)
            {
                //print("GUI CHANGED!!!111oneone");
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
                    ModuleAeroReentry.crewGClamp = newValue;
                }

				if (float.TryParse(newcrewGPower, out newValue))
				{
					ModuleAeroReentry.crewGPower = newValue;
				}

                if (float.TryParse(newcrewGMin, out newValue))
                {
                    ModuleAeroReentry.crewGMin = newValue;
                }
				if (float.TryParse(newcrewGWarn, out newValue))
				{
					ModuleAeroReentry.crewGWarn = newValue;
				}
				if (float.TryParse(newcrewGLimit, out newValue))
				{
					ModuleAeroReentry.crewGLimit = newValue;
				}
                if (float.TryParse(newcrewGKillChance, out newValue))
				{
                    ModuleAeroReentry.crewGKillChance = newValue;
				}
			}
        }

        public static void SaveCustomSettings()
        {
            string[] difficultyNames = {"Easy", "Default", "Hard"};
            bool btmp;
            float ftmp;
            double dtmp;

            ConfigNode savenode = new ConfigNode();
            foreach(string difficulty in difficultyNames)
            {
                foreach (ConfigNode settingNode in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS"))
                {
                    if (settingNode.HasValue("name") && settingNode.GetValue("name") == difficulty)
                    {
                        ConfigNode node = new ConfigNode("@REENTRY_EFFECTS[" + difficulty + "]:AFTER[DeadlyReentry]");

                        if(settingNode.HasValue("shockwaveExponent"))
                        {
                            float.TryParse(settingNode.GetValue("shockwaveExponent"), out ftmp);
                            node.AddValue ("@shockwaveExponent", ftmp);
                        }
                        if (settingNode.HasValue("shockwaveMultiplier"))
                        {
                            float.TryParse(settingNode.GetValue("shockwaveMultiplier"), out ftmp);
                            node.AddValue ("@shockwaveMultiplier", ftmp);
                        }
                        if(settingNode.HasValue("heatMultiplier"))
                        {
                            float.TryParse (settingNode.GetValue ("heatMultiplier"), out ftmp);
                            node.AddValue ("@heatMultiplier", ftmp);
                        }
                        if(settingNode.HasValue("startThermal"))
                        {
                            float.TryParse (settingNode.GetValue ("startThermal"), out ftmp);
                            node.AddValue ("@startThermal", ftmp);
                        }
                        if(settingNode.HasValue("fullThermal"))
                        {
                            float.TryParse (settingNode.GetValue ("fullThermal"), out ftmp);
                            node.AddValue ("@fullThermal", ftmp);
                        }
                        if (settingNode.HasValue("afxDensityExponent"))
                        {
                            float.TryParse(settingNode.GetValue("afxDensityExponent"), out ftmp);
                            node.AddValue ("@afxDensityExponent", ftmp);
                        }
                        if(settingNode.HasValue("temperatureExponent"))
                        {
                            float.TryParse (settingNode.GetValue ("temperatureExponent"), out ftmp);
                            node.AddValue ("@temperatureExponent", ftmp);
                        }
                        if(settingNode.HasValue("densityExponent"))
                        {
                            float.TryParse (settingNode.GetValue ("densityExponent"), out ftmp);
                            node.AddValue ("@densityExponent", ftmp);
                        }
                        
                        if (settingNode.HasValue("gToleranceMult"))
                        {
                            float.TryParse(settingNode.GetValue("gToleranceMult"), out ftmp);
                            node.AddValue ("@gToleranceMult", ftmp);
                        }
                        
                        if (settingNode.HasValue("parachuteTempMult"))
                        {
                            float.TryParse(settingNode.GetValue("parachuteTempMult"), out ftmp);
                            node.AddValue ("@parachuteTempMult", ftmp);
                        }
                        if (settingNode.HasValue("crewGKillChance"))
                        {
                            float.TryParse(settingNode.GetValue("crewGKillChance"), out ftmp);
                            node.AddValue ("@crewGKillChance", ftmp);
                        }
                        if(settingNode.HasValue("parachuteDifficulty"))
                        {
                            float.TryParse(settingNode.GetValue("parachuteDifficulty"), out ftmp);
                            node.AddValue("@parachuteDifficulty", ftmp.ToString());
                        }

                        
                        if (settingNode.HasValue("crewGClamp"))
                        {
                            double.TryParse(settingNode.GetValue("crewGClamp"), out dtmp);
                            node.AddValue ("@crewGClamp", dtmp);
                        }
                        if (settingNode.HasValue("crewGPower"))
                        {
                            double.TryParse(settingNode.GetValue("crewGPower"), out dtmp);
                            node.AddValue ("@crewGPower", dtmp);
                        }
                        if (settingNode.HasValue("crewGMin"))
                        {
                            double.TryParse(settingNode.GetValue("crewGMin"), out dtmp);
                            node.AddValue ("@crewGMin", dtmp);
                        }
                        if (settingNode.HasValue("crewGWarn"))
                        {
                            double.TryParse(settingNode.GetValue("crewGWarn"), out dtmp);
                            node.AddValue ("@crewGWarn", dtmp);
                        }
                        if (settingNode.HasValue("crewGLimit"))
                        {
                            double.TryParse(settingNode.GetValue("crewGLimit"), out dtmp);
                            node.AddValue ("@crewGLimit", dtmp);
                        }
                        
                        if(settingNode.HasValue("legacyAero"))
                        {
                            bool.TryParse(settingNode.GetValue("legacyAero"), out btmp);
                            Debug.Log("[DRE] - legacyAero = " + btmp);
                            node.AddValue ("@legacyAero", btmp.ToString());
                        }
                        if (settingNode.HasValue("dissipationCap"))
                        {
                            bool.TryParse(settingNode.GetValue("dissipationCap"), out btmp);
                            node.AddValue("@dissipationCap", btmp.ToString());
                        }
                        if (settingNode.HasValue("useAlternateDensity"))
                        {
                            bool.TryParse(settingNode.GetValue("useAlternateDensity"), out btmp);
                            node.AddValue("@useAlternateDensity", btmp.ToString());
                        }
                        if (settingNode.HasValue("useAlternateDensity"))
                        {
                            bool.TryParse(settingNode.GetValue("useAlternateHeatModel"), out btmp);
                            node.AddValue("@useAlternateHeatModel", btmp.ToString());
                        }

                        savenode.AddNode (node);
                        break;
                    }
                }
            }
            savenode.Save (KSPUtil.ApplicationRootPath.Replace ("\\", "/") + "GameData/DeadlyReentry/custom.cfg");
        }
    }
}
