using System;

using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;
using ModularFI;

namespace DeadlyReentry
{
    class ModuleAeroReentry : PartModule
    {
        /// <summary>
        /// Hull temperature as opposed to internal temperature (part.temperature)
        /// </summary>
        [KSPField(isPersistant = true)]
        public double skinTemperature = -1.0;

        [KSPField]
        public double skinMaxTemp = -1.0;

        [KSPField]
        public double skinThermalMassModifier = -1.0;
        
        [KSPField(isPersistant = false)]
        public double skinThicknessFactor = 0.1;
        
        [KSPField(isPersistant = false)]
        public double skinHeatConductivity = 0.12;
        
        [KSPField(isPersistant = false)]
        public double skinThermalMass = -1.0;
        
        public double skinThermalMassReciprocal;

        public bool is_debugging;

        // Debug Displays
        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Temp.", guiUnits = "K",   guiFormat = "x.00")]
        public string skinTemperatureDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Thermal Mass.", guiUnits = "",   guiFormat = "x.00")]
        public string skinThermalMassDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Rad. Area", guiUnits = "m2",   guiFormat = "x.00")]
        public string RadiativeAreaDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Exp. Area", guiUnits = "m2",   guiFormat = "x.00")]
        public string ExposedAreaDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Acceleration", guiUnits = "G",   guiFormat = "F3")]
        public double displayGForce;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Damage", guiUnits = "",   guiFormat = "G")]
        public string displayDamage;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Cumulative G", guiUnits = "", guiFormat = "F0")]
        public double gExperienced = 0;

        private ModularFlightIntegrator fi;
        public ModularFlightIntegrator FI
        {
            get
            {
                if ((object)fi == null)
                    fi = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
                return fi;
            }
            set {fi = value;}
        }

        public ModularFlightIntegrator.PartThermalData ptd;

        private double lastGForce = 0;

        [KSPField(isPersistant = true)]
        private bool dead;

        [KSPField]
        public float gTolerance = -1;

        [KSPField(isPersistant = true)]
        public float crashTolerance = 8;
        
        [KSPField(isPersistant = true)]
        public float damage = 0;

        public static double crewGClamp = 30;
        public static double crewGPower = 4;
        public static double crewGMin = 5;
        public static double crewGWarn = 300000;
        public static double crewGLimit = 600000;
        public static double crewGKillChance = 0.75f;
        
        private bool isCompatible = true;
        
        private bool is_on_fire = false;
        private bool is_gforce_fx_playing = false;
        
        private bool is_engine = false;
        private bool is_eva = false;


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
            //if(this.is_debugging)
            //    print (fx.audio.clip.name);
            
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

        public override void OnAwake()
        {
            base.OnAwake();
            if (!CompatibilityChecker.IsAllCompatible())
            {
                isCompatible = false;
                return;
            }
        }

        public virtual void Start()
        {
            if (!isCompatible)
                return;
            //counter = 0;
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            
            
            //FI = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
            if (skinThermalMassModifier == -1.0)
                skinThermalMassModifier = part.thermalMassModifier;
            
            if (skinMaxTemp == -1.0)
                skinMaxTemp = part.maxTemp;
            
            // only one of skinThermalMassModifier and skinThicknessFactor should be configured
            if (skinThermalMass == -1.0)
                skinThermalMass = (double)part.mass * PhysicsGlobals.StandardSpecificHeatCapacity * skinThermalMassModifier * skinThicknessFactor;
            skinThermalMassReciprocal = 1.0 / Math.Max (skinThermalMass, 0.001);
            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
        }
        
        public void OnVesselWasModified(Vessel v)
        {
            if (v == vessel)
                FI = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
        }

        public virtual void FixedUpdate()
        {
            if (!FlightGlobals.ready)
                return;

            //print("FlightGlobals.ready = true");

            //if ((object)fi == null)
            //{
            //    fi = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
            //    if ((object)fi == null)
            //        print("Fatal error! Unable to locate ModularFlightIntegrator for vessel (" + vessel.vesselName + ")");
            //}
            //else
                //print("ModularFlightIntegrator found for this vessel.");
            


            // HACK skipping an update on IsAnalytical is a quick hack. Need to handle Analytical Mode properly
             if (FI.IsAnalytical || FI.RecreateThermalGraph)
                 return;

            if (skinTemperature == -1.0 && !Double.IsNaN(vessel.externalTemperature))
            {
                skinTemperature = vessel.externalTemperature;
                //print("Uninitialized skinTemperature initialized.");
            }
            if (PhysicsGlobals.ThermalDataDisplay)
            {
                skinTemperatureDisplay = skinTemperature.ToString ("F4");
                skinThermalMassDisplay = skinThermalMass.ToString();
                RadiativeAreaDisplay = part.radiativeArea.ToString ("F4");
                ExposedAreaDisplay = part.exposedArea.ToString("F4");
            }
            //print("Starting UpdateSkinThermals() coroutine.");
            StartCoroutine (UpdateSkinThermals());
        }

        public IEnumerator UpdateSkinThermals()
        {
            yield return new WaitForFixedUpdate();
            
            ptd = FI.PartThermalDataList.Where(p => p.part == part).FirstOrDefault();
            
            if(PhysicsGlobals.ThermalConvectionEnabled)
                UpdateConvection();
            if(PhysicsGlobals.ThermalRadiationEnabled)
                UpdateRadiation();
            if(PhysicsGlobals.ThermalConductionEnabled)
                UpdateSkinConduction();
            
            if (skinTemperature > skinMaxTemp)
            {
                FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] "
                                          + part.partInfo.title + " burned up from overheating.");
                
                if ( part is StrutConnector )
                {
                    ((StrutConnector)part).BreakJoint();
                }
                
                part.explode();
            }
            CheckForFire();
            CheckGeeForces();
        }

        public void UpdateConvection()
        {
            // get sub/transonic convection
            double convectionArea = UtilMath.Lerp(
                part.radiativeArea,
                part.exposedArea,
                PhysicsGlobals.FullConvectionAreaMin + (part.machNumber - PhysicsGlobals.FullToCrossSectionLerpStart) / (PhysicsGlobals.FullToCrossSectionLerpEnd - PhysicsGlobals.FullToCrossSectionLerpStart));
            
            double convectiveFlux = (part.externalTemperature - skinTemperature) * FI.convectiveCoefficient * convectionArea;
            
            // get mach convection
            // defaults to starting at M=2 and being full at M=3
            double machLerp = (part.machNumber - PhysicsGlobals.MachConvectionStart) / (PhysicsGlobals.MachConvectionEnd - PhysicsGlobals.MachConvectionStart);
            
            if (machLerp > 0d)
            {
                machLerp = Math.Min(1d, Math.Pow(machLerp, PhysicsGlobals.MachConvectionExponent));
                
                // get flux
                double machHeatingFlux = convectionArea * FI.convectiveMachFlux;
                convectiveFlux = UtilMath.LerpUnclamped(convectiveFlux, machHeatingFlux, machLerp);
                
                // get steady-state radiative temperature for this flux. Assume 0.5 emissivity, because while part emissivity might be higher,
                // radiative area will be way higher than convective area, so this will be safe.
                // We multiply in the convective area multiplier because that accounts for both how much area is occluded, but also modifiers
                // to the shock temperature.
                double machExtTemp = Math.Pow(0.5d * FI.convectiveMachFlux * ptd.convectionAreaMultiplier * part.heatConvectiveConstant
                                              / (PhysicsGlobals.StefanBoltzmanConstant * PhysicsGlobals.RadiationFactor), 1d / PhysicsGlobals.PartEmissivityExponent);
                part.externalTemperature = Math.Max(part.externalTemperature, UtilMath.LerpUnclamped(part.externalTemperature, machExtTemp, machLerp));
            }
            convectiveFlux *= 0.001d * part.heatConvectiveConstant * ptd.convectionAreaMultiplier; // W to kW, scalars
            part.thermalConvectionFlux = convectiveFlux;
            skinTemperature = Math.Max((skinTemperature + convectiveFlux * skinThermalMassReciprocal * TimeWarp.fixedDeltaTime), PhysicsGlobals.SpaceTemperature);
        }
/*
        public void UpdateConvection ()
        {
            // get sub/transonic convection
            double convectionArea = UtilMath.Lerp(part.radiativeArea, part.exposedArea,
                                                  (part.machNumber - PhysicsGlobals.FullToCrossSectionLerpStart) / (PhysicsGlobals.FullToCrossSectionLerpEnd - PhysicsGlobals.FullToCrossSectionLerpStart));
            
            double convectiveFlux = (part.externalTemperature - skinTemperature) * FI.convectiveCoefficient * convectionArea;
            //print("convectionArea = " + convectionArea.ToString("F4"));
            //print("convectiveFlux = " + convectiveFlux.ToString("F4"));

            // get hypersonic convection
            // defaults to starting at M=0.8 and being full at M=2.05
            double machLerp = (part.machNumber - PhysicsGlobals.MachConvectionStart) / (PhysicsGlobals.MachConvectionEnd - PhysicsGlobals.MachConvectionStart);
            if (machLerp > 0)
            {
                double machHeatingFlux =
                    part.exposedArea
                        * 1.83e-4d
                        * Math.Pow(part.vessel.speed, PhysicsGlobals.ConvectionVelocityExponent)
                        * Math.Sqrt(Math.Pow(part.atmDensity, PhysicsGlobals.ConvectionDensityExponent)/Math.Max(0.625d,Math.Sqrt(part.exposedArea / Math.PI))); // should be sqrt(density/nose radiu)s
                
                machHeatingFlux *= (double)PhysicsGlobals.ConvectionFactor;
                convectiveFlux = UtilMath.Lerp(convectiveFlux, machHeatingFlux, machLerp);
                //print("machHeatingFlux = " + machHeatingFlux.ToString("F4"));
            }
            convectiveFlux *= 0.001d * part.heatConvectiveConstant * ptd.convectionAreaMultiplier; // W to kW, scalars
            part.thermalConvectionFlux = convectiveFlux;
            //print("Final convectionFlux = " + convectiveFlux.ToString("F4"));
            skinTemperature = Math.Max(skinTemperature + (convectiveFlux * skinThermalMassReciprocal * TimeWarp.fixedDeltaTime), PhysicsGlobals.SpaceTemperature);
        }
        */

        public void UpdateRadiation()
        {
            
            double scalar = part.emissiveConstant // local scalar
                * PhysicsGlobals.RadiationFactor // global scalar
                    * 0.001d; // W to kW
            double finalScalar = skinThermalMassReciprocal * (double)TimeWarp.fixedDeltaTime;
            double sunFlux = 0d;
            double tempTemp = skinTemperature;
            
            
            if (vessel.directSunlight)
            {
                // assume half the surface area is under sunlight
                sunFlux = _GetSunArea(fi, ptd) * scalar * FI.solarFlux * FI.solarFluxMultiplier;
                tempTemp += sunFlux * finalScalar;
                //print("Temp + sunFlux = " + tempTemp.ToString("F4"));
            }
            double bodyFlux = FI.bodyEmissiveFlux + FI.bodyAlbedoFlux;
            if (bodyFlux > 0d)
            {
                tempTemp += UtilMath.Lerp(0.0, bodyFlux, FI.DensityThermalLerp) * _GetBodyArea(ptd) * scalar * finalScalar;
                //print("Temp + bodyFlux = " + tempTemp.ToString("F4"));
            }
            
            // Radiative flux = S-Bconst*e*A * (T^4 - radInT^4)

            //part.temperature = Math.Max(tempTemp, PhysicsGlobals.SpaceTemperature);
            double backgroundRadiationTemp = UtilMath.Lerp(FI.atmosphericTemperature, PhysicsGlobals.SpaceTemperature, FI.DensityThermalLerp);
            
            double radOut = -(Math.Pow(tempTemp, PhysicsGlobals.PartEmissivityExponent)
                - Math.Pow (backgroundRadiationTemp, PhysicsGlobals.PartEmissivityExponent)) // Using modified background radiation with no reentry radiant flux
                    * PhysicsGlobals.StefanBoltzmanConstant * scalar * part.radiativeArea;
            tempTemp += radOut * finalScalar;
            //print("Temp + radOut =" + tempTemp.ToString("F4"));
            part.thermalRadiationFlux = radOut + sunFlux;
            
            skinTemperature = Math.Max(tempTemp, PhysicsGlobals.SpaceTemperature);
        }

        public void UpdateSkinConduction()
        {
            double timeConductionFactor = PhysicsGlobals.ConductionFactor * Time.fixedDeltaTime;
            double temperatureDelta = skinTemperature - part.temperature;
            double energyTransferred =
                temperatureDelta
                    * Math.Min(skinThermalMass, part.thermalMass) * 0.5d
                    * Math.Max(0d, Math.Min(1d,
                                            timeConductionFactor
                                            * Math.Min(skinHeatConductivity, part.heatConductivity)
                                            * part.radiativeArea)); // should be contact area... how large a value should we use?
            
            double kilowatts = energyTransferred * FI.WarpReciprocal;
            double temperatureLost = energyTransferred * skinThermalMassReciprocal;
            double temperatureRecieved = energyTransferred * part.thermalMassReciprocal;
            
            //skinThermalConductionFlux -= kilowatts;
            //part.thermalConductionFlux += kilowatts;
            
            skinTemperature = Math.Max(skinTemperature - temperatureLost, PhysicsGlobals.SpaceTemperature);
            part.AddThermalFlux(kilowatts);
        }

        /*
        public void UpdateSkinConduction()
        {
            if (PhysicsGlobals.ThermalConductionEnabled)
            {
                double temperatureDelta = skinTemperature - part.temperature;
                double surfaceConductionFlux = temperatureDelta * skinHeatConductivity * PhysicsGlobals.ConductionFactor * skinThermalMass * 0.001;
                
                //print (temperatureDelta.ToString ("N8"));
                //print (surfaceConductionFlux.ToString ("N8"));
                
                part.AddThermalFlux (surfaceConductionFlux);
                skinTemperature -= surfaceConductionFlux * skinThermalMassReciprocal * (double)TimeWarp.fixedDeltaTime;
            }
        }
        */



        public virtual void Update()
        {
            if (is_debugging != PhysicsGlobals.ThermalDataDisplay)
            {
                is_debugging = PhysicsGlobals.ThermalDataDisplay;
                Fields["skinTemperatureDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                Fields["skinThermalMassDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                Fields["RadiativeAreaDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                Fields["ExposedAreaDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                if (myWindow != null)
                {
                    myWindow.displayDirty = true;
                }
            }
        }

        public void CheckGeeForces()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)
            {
                if (dead || (object)vessel == null || TimeWarp.fixedDeltaTime > 0.5 || TimeWarp.fixedDeltaTime <= 0)
                    return; // don't check G-forces in warp
                
                double geeForce = vessel.geeForce_immediate;
                if (geeForce > 40 && geeForce > lastGForce)
                {
                    // G forces over 40 are probably a Kraken twitch unless they last multiple frames
                    displayGForce = displayGForce * (1 - TimeWarp.fixedDeltaTime) + (float)(lastGForce * TimeWarp.fixedDeltaTime);
                }
                else
                {
                    //keep a running average of G force over 1s, to further prevent absurd spikes (mostly decouplers & parachutes)
                    displayGForce = displayGForce * (1 - TimeWarp.fixedDeltaTime) + (float)(geeForce * TimeWarp.fixedDeltaTime);
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
                    AddDamage(TimeWarp.fixedDeltaTime * (float)(displayGForce / gTolerance - 1));
                    if (!is_eva)
                    { // kerbal bones shouldn't sound like metal when they break.
                        gForceFX.audio.pitch = (float)(displayGForce / gTolerance);
                        PlaySound(gForceFX, damage * 0.3f + 0.7f);
                        is_gforce_fx_playing = true;
                    }
                }
                else if (is_gforce_fx_playing)
                {
                    double new_volume = (gForceFX.audio.volume *= 0.8f);
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
                    gExperienced += Math.Pow(Math.Min(Math.Abs(Math.Max(displayGForce, geeForce)), crewGClamp), crewGPower) * TimeWarp.fixedDeltaTime;
                    List<ProtoCrewMember> crew = part.protoModuleCrew; //vessel.GetVesselCrew();
                    if (gExperienced > crewGWarn && crew.Count > 0)
                    {
                        
                        if (gExperienced < crewGLimit)
                            ScreenMessages.PostScreenMessage(ReentryPhysics.crewGWarningMsg, false);
                        else
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

        public void AddDamage(float dmg)
        {
            if (dead || part == null || part.partInfo == null || part.partInfo.partPrefab == null)
                return;
            if(is_debugging)
                print (part.partInfo.title + ": +" + dmg + " damage");
            damage += dmg;
            part.maxTemp = part.partInfo.partPrefab.maxTemp * (1 - 0.15f * damage);
            part.breakingForce = part.partInfo.partPrefab.breakingForce * (1 - damage);
            part.breakingTorque = part.partInfo.partPrefab.breakingTorque * (1 - damage);
            part.crashTolerance = part.partInfo.partPrefab.crashTolerance * (1 - 0.5f * damage);
            SetDamageLabel ();
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

        public void CheckForFire()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready)
            {
                if (FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH || FlightGlobals.ActiveVessel.missionTime > 2.0)
                {
                    if (dead)
                        return;
                    double damageThreshold;
                    
                    if (is_engine && damage < 1)
                        damageThreshold = part.maxTemp * 0.975;
                    else if (is_eva)
                    {
                        damageThreshold = 800 * (1 - damage) * (1 - damage);
                        part.maxTemp = 900;
                    }
                    else
                        damageThreshold = part.maxTemp * 0.85;
                    if (part.temperature > damageThreshold)
                    {
                        // Handle client-side fire stuff.
                        // OH GOD IT'S ON FIRE.
                        float tempRatio = (float)((part.temperature / damageThreshold) - 1.0);
                        tempRatio *= (float)((part.temperature / part.maxTemp) * (part.temperature / part.maxTemp) * 4.0);
                        AddDamage(TimeWarp.deltaTime * (float)((damage + 1.0) * tempRatio));
                        float soundTempRatio = (float)(part.temperature / part.maxTemp);
                        PlaySound(ablationFX, soundTempRatio * soundTempRatio);
                        
                        if (is_engine && damage < 1)
                            part.temperature = UnityEngine.Random.Range(0.97f + 0.05f * damage, 0.98f + 0.05f * damage) * part.maxTemp;
                        else if (damage < 1)// non-engines can keep burning
                            part.temperature += UnityEngine.Random.Range(0.5f + 0.5f * damage, 1.0f + 0.5f * damage) * (tempRatio * 0.04f * part.maxTemp * TimeWarp.fixedDeltaTime);
                        
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
                            foreach (ParticleEmitter fx in ablationFX.fxEmitters)
                            {
                                fx.gameObject.SetActive(true);
                                fx.gameObject.transform.LookAt(part.transform.position + vessel.srf_velocity);
                                fx.gameObject.transform.Rotate(90, 0, 0);
                            }
                            foreach (ParticleEmitter fx in ablationSmokeFX.fxEmitters)
                            {
                                fx.gameObject.SetActive(vessel.atmDensity > 0.02);
                                fx.gameObject.transform.LookAt(part.transform.position + vessel.srf_velocity);
                                fx.gameObject.transform.Rotate(90, 0, 0);
                            }
                            float severity = (float)((this.part.maxTemp * 0.85) / this.part.maxTemp);
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

        public double _GetBodyArea(ModularFlightIntegrator.PartThermalData ptd)
        {
            Part p = ptd.part;
            if (p.DragCubes.None)
                return 0d;
            Vector3 bodyLocal = p.partTransform.InverseTransformDirection(-vessel.upAxis);
            return p.DragCubes.GetCubeAreaDir(bodyLocal) * ptd.bodyAreaMultiplier;
        }
        
        public double _GetSunArea(ModularFlightIntegrator fi, ModularFlightIntegrator.PartThermalData ptd)
        {
            Part p = ptd.part;
            if (p.DragCubes.None)
                return 0d;
            Vector3 localSun = p.partTransform.InverseTransformDirection(FI.sunVector);
            return p.DragCubes.GetCubeAreaDir(localSun) * ptd.sunAreaMultiplier;
        }
        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.ModuleAeroReentry] " + msg);
        }
    }

    class ModuleHeatShield : ModuleAeroReentry
    {
        [KSPField]
        public string ablativeResource = "";
        
        /// <summary>
        /// The "scale height" for ablator loss
        /// </summary>
        [KSPField]
        public double lossExp = 0d;
        
        /// <summary>
        /// Constant to tune ablator loss
        /// </summary>
        [KSPField]
        public double lossConst = 1d;
        
        /// <summary>
        /// Factor to the ablator's specific heat to use for the pyrolysis flux
        /// </summary>
        [KSPField]
        public double  pyrolysisLossFactor = 10d;
        
        /// <summary>
        /// Minimum temperature for ablation to start
        /// </summary>
        [KSPField]
        public double ablationTempThresh = 600d;
        
        /// <summary>
        /// When our bottom is unoccluded, assume reentry and lower conductivity
        /// </summary>
        [KSPField]
        public double reentryConductivity = 0.01d;
        
        
        // public fields
        public PartResource ablative = null; // pointer to the PartResource
        public double pyrolysisLoss; // actual per-tonne flux
        public double origConductivity; // we'll store the part's original conductivity here
        public int downDir = (int)DragCube.DragFace.YN;
        public double density = 1d;
        public double invDensity = 1d;
        
        [KSPField(guiActive = true, guiName ="Ablation: ", guiUnits = " kg/sec", guiFormat = "N5")]
        double loss = 0d;
        
        [KSPField(guiActive = true, guiName = "Pyrolysis Flux: ", guiUnits = " kW", guiFormat = "F4")]
        double flux = 0d;
        
        public override void Start()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            
            base.Start ();
            if (ablativeResource != null && ablativeResource != "")
            {
                if (part.Resources.Contains(ablativeResource) && lossExp < 0)
                {
                    ablative = part.Resources[ablativeResource];
                    pyrolysisLoss = pyrolysisLossFactor * ablative.info.specificHeatCapacity;
                    density = ablative.info.density;
                    invDensity = 1d / density;
                }
            }
            origConductivity = part.heatConductivity;
        }
        
        public override void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
                return;
            
            // shouldn't matter for this...
            //if (FI.IsAnalytical || FI.RecreateThermalGraph)
            //  return;
            
            
            base.FixedUpdate ();
            flux = 0d;
            loss = 0d;
            
            // Set conductivity based on whether stuff is occluding our bottom. If not, then we assume
            // we shouldn't conduct.
            if (part.DragCubes.AreaOccluded[downDir] > 0.1d * part.DragCubes.WeightedArea[downDir])
                part.heatConductivity = reentryConductivity;
            else
                part.heatConductivity = origConductivity;
            
            
            if ((object)ablative != null && skinTemperature > ablationTempThresh)
            {
                double ablativeAmount = ablative.amount;
                if (ablativeAmount > 0d)
                {
                    loss = lossConst * Math.Exp(lossExp / skinTemperature);
                    if (loss > 0d)
                    {
                        loss *= ablativeAmount;
                        ablative.amount -= loss * TimeWarp.fixedDeltaTime;
                        loss *= density;
                        flux = pyrolysisLoss * loss;
                        loss *= 1000.0;

                        skinTemperature = Math.Max (skinTemperature - (flux * skinThermalMassReciprocal * (double)TimeWarp.fixedDeltaTime), PhysicsGlobals.SpaceTemperature);
                    }                    
                }
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, false)] // fixed
	public class FixMaxTemps : MonoBehaviour
	{
        //public PartModule RFEngineConfig = null;
        //public FieldInfo[] RFEConfigs = null;
        
        public void Start()
		{
            if (!CompatibilityChecker.IsAllCompatible())
                return;
            Debug.Log("FixMaxTemps: Fixing Temps");
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS"))
            {
                if(node.HasValue("name") && node.GetValue("name") == "Default" && node.HasValue("ridiculousMaxTemp"))
                {
					double maxTemp;
					double scale = 0.5f;
					if(node.HasValue ("maxTempScale"))
						double.TryParse(node.GetValue("maxTempScale"), out scale);
					if(scale > 0 && double.TryParse(node.GetValue("ridiculousMaxTemp"), out maxTemp))
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
                                        double oldTemp = part.partPrefab.maxTemp;
                                        bool changed = false;
                                        if (part.partPrefab.maxTemp > maxTemp)
                                        {
                                            part.partPrefab.maxTemp = Math.Min(part.partPrefab.maxTemp * scale, maxTemp);
                                            changed = true;
                                        }
                                        if (changed)
                                        {
                                            double curScale = part.partPrefab.maxTemp / oldTemp;

                                            foreach (ModuleEngines module in part.partPrefab.Modules.OfType<ModuleEngines>())
                                            {
                                                module.heatProduction *= (float)curScale;
                                            }
                                            foreach (ModuleEnginesFX module in part.partPrefab.Modules.OfType<ModuleEnginesFX>())
                                            {
                                                module.heatProduction *= (float)curScale;
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
        

        public static float gToleranceMult = 6.0f;

        public static bool debugging = false;
    }
}