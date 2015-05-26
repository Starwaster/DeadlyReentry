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
        [KSPField]
        public bool leaveTemp = false;

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
        public double skinThicknessFactor = 0.01;
        
        [KSPField(isPersistant = false)]
        public double skinHeatConductivity = 0.0012;

        [KSPField(isPersistant = false)]
        public double skinUnexposedTempFraction = 0.35;
        
        [KSPField(isPersistant = false)]
        public double skinThermalMass = -1.0;
        
        public double skinThermalMassReciprocal;
        private double thermalMassMult;
        
        public bool is_debugging;
        
        // Debug Displays
        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Temp.", guiUnits = " K",   guiFormat = "x.00")]
        public string skinTemperatureDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Thermal Mass.", guiUnits = "",   guiFormat = "x.00")]
        public string skinThermalMassDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Rad. Area", guiUnits = " m2",   guiFormat = "x.00")]
        public string RadiativeAreaDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Exp. Area", guiUnits = " m2",   guiFormat = "x.00")]
        public string ExposedAreaDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Flux /Area", guiUnits = " kW/m2",   guiFormat = "x.00")]
        public string convFluxAreaDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Rad Flux /Area", guiUnits = " kW/m2", guiFormat = "x.00")]
        public string radFluxInAreaDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Cond", guiUnits = " W/m2",   guiFormat = "x.00")]
        public string skinCondFluxAreaDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Acceleration", guiUnits = " G",   guiFormat = "F3")]
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
                if (fi == null)
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

        private double convectionArea;
        
        
        EventData<GameEvents.ExplosionReaction> ReentryReaction = GameEvents.onPartExplode;
        
        UIPartActionWindow _myWindow = null; 
        UIPartActionWindow myWindow 
        {
            get {
                if(_myWindow == null)
                {
                    UIPartActionWindow[] windows = FindObjectsOfType<UIPartActionWindow>();
                    for(int i = windows.Length - 1; i >= 0; --i)
                    {
                        if (windows[i].part == part)
                        {
                            _myWindow = windows[i];
                            break;
                        }
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

            // are we an engine?
            for(int i = part.Modules.Count - 1; i >= 0; --i)
                if (part.Modules[i] is ModuleEngines)
                {
                    is_engine = true;
                    break;
                }
        }

        public virtual void Start()
        {
            if (!isCompatible)
                return;
            //counter = 0;
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            
            
            FI = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
            if (skinThermalMassModifier == -1.0)
                skinThermalMassModifier = part.thermalMassModifier;
            
            if (skinMaxTemp == -1.0)
                skinMaxTemp = part.maxTemp;
            
            // only one of skinThermalMassModifier and skinThicknessFactor should be configured
            if (skinThermalMass == -1.0)
                skinThermalMass = Math.Max((double)part.mass, 0.001D) * PhysicsGlobals.StandardSpecificHeatCapacity * skinThermalMassModifier * skinThicknessFactor;
            skinThermalMassReciprocal = 1.0 / Math.Max (skinThermalMass, 0.001);
            GameEvents.onVesselWasModified.Add(OnVesselWasModified);

            // edit part thermal mass modifier so we subtract out skin thermal mass
            if (part.partInfo != null && part.partInfo.partPrefab != null)
            {
                if (part.thermalMassModifier == part.partInfo.partPrefab.thermalMassModifier)
                {
                    double baseTM = (double)part.mass * PhysicsGlobals.StandardSpecificHeatCapacity * part.thermalMassModifier;
                    part.thermalMassModifier *= (baseTM - skinThermalMass) / baseTM;
                }
            }
            //print(part.name + " Flight Integrator ID = " + FI.GetInstanceID().ToString());
        }

        void OnDestroy()
        {
            FI = null;
            if(_ablationFX != null && _ablationFX.audio != null)
                DestroyImmediate(_ablationFX.audio);
            if(_gForceFX != null && _gForceFX.audio != null)
                DestroyImmediate(_gForceFX.audio);
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
            if (FI == null)
            {
                print("FlightIntegrator null. Trying to retrieve correct FI");
                FI = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
            }
            
            
            // HACK skipping an update on IsAnalytical is a quick hack. Need to handle Analytical Mode properly
            if (FI.IsAnalytical || FI.RecreateThermalGraph)
                return;
            
            if (skinTemperature == -1.0 && !Double.IsNaN(vessel.externalTemperature))
            {
                //skinTemperature = vessel.externalTemperature;
                skinTemperature = part.temperature;
                //print(part.name + " skinTemperature initializing = " + part.temperature.ToString() + " (part.temperature)");
                //print(" vessel external temperature reads at " + vessel.externalTemperature.ToString() + "K");
                //print("Uninitialized skinTemperature initialized.");
            }
            //print("Starting UpdateSkinThermals() coroutine.");
            StartCoroutine (UpdateSkinThermals());
        }
        
        public IEnumerator UpdateSkinThermals()
        {
            yield return new WaitForFixedUpdate();
            
            ptd = FI.PartThermalDataList.Where(p => p.part == part).FirstOrDefault();
            if ((object)ptd != null)
            {
                float newExp, newRad, newTot;
                CalculateAreas(out newRad, out newExp, out newTot);
                part.exposedArea = newExp / newRad * part.radiativeArea;
                if (double.IsNaN(part.exposedArea))
                    part.exposedArea = 0d;
                thermalMassMult = newRad / newTot;
                if (double.IsNaN(thermalMassMult))
                    thermalMassMult = 1.0d;
                convectionArea = part.radiativeArea;

                //print(part.name + " PartThermalData HashCode = " + ptd.GetHashCode());              
                if(PhysicsGlobals.ThermalConvectionEnabled && !part.ShieldedFromAirstream)
                    UpdateConvection();
                if(PhysicsGlobals.ThermalRadiationEnabled && !part.ShieldedFromAirstream)
                    UpdateRadiation();
                if(PhysicsGlobals.ThermalConductionEnabled)
                    UpdateSkinConduction();


                if (skinTemperature > skinMaxTemp)
                {
                    //print(part.name + ".skinTemperature = " + skinTemperature.ToString());
                    //print(part.name + ".skinMaxTemp = " + skinMaxTemp.ToString());

                    FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] "
                                              + part.partInfo.title + " burned up from overheating.");
                    
                    if ( part is StrutConnector )
                    {
                        ((StrutConnector)part).BreakJoint();
                    }
                    if (!CheatOptions.IgnoreMaxTemperature)
                        part.explode();
                }
                CheckForFire();
                CheckGeeForces();
                if (PhysicsGlobals.ThermalDataDisplay)
                {
                    skinTemperatureDisplay = skinTemperature.ToString ("F2");
                    skinThermalMassDisplay = skinThermalMass.ToString("F2");
                    RadiativeAreaDisplay = part.radiativeArea.ToString ("F2");
                    ExposedAreaDisplay = part.exposedArea.ToString("F2");
                    convFluxAreaDisplay = (part.thermalConvectionFlux / part.exposedArea).ToString("F4");
                }
            }
            else
            {
                //print(part.name + ": PartThermalData is NULL!");
            }
        }
        public void CalculateAreas(out float radArea, out float exposedArea, out float totalArea)
        {
            exposedArea = 0f;
            radArea = 0f;
            totalArea = 0f;
            bool inAtmo = vessel.atmDensity > 0d;
            for (int i = 0; i < 6; ++i)
            {
                float faceArea = part.DragCubes.AreaOccluded[i];
                radArea += faceArea;
                totalArea += part.DragCubes.WeightedArea[i];
                if (inAtmo)
                {
                    Vector3 faceDirection = DragCubeList.GetFaceDirection((DragCube.DragFace)i);
                    float dot = Vector3.Dot(-part.dragVectorDirLocal, faceDirection);
                    float dotNormalized = (dot + 1f) * 0.5f;
                    float dragMult = PhysicsGlobals.DragCurveValue(dotNormalized, (float)part.machNumber);
                    float CdMult = part.DragCubes.WeightedDrag[i];
                    if (CdMult < 0.01f)
                        CdMult = 1f;
                    if (CdMult <= 1f)
                        CdMult = 1f / CdMult;
                    exposedArea += faceArea * dragMult / PhysicsGlobals.DragCurveMultiplier.Evaluate((float)part.machNumber) * CdMult;
                }
            }
            if (float.IsNaN(exposedArea))
                exposedArea = 0f;
        }

        public void UpdateConvection()
        {
            if (fi == null)
                print(part.name + ": UpdateConvection() Null flight integrator.");
            // get sub/transonic convection
            convectionArea = UtilMath.Lerp(
                part.radiativeArea,
                part.exposedArea,
                PhysicsGlobals.FullConvectionAreaMin + (part.machNumber - PhysicsGlobals.FullToCrossSectionLerpStart) 
                        / (PhysicsGlobals.FullToCrossSectionLerpEnd - PhysicsGlobals.FullToCrossSectionLerpStart));
            
            double convectiveFlux = (part.externalTemperature - skinTemperature) * FI.convectiveCoefficient * convectionArea;
            
            // get mach convection
            // defaults to starting at M=2 and being full at M=3
            double machLerp = (part.machNumber - PhysicsGlobals.MachConvectionStart) / (PhysicsGlobals.MachConvectionEnd - PhysicsGlobals.MachConvectionStart);
            if (machLerp > 0d)
            {
                if (machLerp < 1d)
                    machLerp = Math.Pow(machLerp, PhysicsGlobals.MachConvectionExponent);
                else
                    machLerp = 1d;
                
                // get flux
                double machHeatingFlux = convectionArea * FI.convectiveMachFlux * ReentryPhysics.machMultiplier;
                convectiveFlux = UtilMath.LerpUnclamped(convectiveFlux, machHeatingFlux, machLerp);
            }
            convectiveFlux *= 0.001d * part.heatConvectiveConstant * ptd.convectionAreaMultiplier; // W to kW, scalars
            part.thermalConvectionFlux = convectiveFlux;
            //print(part + ": convectiveFlux = " + convectiveFlux.ToString("F4") + ", skinThermalMassReciprocal " + skinThermalMassReciprocal.ToString("F4"));
            skinTemperature = Math.Max((skinTemperature + convectiveFlux * skinThermalMassReciprocal * thermalMassMult * TimeWarp.fixedDeltaTime), PhysicsGlobals.SpaceTemperature);
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
            double finalScalar = skinThermalMassReciprocal * thermalMassMult * (double)TimeWarp.fixedDeltaTime;
            double sunFlux = 0d;
            double exposedTemp = skinTemperature;
            // TODO This needs to track two skin temperatures: exposed skinTemperature and unexposed skinTemperature
            double restTemp = Math.Max(part.temperature, skinTemperature * skinUnexposedTempFraction); // assume non-exposed area is at the part's temp.
            double exposedMult = convectionArea / part.radiativeArea;
            if (double.IsNaN(exposedMult))
                exposedMult = 1d;
            double restMult = 1d - exposedMult;
            
            if (vessel.directSunlight)
            {
                // assume half the surface area is under sunlight
                sunFlux = _GetSunArea(FI, ptd) * scalar * FI.solarFlux * FI.solarFluxMultiplier;
                double num = sunFlux * finalScalar;
                exposedTemp += num * exposedMult;
                restTemp += num * restMult;
                //print("Temp + sunFlux = " + tempTemp.ToString("F4"));
            }
            double bodyFlux = FI.bodyEmissiveFlux + FI.bodyAlbedoFlux;
            if (bodyFlux > 0d)
            {
                double num = UtilMath.Lerp(0.0, bodyFlux, FI.DensityThermalLerp) * _GetBodyArea(ptd) * scalar * finalScalar;
                exposedTemp += num * exposedMult;
                restTemp += num * restMult;
                //print("Temp + bodyFlux = " + tempTemp.ToString("F4"));
            }
            
            // Radiative flux = S-Bconst*e*A * (T^4 - radInT^4)
            
            // get background radiation temperatures
            double lowLevelRadiationTemp = UtilMath.Lerp(FI.atmosphericTemperature, PhysicsGlobals.SpaceTemperature, FI.DensityThermalLerp);
            
            double exposedRadiationTemp = FI.backgroundRadiationTemp;
            // recalculate radiation temp from dynamic density
            if(vessel.mach > 1d)
            {
                double M2 = vessel.mach;
                M2 *= M2;
                double dynDensity = (vessel.mainBody.atmosphereAdiabaticIndex + 1d) * M2 / (2d + (vessel.mainBody.atmosphereAdiabaticIndex - 1d) * M2) * vessel.atmDensity;
                double dyndensityThermalLerp = 1d - dynDensity;
                if (dyndensityThermalLerp < 0.5d)
                {
                    dyndensityThermalLerp = 0.25d / dynDensity;
                }
                exposedRadiationTemp = UtilMath.Lerp(FI.externalTemperature, PhysicsGlobals.SpaceTemperature, dyndensityThermalLerp);
            }
            double radIn = Math.Pow(exposedRadiationTemp, PhysicsGlobals.PartEmissivityExponent) * PhysicsGlobals.StefanBoltzmanConstant * scalar;
            radFluxInAreaDisplay = (radIn / convectionArea).ToString("N4");

            double exposedRad = -(Math.Pow(exposedTemp, PhysicsGlobals.PartEmissivityExponent) * PhysicsGlobals.StefanBoltzmanConstant * scalar
                              - radIn)
                              * convectionArea;

            double restRad = -(Math.Pow(restTemp, PhysicsGlobals.PartEmissivityExponent)
                              - Math.Pow(lowLevelRadiationTemp, PhysicsGlobals.PartEmissivityExponent))
                * PhysicsGlobals.StefanBoltzmanConstant * scalar * (part.radiativeArea - convectionArea);

            exposedTemp += (exposedRad + restRad) * finalScalar;
            //print("Temp + radOut =" + tempTemp.ToString("F4"));
            part.thermalRadiationFlux = exposedRad + restRad + sunFlux;
            
            skinTemperature = Math.Max(exposedTemp, PhysicsGlobals.SpaceTemperature);
        }
        
        public void UpdateSkinConduction()
        {
            double timeConductionFactor = PhysicsGlobals.ConductionFactor * Time.fixedDeltaTime;
            double temperatureDelta = skinTemperature - part.temperature;
            double conductArea = part.radiativeArea; // FIXME: should it be sum of weightedarea, since skin conducts even for joined parts?
            if (convectionArea < conductArea && conductArea > 0d)
            {
                double exposedFrac = convectionArea / conductArea;
                double unexposedFrac = 1d - exposedFrac;
                temperatureDelta = temperatureDelta * exposedFrac +
                    (Math.Max(part.temperature, skinTemperature * skinUnexposedTempFraction) - part.temperature) * unexposedFrac;
            }
            double energyTransferred =
                temperatureDelta
                    * Math.Min(skinThermalMass * thermalMassMult, part.thermalMass) * 0.5d
                    * UtilMath.Clamp01(timeConductionFactor
                                       * skinHeatConductivity
                                       * conductArea);
            
            double kilowatts = energyTransferred * FI.WarpReciprocal;
            double temperatureLost = energyTransferred * skinThermalMassReciprocal * thermalMassMult;
            double temperatureRecieved = energyTransferred * part.thermalMassReciprocal;
            
            //skinThermalConductionFlux -= kilowatts;
            //part.thermalConductionFlux += kilowatts;
            
            skinTemperature = Math.Max(skinTemperature - temperatureLost, PhysicsGlobals.SpaceTemperature);
            part.AddThermalFlux(kilowatts);
            if (PhysicsGlobals.ThermalDataDisplay)
                skinCondFluxAreaDisplay = (kilowatts/part.radiativeArea).ToString("N4");
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
                Fields["convFluxAreaDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                Fields["radFluxInAreaDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                Fields["skinCondFluxAreaDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
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
                            
                            List<ParticleEmitter> fxs = ablationFX.fxEmitters;
                            for(int i = fxs.Count-1; i >= 0; --i)
                                GameObject.DestroyImmediate(fxs[i].gameObject);
                            fxs = ablationSmokeFX.fxEmitters;
                            for(int i = fxs.Count-1; i >= 0; --i)
                                GameObject.DestroyImmediate(fxs[i].gameObject);

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
                            List<ParticleEmitter> fxs = ablationFX.fxEmitters;
                            ParticleEmitter fx;
                            for (int i = fxs.Count - 1; i >= 0; --i)
                            {
                                fx = fxs[i];
                                fx.gameObject.SetActive(true);
                                fx.gameObject.transform.LookAt(part.transform.position + vessel.srf_velocity);
                                fx.gameObject.transform.Rotate(90, 0, 0);
                            }
                            fxs = ablationSmokeFX.fxEmitters;
                            for (int i = fxs.Count - 1; i >= 0; --i)
                            {
                                fx = fxs[i];
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

                        List<ParticleEmitter> fxs = ablationFX.fxEmitters;
                        for (int i = fxs.Count - 1; i >= 0; --i)
                            fxs[i].gameObject.SetActive(false);
                        fxs = ablationSmokeFX.fxEmitters;
                        for (int i = fxs.Count - 1; i >= 0; --i)
                            fxs[i].gameObject.SetActive(false);
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
        public double  pyrolysisLossFactor = 1000d;
        
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

        [KSPField]
        public double depletedMaxTemp = 1300;

        // Char stuff
        private static int shaderPropertyBurnColor = Shader.PropertyToID("_BurnColor");
        private Renderer[] renderers;
        [KSPField]
        public float charAlpha = 0.8f;
        [KSPField]
        public float charMax = 0.85f;
        [KSPField]
        public float charMin = 0f;
        [KSPField]
        public double charOffset = 0d; // offset to amount and maxamount
        private bool doChar = false;
        
        // public fields
        public PartResource ablative = null; // pointer to the PartResource
        public double pyrolysisLoss; // actual per-tonne flux
        public double origConductivity; // we'll store the part's original conductivity here
        public int downDir = (int)DragCube.DragFace.YN;
        public double density = 1d;
        public double invDensity = 1d;
        
        [KSPField(guiActive = true, guiName ="Ablation: ", guiUnits = " kg/sec")]
        string lossDisplay;
        
        double loss = 0d;
        
        [KSPField(guiActive = true, guiName = "Pyrolysis Flux: ", guiUnits = " kW")]
        string fluxDisplay;
        
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

            // Do we do charring?
            doChar = false;
            renderers = part.FindModelComponents<Renderer>();
            if ((object)ablative != null && renderers != null && renderers.Length > 0 && charMax != charMin)
            {
                try
                {
                    Color color = new Color(charMax, charMax, charMax, charAlpha);
                    for (int i = renderers.Length - 1; i >= 0; --i)
                    {
                        renderers[i].material.SetColor(shaderPropertyBurnColor, color);
                    }
                    doChar = true;
                }
                catch
                {
                    doChar = false;
                }
            }
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

                        skinTemperature = Math.Max(skinTemperature - (flux * skinThermalMassReciprocal * (double)TimeWarp.fixedDeltaTime), PhysicsGlobals.SpaceTemperature);
                    }
                }
                else
                {
                    part.maxTemp = Math.Min(part.maxTemp, depletedMaxTemp);
                    skinMaxTemp = Math.Min(skinMaxTemp, depletedMaxTemp);
                }
            }
            fluxDisplay = flux.ToString("N4");
            lossDisplay = loss.ToString("N4");
            if (doChar)
                UpdateColor();
        }
        private void UpdateColor()
        {
            float ratio = 0f;
            if(ablative.amount > charOffset)
                ratio = (float)((ablative.amount - charOffset) / (ablative.maxAmount - charOffset));
            float delta = charMax - charMin;
            float colorValue = charMin + delta * ratio;
            Color color = new Color(colorValue, colorValue, colorValue, charAlpha);
            for (int i = renderers.Length - 1; i >= 0; --i)
            {
                renderers[i].material.SetColor(shaderPropertyBurnColor, color);
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
                                        if (part.partPrefab.Modules.Contains("ModuleAeroReentry"))
                                        {
                                            if (((ModuleAeroReentry)(part.partPrefab.Modules["ModuleAeroReentry"])).leaveTemp)
                                                continue;
                                        }
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

                                            foreach (PartModule module in part.partPrefab.Modules)
                                            {
                                                if (module is ModuleEngines)
                                                    ((ModuleEngines)module).heatProduction *= (float)curScale;
                                         
 }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    print("Error processing part maxTemp " + part.name);
                                    Debug.Log(e.Message);
                                }
                                try
                                {
                                    if (part.partPrefab != null)
                                    {
                                        bool add = true;
                                        for (int i = part.partPrefab.Modules.Count - 1; i >= 0; --i)
                                        {
                                            // heat shield derives from this, so true for it too
                                            if (part.partPrefab.Modules[i] is ModuleAeroReentry)
                                            {
                                                add = false;
                                                break;
                                            }
                                        }
                                        if (add)
                                            part.partPrefab.AddModule("ModuleAeroReentry");
                                    }
                                }
                                catch (Exception e)
                                {
                                    print("Error adding ModuleAeroReentry to " + part.name);
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

        public static double machMultiplier  = 1.0;
        
        public static bool debugging = false;

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
        }
        // TODO Move all G-Force related settings to static ReentryPhysics settings?
        public static void LoadSettings()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("REENTRY_EFFECTS"))
            {
                if (node.HasValue("name") && node.GetValue("name") == DeadlyReentryScenario.Instance.DifficultyName)
                {
                    if (node.HasValue("machMultiplier"))
                        double.TryParse(node.GetValue("machMultiplier"), out machMultiplier);
                    if (node.HasValue("gToleranceMult"))
                        float.TryParse(node.GetValue("gToleranceMult"), out gToleranceMult);                    
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
                        double.TryParse(node.GetValue("crewGKillChance"), out ModuleAeroReentry.crewGKillChance);
                    
                    
                    if(node.HasValue("debugging"))
                        bool.TryParse (node.GetValue ("debugging"), out debugging);
                    if(node.HasValue("legacyAero"))
                    Debug.Log("[DRE] - debugging = " + debugging.ToString());
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
                    if (node.HasValue("gToleranceMult"))
                        node.SetValue("gToleranceMult", gToleranceMult.ToString());
                    
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
                    break;
                }
            }
            SaveCustomSettings();
        }
        public static void SaveCustomSettings()
        {
            string[] difficultyNames = {"Default"};
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
                        // This is :Final because it represents player choices and must not be overridden by other mods.
                        ConfigNode node = new ConfigNode("@REENTRY_EFFECTS[" + difficulty + "]:Final");
                        
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
                        savenode.AddNode (node);
                        break;
                    }
                }
            }
            savenode.Save (KSPUtil.ApplicationRootPath.Replace ("\\", "/") + "GameData/DeadlyReentry/custom.cfg");
        }
    }
}