using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;

namespace RealHeat
{
	public class ModuleDamage: PartModule
	{
		UIPartActionWindow _myWindow = null; 
		UIPartActionWindow myWindow {
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
			if(RealHeatUtils.debugging)
				print (fx.audio.clip.name);

		}
		FXGroup _gForceFX = null;
		FXGroup gForceFX {
			get {
				if(_gForceFX == null) {
					_gForceFX = new FXGroup (part.partName + "_Crushing");
					_gForceFX.audio = gameObject.AddComponent<AudioSource>();
					_gForceFX.audio.clip = GameDatabase.Instance.GetAudioClip("RealHeat/Sounds/gforce_damage");
					_gForceFX.audio.volume = GameSettings.SHIP_VOLUME;
					_gForceFX.audio.Stop ();
				}
				return _gForceFX;

			}
		}
		FXGroup _ablationSmokeFX = null;
		FXGroup ablationSmokeFX {
			get {
				if(_ablationSmokeFX == null) {
					_ablationSmokeFX = new FXGroup (part.partName + "_Smoking");
					_ablationSmokeFX.fxEmitters.Add (Emitter("fx_smokeTrail_medium").GetComponent<ParticleEmitter>());
				}
				return _ablationSmokeFX;
			}
		}

		FXGroup _ablationFX = null;
		FXGroup ablationFX {
			get {
				if(_ablationFX == null) {
					_ablationFX = new FXGroup (part.partName + "_Burning");
					_ablationFX.fxEmitters.Add (Emitter("fx_exhaustFlame_yellow").GetComponent<ParticleEmitter>());
                    _ablationFX.fxEmitters.Add(Emitter("fx_exhaustSparks_yellow").GetComponent<ParticleEmitter>());
					_ablationFX.audio = gameObject.AddComponent<AudioSource>();
					_ablationFX.audio.clip = GameDatabase.Instance.GetAudioClip("RealHeat/Sounds/fire_damage");
                    _ablationFX.audio.volume = GameSettings.SHIP_VOLUME;
					_ablationFX.audio.Stop ();

				}
				return _ablationFX;
			}
		}

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
        private double counter = 0;
        public static double crewGClamp = 30;
        public static double crewGPower = 4;
        public static double crewGMin = 5;
        public static double crewGWarn = 300000;
        public static double crewGLimit = 600000;
        public static double crewGKillChance = 0.75;
		protected float deltaTime = 0f;

        // constants
        public const float CTOK = 273.15f;

        // flags
        private bool is_debugging = false;
        private bool is_on_fire = false;
        private bool is_gforce_fx_playing = false;
        private bool is_engine = false;
        private bool is_eva = false;

        // Interaction
        private ModuleParachute parachute = null;
        private PartModule realChute = null;
        private Type rCType = null;
        private bool hasParachute = false;
        private ModuleRealHeat heatModule = null;

        [KSPEvent(guiName = "No Damage", guiActiveUnfocused = true, externalToEVAOnly = true, guiActive = false, unfocusedRange = 4f)]
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
            try
            {
                if (damage > 0.5)
                    Events[""].guiName = "Repair Critical Damage";
                else if (damage > 0.25)
                    Events["RepairDamage"].guiName = "Repair Heavy Damage";
                else if (damage > 0.125)
                    Events["RepairDamage"].guiName = "Repair Moderate Damage";
                else if (damage > 0)
                    Events["RepairDamage"].guiName = "Repair Light Damage";
                else
                    Events["RepairDamage"].guiName = "No Damage";
            }
            catch
            {
            }

		}

        public override void OnAwake()
        {
            base.OnAwake();
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
            if (part.Modules.Contains("ModuleRealHeat"))
                heatModule = (ModuleRealHeat)part.Modules["ModuleRealHeat"];
            else
                Debug.Log("*RF* ERROR: Part does not contain ModuleRealHeat");
        }

		public override void OnStart (StartState state)
		{
            counter = 0;
			if (state == StartState.Editor)
				return;
			SetDamageLabel ();
			if (myWindow != null)
				myWindow.displayDirty = true;
			// moved part detection logic to OnAWake

		}

		public void FixedUpdate ()
		{
            // handle all heat management
            Vector3 velocity = Vector3.zero;
			deltaTime = TimeWarp.fixedDeltaTime;
            if (counter < 5.0)
            { // avoid Kraken-kill on spawn
                counter += deltaTime;
                lastGForce = 0;
                return;
            }
            if (is_debugging != RealHeatUtils.debugging)
            {
                is_debugging = RealHeatUtils.debugging;
                Fields["displayGForce"].guiActive = RealHeatUtils.debugging;
                Fields["gExperienced"].guiActive = RealHeatUtils.debugging;
            }
            velocity = part.vessel.orbit.GetVel() - part.vessel.mainBody.getRFrmVel(part.vessel.vesselTransform.position);

            CheckForFire(velocity);
            CheckGeeForces();
		}


		public void AddDamage(float dmg)
		{
			if (dead || part == null || part.partInfo == null || part.partInfo.partPrefab == null)
				return;
			if(RealHeatUtils.debugging)
				print (part.partInfo.title + ": +" + dmg + " damage");
			damage += dmg;
			// trying with this disabled; it kills parts too quickly.
			//part.maxTemp = part.partInfo.partPrefab.maxTemp * (1 - 0.15f * damage);
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

                if (gTolerance < 0)
                {
                    if (is_engine && damage < 1)
                        gTolerance = (float)Math.Pow(UnityEngine.Random.Range(11.9f, 12.1f) * part.crashTolerance, 0.5);
                    else
                        gTolerance = (float)Math.Pow(UnityEngine.Random.Range(5.9f, 6.1f) * part.crashTolerance, 0.5);

                    gTolerance *= RealHeatUtils.gToleranceMult;
                }
                if (gTolerance >= 0 && displayGForce > gTolerance)
                { // G tolerance is based roughly on crashTolerance
                    AddDamage(deltaTime * (float)((displayGForce / gTolerance) - 1));
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
                    part.explode();
                    return;
                }
                if (displayGForce >= crewGMin)
                {
                    gExperienced += Math.Pow(Math.Min(Math.Abs(displayGForce), crewGClamp), crewGPower) * deltaTime;
                    List<ProtoCrewMember> crew = part.protoModuleCrew; //vessel.GetVesselCrew();
                    if (gExperienced > crewGWarn && crew.Count > 0)
                    {
                            
                        if (gExperienced < crewGLimit)
                                ScreenMessages.PostScreenMessage("Reaching Crew G limit!", 3f, ScreenMessageStyle.UPPER_CENTER);
                        else
                        {
                            // borrowed from TAC Life Support
                            if (UnityEngine.Random.Range(0, 1) < crewGKillChance)
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
                                Debug.Log("*RH* [" + Time.time + "]: " + vessel.vesselName + " - " + member.name + " died of G-force damage.");

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

		public void CheckForFire(Vector3 velocity)
		{
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)
            {
                if (FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH || FlightGlobals.ActiveVessel.missionTime > 2.0)
                {
                    if (dead)
                        return;
                    // deal with parachutes here
                    if (hasParachute)
                    {
                        bool cut = heatModule.ambient - CTOK + Math.Pow(heatModule.density, RealHeatUtils.densityExponent) * heatModule.shockwave * 10f
                            > part.maxTemp * RealHeatUtils.parachuteTempMult;
                        if ((object)parachute != null)
                        {
                            ModuleParachute p = parachute;
                            if ((p.deploymentState == ModuleParachute.deploymentStates.DEPLOYED || p.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED) && cut)
                                p.CutParachute();
                        }
                        if ((object)realChute != null)
                        {
                            if (!(bool)rCType.GetProperty("anyDeployed").GetValue(realChute, null) && cut)
                                rCType.GetMethod("GUICut").Invoke(realChute, null);
                        }
                    }
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
                        tempRatio = part.temperature / part.maxTemp;
                        PlaySound(ablationFX, tempRatio * tempRatio);

                        new GameEvents.ExplosionReaction(0, damage);

                        if (is_engine && damage < 1)
                            part.temperature = UnityEngine.Random.Range(0.97f + 0.05f * damage, 0.98f + 0.05f * damage) * part.maxTemp;
                        else if (damage < 1)// non-engines can keep burning
                            part.temperature += (1f+damage*4f) * (1f+damage*4f) * 200f * deltaTime;

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
                                                           + part.partInfo.title + " burned up on reentry.");
                                part.explode();
                                return;
                            }
                        }
                        else
                        {
                            is_on_fire = true;
                            foreach (ParticleEmitter fx in ablationFX.fxEmitters)
                            {
                                fx.gameObject.SetActive(true);
                                fx.gameObject.transform.LookAt(part.transform.position + velocity);
                                fx.gameObject.transform.Rotate(90, 0, 0);
                            }
                            foreach (ParticleEmitter fx in ablationSmokeFX.fxEmitters)
                            {
                                fx.gameObject.SetActive(heatModule.density > 0.1);
                                fx.gameObject.transform.LookAt(part.transform.position + velocity);
                                fx.gameObject.transform.Rotate(90, 0, 0);
                            }
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
		public GameObject Emitter(string fxName) {
			GameObject fx = (GameObject)UnityEngine.Object.Instantiate (UnityEngine.Resources.Load ("Effects/" + fxName));

			fx.transform.parent = part.transform;
			fx.transform.localPosition = new Vector3 (0, 0, 0);
			fx.transform.localRotation = Quaternion.identity;
			fx.SetActive (false);
			return fx;

		}

	}
}

