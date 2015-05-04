using System;

//using System.Collections.Generic;
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

        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Temp.", guiUnits = "K",   guiFormat = "x.00")]
        public string skinTemperatureDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Rad. Area", guiUnits = "m2",   guiFormat = "x.00")]
        public string RadiativeAreaDisplay;
        
        [KSPField(isPersistant = false, guiActive = false, guiName = "Skin Rad.", guiUnits = "K",   guiFormat = "x.00")]
        public string skinThermalRadiationFluxDisplay;
        
        public ModularFlightIntegrator fi;
        public ModularFlightIntegrator.PartThermalData ptd;

        public virtual void Start()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            
            
            fi = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
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
                fi = vessel.gameObject.GetComponent<ModularFlightIntegrator>();
        }

        public virtual void FixedUpdate()
        {
            if (!FlightGlobals.ready)
                return;
            
            // Can't look this up right now; trying to declare MFI in a field is smashing the assembly
             if (fi.IsAnalytical || fi.RecreateThermalGraph)
                 return;
            
            if (skinTemperature == -1.0 && !Double.IsNaN(vessel.externalTemperature))
                skinTemperature = vessel.externalTemperature;   
            
            if (PhysicsGlobals.ThermalDataDisplay)
            {
                skinTemperatureDisplay = skinTemperature.ToString ("F4");
                RadiativeAreaDisplay = part.radiativeArea.ToString ("F4");
            }

            StartCoroutine (UpdateSkinThermals());
        }

        public IEnumerator UpdateSkinThermals()
        {
            yield return new WaitForFixedUpdate();
            
            ptd = fi.PartThermalDataList.Where(p => p.part == part).FirstOrDefault();
            
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
        }

        public void UpdateConvection()
        {
            // get sub/transonic convection
            double convectionArea = UtilMath.Lerp(
                part.radiativeArea,
                part.exposedArea,
                PhysicsGlobals.FullConvectionAreaMin + (part.machNumber - PhysicsGlobals.FullToCrossSectionLerpStart) / (PhysicsGlobals.FullToCrossSectionLerpEnd - PhysicsGlobals.FullToCrossSectionLerpStart));
            
            double convectiveFlux = (part.externalTemperature - skinTemperature) * fi.convectiveCoefficient * convectionArea;
            
            // get mach convection
            // defaults to starting at M=2 and being full at M=3
            double machLerp = (part.machNumber - PhysicsGlobals.MachConvectionStart) / (PhysicsGlobals.MachConvectionEnd - PhysicsGlobals.MachConvectionStart);
            
            if (machLerp > 0d)
            {
                machLerp = Math.Min(1d, Math.Pow(machLerp, PhysicsGlobals.MachConvectionExponent));
                
                // get flux
                double machHeatingFlux = convectionArea * fi.convectiveMachFlux;
                convectiveFlux = UtilMath.LerpUnclamped(convectiveFlux, machHeatingFlux, machLerp);
                
                // get steady-state radiative temperature for this flux. Assume 0.5 emissivity, because while part emissivity might be higher,
                // radiative area will be way higher than convective area, so this will be safe.
                // We multiply in the convective area multiplier because that accounts for both how much area is occluded, but also modifiers
                // to the shock temperature.
                double machExtTemp = Math.Pow(0.5d * fi.convectiveMachFlux * ptd.convectionAreaMultiplier * part.heatConvectiveConstant
                                              / (PhysicsGlobals.StefanBoltzmanConstant * PhysicsGlobals.RadiationFactor), 1d / PhysicsGlobals.PartEmissivityExponent);
                part.externalTemperature = Math.Max(part.externalTemperature, UtilMath.LerpUnclamped(part.externalTemperature, machExtTemp, machLerp));
            }
            convectiveFlux *= 0.001d * part.heatConvectiveConstant * ptd.convectionAreaMultiplier; // W to kW, scalars
            part.thermalConvectionFlux = convectiveFlux;
            skinTemperature = Math.Max((skinTemperature + convectiveFlux * part.thermalMassReciprocal * TimeWarp.fixedDeltaTime), PhysicsGlobals.SpaceTemperature);
        }
        /*
        public void UpdateConvection ()
        {
            // get sub/transonic convection
            double convectionArea = UtilMath.Lerp(part.radiativeArea, part.exposedArea,
                                                  (part.machNumber - PhysicsGlobals.FullToCrossSectionLerpStart) / (PhysicsGlobals.FullToCrossSectionLerpEnd - PhysicsGlobals.FullToCrossSectionLerpStart));
            
            double convectiveFlux = (part.externalTemperature - skinTemperature) * fi.convectiveCoefficient * convectionArea;
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
                sunFlux = _GetSunArea(fi, ptd) * scalar * fi.solarFlux * fi.solarFluxMultiplier;
                tempTemp += sunFlux * finalScalar;
                //print("Temp + sunFlux = " + tempTemp.ToString("F4"));
            }
            double bodyFlux = fi.bodyEmissiveFlux + fi.bodyAlbedoFlux;
            if (bodyFlux > 0d)
            {
                tempTemp += UtilMath.Lerp(0.0, bodyFlux, fi.DensityThermalLerp) * _GetBodyArea(ptd) * scalar * finalScalar;
                //print("Temp + bodyFlux = " + tempTemp.ToString("F4"));
            }
            
            // Radiative flux = S-Bconst*e*A * (T^4 - radInT^4)

            //part.temperature = Math.Max(tempTemp, PhysicsGlobals.SpaceTemperature);
            double backgroundRadiationTemp = UtilMath.Lerp(fi.atmosphericTemperature, PhysicsGlobals.SpaceTemperature, fi.DensityThermalLerp);
            
            double radOut = -(Math.Pow(tempTemp, PhysicsGlobals.PartEmissivityExponent)
                - Math.Pow (fi.backgroundRadiationTemp, PhysicsGlobals.PartEmissivityExponent)) // Using modified background radiation with no reentry radiant flux
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
                                            * skinHeatConductivity
                                            * part.heatConductivity
                                            * part.radiativeArea)); // should be contact area... how large a value should we use?
            
            double kilowatts = energyTransferred * fi.WarpReciprocal;
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



        public UIPartActionWindow _myWindow = null; 
        public UIPartActionWindow myWindow 
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

        public virtual void Update()
        {
            if (is_debugging != PhysicsGlobals.ThermalDataDisplay)
            {
                is_debugging = PhysicsGlobals.ThermalDataDisplay;
                Fields["skinTemperatureDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                Fields["RadiativeAreaDisplay"].guiActive = PhysicsGlobals.ThermalDataDisplay;
                if (myWindow != null)
                {
                    myWindow.displayDirty = true;
                }
            }
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
            Vector3 localSun = p.partTransform.InverseTransformDirection(fi.sunVector);
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
            
            //base.Start ();
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
            //if (fi.IsAnalytical || fi.RecreateThermalGraph)
            //  return;
            
            
            //base.FixedUpdate ();
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
                        loss *= 1000d * density;
                        flux = pyrolysisLoss * loss;
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
					float scale = 0.5f;
					if(node.HasValue ("maxTempScale"))
						float.TryParse(node.GetValue("maxTempScale"), out scale);
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
}
