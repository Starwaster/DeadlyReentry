using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;

namespace RealHeat
{
    public class ModuleRealHeat : PartModule
    {
        UIPartActionWindow _myWindow = null;
        UIPartActionWindow myWindow
        {
            get
            {
                if (_myWindow == null)
                {
                    foreach (UIPartActionWindow window in FindObjectsOfType(typeof(UIPartActionWindow)))
                    {
                        if (window.part == part) _myWindow = window;
                    }
                }
                return _myWindow;
            }
        }


        [KSPField(isPersistant = false, guiActive = false, guiName = "Shockwave", guiUnits = "", guiFormat = "G")]
        public string displayShockwave;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Ambient", guiUnits = "", guiFormat = "G")]
        public string displayAmbient;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Temperature", guiUnits = "C", guiFormat = "F0")]
        public float displayTemperature;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Flux In", guiUnits = "kW/m^2", guiFormat = "N3")]
        public float displayFluxIn;

        [KSPField(isPersistant = false, guiActive = false, guiName = "Flux Out", guiUnits = "kW/m^2", guiFormat = "N3")]
        public float displayFluxOut;

        [KSPField(isPersistant = true)]
        public float adjustCollider = 0;

        [KSPField(isPersistant = true)]
        public float leeConst = 0f; // amount of localShockwave used for radiation for lee-facing area


        [KSPField(isPersistant = false)]
        public bool hasShield = false;

        [KSPField(isPersistant = true)]
        public float shieldMass = 0f; // Allows one part to be both pod and shield.

        [KSPField(isPersistant = true)]
        public float shieldHeatCapacity = 1f;

        [KSPField(isPersistant = true)]
        public float shieldEmissiveConst = 0f;

        [KSPField(isPersistant = false)]
        public float shieldArea = 0f;

        [KSPField(isPersistant = true)]
        public int deployAnimationController; // for deployable shields

        [KSPField(isPersistant = true)]
        public FloatCurve loss = new FloatCurve();

        [KSPField(isPersistant = true)]
        public FloatCurve dissipation = new FloatCurve();

        [KSPField(isPersistant = false, guiActive = false, guiName = "angle", guiUnits = " ", guiFormat = "F3")]
        public float dot; // -1....1 = facing opposite direction....facing same direction as airflow

        [KSPField(isPersistant = true)]
        public Vector3 direction;

        [KSPField(isPersistant = true)]
        public float reflective;

        [KSPField(isPersistant = true)]
        public string ablative;

        [KSPField(isPersistant = true)]
        public float lossExp = -1;

        [KSPField(isPersistant = true)]
        public float lossConst = 1.0f;

        [KSPField(isPersistant = true)]
        public float pyrolysisLoss = -1;

        [KSPField(isPersistant = true)]
        public float ablationTempThresh = 573.15f; // temperature below which ablation is ignored (K)

        [KSPField(isPersistant = true)]
        public float heatCapacity = 480f; // in J/g-K, use default for stainless steel

        [KSPField(isPersistant = true)]
        public float emissiveConst = 0; // coefficient for emission

        // per-frame shared members
        protected double counter = 0; // for initial delay
		protected double deltaTime = 0; // seconds since last FixedUpdate
        public double ambient = 0; // ambient temperature (K)
        public double density = 1.225; // ambient density (kg/m^3)
        protected bool inAtmo = false;
        public double shockwave; // shockwave temperature outside all shielding (K)
        protected double adjustedAmbient; // shockwave temperature experienced by the part(K)
        protected double Cp; // specific heat
        protected double Cd = 0.2; // Drag coefficient
        public double S = 2; // surface area (m^2)
        protected double ballisticCoeff = 600; // kg/m^2
        protected double mass = 1; // mass this frame (tonnnes)
        protected double temperature = 0; // part tempterature this frame (K)
        protected Vector3 velocity; // velocity vector in local reference space (m/s)
        protected float speed; // velocity magnitude (m/s)
        protected double fluxIn = 0; // heat flux in, kW/m^2
        protected double fluxOut = 0; // heat flux out, kW/m^2
        protected float temperatureDelta = 0f; // change in temperature (K)
        protected double frontalArea = 1;
        protected double leeArea = 1;

        public const double CTOK = 273.15; // convert Celsius to Kelvin
        public const double SIGMA = 5.670373e-8; // Stefan–Boltzmann constant
        public const double AIREMISS = 0.3;
        public const double MASSEPSILON = 1e-20; // small value to offset mass calcs just in case

        public double SOLARLUM = 3.8e+26; // can't be const, since it depends on what Kerbin's SMA is.
        public const double SOLARCONST = 1370;


        public float heatConductivity = 0.0f;


        [KSPField]
        private bool is_debugging = false;

        // Interaction
        private PartModule FARPartModule = null;
        private bool hasFAR = false;
        FieldInfo fiCd = null;
        FieldInfo fiS = null;

        public Dictionary<string, double> nodeArea;

        public override string GetInfo()
        {
            string s;
            if (hasShield)
            {
                s = "Active Heat Shield";
                if (direction.x != 0 || direction.y != 0 || direction.z != 0)
                    s += " (directional)";
            }
            else
                s = "Heat by RealHeat";
            return s;
        }

        public override void OnAwake()
        {
            base.OnAwake();
			FARPartModule = null;
            nodeArea = new Dictionary<string, double>();
        }

        public override void OnStart(StartState state)
        {
            part.heatDissipation = part.heatConductivity = 0f;
            counter = 0;
            if (state == StartState.Editor)
                return;
            if (myWindow != null)
                myWindow.displayDirty = true;
            // moved part detection logic to OnAWake
            // exception: FAR.
            if (part.Modules.Contains("FARBasicDragModel"))
            {
                FARPartModule = part.Modules["FARBasicDragModel"];
                hasFAR = true;
                fiCd = FARPartModule.GetType().GetField("Cd");
                fiS = FARPartModule.GetType().GetField("SPlusAttachArea");
            }
            else if (part.Modules.Contains("FARWingAerodynamicModel"))
            {
                FARPartModule = part.Modules["FARWingAerodynamicModel"];
                hasFAR = true;
                fiCd = FARPartModule.GetType().GetField("Cd");
                fiS = FARPartModule.GetType().GetField("S");
            }

            if (ablative == null)
                ablative = "None";

            // calculate Solar luminosity
            // FIXME: get actual distance from sun.
            if(FlightGlobals.Bodies[1].referenceBody == FlightGlobals.Bodies[0])
                SOLARLUM = SOLARCONST * Math.Pow(FlightGlobals.Bodies[1].orbit.semiMajorAxis, 2) * 4 * Math.PI;
            else
                SOLARLUM = SOLARCONST * Math.Pow(FlightGlobals.Bodies[1].referenceBody.orbit.semiMajorAxis, 2) * 4 * Math.PI;
        }

        private bool GetShieldedStateFromFAR()
        {
            // Check if this part is shielded by fairings/cargobays according to FAR's information...

            if ((object)FARPartModule != null)
            {
                //Debug.Log("[RHC] Part has FAR module.");
                try
                {
                    FieldInfo fi = FARPartModule.GetType().GetField("isShielded");
                    bool isShieldedFromFAR = ((bool)(fi.GetValue(FARPartModule)));
                    //Debug.Log("[RHC] Found FAR isShielded: " + isShieldedFromFAR.ToString());
                    return isShieldedFromFAR;
                }
                catch (Exception e)
                {
                    Debug.Log("[RHC]: " + e.Message);
                    return false;
                }
            }
            else
            {
                //Debug.Log("[RHC] No FAR module.");
                return false;
            }
        }

        public bool IsShielded(Vector3 direction)
		{   
            Ray ray = new Ray(part.transform.position - direction.normalized * (1.0f+adjustCollider), direction.normalized);
			RaycastHit[] hits = Physics.RaycastAll (ray, 10);
			foreach (RaycastHit hit in hits) {
				if(hit.rigidbody != null && hit.collider != part.collider) {
					return true;
				}
			}
			return false;
		}

        public void CalculateParameters()
        {
            inAtmo = false;
            if (vessel.staticPressure > 0)
            {
                inAtmo = true;
                shockwave = (double)RealHeatUtils.baseTempCurve.EvaluateTempDiffCurve(speed);
                Cp = RealHeatUtils.baseTempCurve.EvaluateVelCpCurve(speed); // FIXME should be based on adjustedAmbient
                frontalArea = S * Cd;
                leeArea = S - frontalArea;
            }
            else
            {
                shockwave = 0;
                Cp = 1.4;
                frontalArea = S;
                leeArea = 0;
            }
            if (GetShieldedStateFromFAR())
                adjustedAmbient = part.temperature + CTOK; // FIXME: Change to the fairing part's temperature
            else
                if (IsShielded(velocity))
                    adjustedAmbient = ambient + shockwave * leeConst;
                else
                    adjustedAmbient = shockwave + ambient;
            fluxIn = 0;
        }

        public float CalculateTemperatureDelta()
        {
            double flux = fluxIn - fluxOut;
            double multiplier = (mass - shieldMass) * heatCapacity + shieldMass * shieldHeatCapacity;
            multiplier *= 1000; // convert J/gK to kJ/tK
            return (float)(flux / multiplier);
        }

        public void FixedUpdate()
        {
            if ((object)vessel == null || (object)vessel.flightIntegrator == null)
                return;

            if (is_debugging != RealHeatUtils.debugging)
            {
                is_debugging = RealHeatUtils.debugging;
                Fields["displayShockwave"].guiActive = RealHeatUtils.debugging;
                Fields["displayAmbient"].guiActive = RealHeatUtils.debugging;
                Fields["displayFluxIn"].guiActive = RealHeatUtils.debugging;
                Fields["displayFluxOut"].guiActive = RealHeatUtils.debugging;
            }

            deltaTime = TimeWarp.fixedDeltaTime;
            velocity = part.vessel.orbit.GetVel() - part.vessel.mainBody.getRFrmVel(part.vessel.vesselTransform.position);
            //(rb.velocity + ReentryPhysics.frameVelocity);
            speed = velocity.magnitude;
            ambient = vessel.flightIntegrator.getExternalTemperature() + CTOK;
            temperature = part.temperature + CTOK;
            density = RealHeatUtils.CalculateDensity(vessel.mainBody, vessel.staticPressure, ambient);

            // calculate ballistic coefficient for root part, grab from it if other part
            if (part == vessel.rootPart)
            {
                double sumArea = 0;
                double sumMass = 0;
                foreach (Part p in vessel.Parts)
                {
                    foreach (ModuleRealHeat m in p.Modules.OfType<ModuleRealHeat>())
                        sumArea += m.S * m.Cd;
                    sumMass += part.mass + part.GetResourceMass();
                }
                ballisticCoeff = (sumMass + MASSEPSILON) * 1000 / sumArea;
            }
            else
            {
                ModuleRealHeat m = (ModuleRealHeat)vessel.rootPart.Modules["ModuleRealHeat"];
                if ((object)m != null)
                    ballisticCoeff = m.ballisticCoeff;
            }

            // get mass for thermal calculations
            if (shieldMass <= 0)
            {
                if (part.rb != null)
                    mass = part.rb.mass;
                mass = Math.Max(part.mass, mass) + MASSEPSILON;
            }
            else
                mass = shieldMass + MASSEPSILON;

            // get Cd and surface area from FAR if we can, else use crazy stock stuff
            if (hasFAR)
            {
                try
                {
                    Cd = ((double)(fiCd.GetValue(FARPartModule)));
                    S = ((double)(fiS.GetValue(FARPartModule)));
                }
                catch (Exception e)
                {
                    Debug.Log("[RH]: error getting drag area" + e.Message);
                    S = mass * 8;
                    Cd = 0.2;
                }
            }
            else
            {
                S = mass * 8;
                Cd = 0.2;
            }

            // if too soon, abort.
            if (counter < 5.0)
            {
                counter += deltaTime;
                return;
            }

            CalculateParameters();

            ManageHeatConduction();
            ManageHeatConvection(velocity);
            ManageHeatRadiation();
            ManageSolarHeat();

            fluxIn *= 0.001 * deltaTime; // convert to kW then to the amount of time passed
            fluxOut *= 0.001 * deltaTime; // convert to kW then to the amount of time passed

            temperatureDelta = CalculateTemperatureDelta();
            part.temperature += temperatureDelta;
            
            if (part.temperature < -253) // clamp to 20K
                part.temperature = -253;

            displayFluxIn = (float)fluxIn;
            displayFluxOut = (float)fluxOut;
            displayAmbient = ambient.ToString("F0") + "C";
            displayTemperature = part.temperature;
        }

        public void HeatExchange(Part p)
        {
            //FIXME: This is just KSP's stock heat system.
            /*float sqrMagnitude = (this.part.transform.position - p.transform.position).sqrMagnitude;
            if (sqrMagnitude < 25f)
            {
                float num = part.temperature * this.heatConductivity * Time.deltaTime * (1f - sqrMagnitude / 25f);
                p.temperature += num;
                part.temperature -= num;
            }*/
            // do nothing, since it's all handled by other stuff
        }

        public void ManageHeatConduction()
        {
            /***
             * this isn't quite realistic.
             * We're essentially modelling a part as a series of tubes.
             * Each attachNode is connected to the part's Center of Mass
             * by a solid cylinder with a diameter equal to the attachNode's
             * size (0 = 0.625m, 1 = 1.25m, 2 = 2.5m, etc.); heat flows through
             * each of those cylinders to equalize with part.Temperature,
             * which is the temperature at the part's CoM.
             * It's not very precise, but it's better than stock.
             * 
             ***/
            if (part.heatConductivity > 0f)
            { // take over heat management from KSP
                heatConductivity = part.heatConductivity;
                part.heatConductivity = 0f;
            }
            else if (heatConductivity == 0f)
                return;

            float accumulatedExchange = 0f;
            string logLine = "Part: " + part.name + " (temp " + part.temperature.ToString() + " / conductivity " + heatConductivity.ToString() + ")";
            List<Part> partsToProcess = new List<Part>(part.children);
            if (part.parent != null)
                partsToProcess.Add(part.parent);

            foreach (AttachNode node in part.attachNodes)
            {
                float radius2 = node.size * node.size;
                if (node.size == 0)
                    radius2 = 0.25f;
                logLine += ("\n +-Node: " + node.id + " [" + node.size + "m] ");
                float cFactor = radius2 * heatConductivity;
                if (part.transform != null)
                {
                    if (!nodeArea.ContainsKey(node.id))
                        nodeArea.Add(node.id, part.temperature);
                    logLine += " temp " + nodeArea[node.id];

                    float d = 1f + (part.transform.position - node.position).magnitude;
                    float exchange = cFactor * (part.temperature - (float)nodeArea[node.id]) / d;
                    accumulatedExchange -= exchange;
                    nodeArea[node.id] += exchange;

                    Part p = node.attachedPart;
                    if (p != null && p.isAttached && part.isAttached)
                    {
                        logLine += " - " + p.name;
                        partsToProcess.Remove(p);
                        AttachNode otherNode = p.findAttachNodeByPart(part);
                        if (otherNode == null)
                        {   // TODO: Find the nearest two nodes and compute the average temperature.
                            // for now, we'll just exchange directly with the part's CoM.
                            float cFactor2 = radius2 * TimeWarp.fixedDeltaTime;
                            float deltaT = ((float)nodeArea[node.id] - p.temperature);
                            nodeArea[node.id] += deltaT * cFactor2 * heatConductivity * 4f;
                            p.temperature -= deltaT * cFactor2 * heatConductivity * 4f;
                        }
                        else
                        {
                            logLine += " (Node: " + otherNode.id + " + [" + otherNode.size + "m]) ";
                            ModuleRealHeat heatModule = (ModuleRealHeat)p.Modules["ModuleRealHeat"];
                            if (heatModule == null)
                            {
                                // something has gone VERY wrong.
                                Debug.Log("   !!! NO HEAT MODULE");
                            }
                            else if (heatModule.heatConductivity > 0f)
                            {
                                if (!heatModule.nodeArea.ContainsKey(otherNode.id))
                                    heatModule.nodeArea.Add(otherNode.id, p.temperature);
                                if (otherNode.size < node.size)
                                {
                                    radius2 = otherNode.size * otherNode.size;
                                    if (otherNode.size == 0)
                                        radius2 = 0.25f;
                                }
                                float cFactor2 = radius2 * TimeWarp.fixedDeltaTime;

                                float deltaT = ((float)heatModule.nodeArea[otherNode.id] - (float)nodeArea[node.id]);

                                nodeArea[node.id] += deltaT * cFactor2 * (heatConductivity + heatModule.heatConductivity);
                                heatModule.nodeArea[otherNode.id] -= deltaT * cFactor2 * (heatConductivity + heatModule.heatConductivity);
                                logLine += "flow: " + (deltaT * cFactor2).ToString();
                            }
                        }
                    }
                }
                Debug.Log(logLine + "\n");
            }

            fluxIn = accumulatedExchange;

            foreach (Part p in partsToProcess)
            {
                if (p.isAttached)
                {
                    //HeatExchange(p);
                }
            }
        }

        public void ManageHeatConvection(Vector3 velocity)
        {
            if (inAtmo)
            {
                // convective heating in atmosphere
                double baseFlux = RealHeatUtils.heatMultiplier * Cp * Math.Sqrt(speed) * Math.Sqrt(density);
                fluxIn += baseFlux * frontalArea * (adjustedAmbient - part.temperature);
                fluxIn += baseFlux * leeArea * (ambient + (adjustedAmbient - ambient) * leeConst) - part.temperature;
            }
            //Debug.Log("Part: " + part.name + "Convection; Flux out: " + fluxOut + " Flux in: " + fluxIn);
        }

        public void ManageSolarHeat()
        {
            double distance = (Planetarium.fetch.Sun.transform.position - vessel.transform.position).sqrMagnitude;
            double retval = 1.0;
            if (inAtmo)
                retval *= 1 - (density * 0.31020408163265306122448979591837); // 7-900W at sea level     this factor is 0.38 / 1.225 to achieve that power from radiation
            retval *= SOLARLUM / (4 * Math.PI * distance);
            fluxIn += S * 0.5 * retval;
            //Debug.Log("Part: " + part.name + "Solar; Flux out: " + fluxOut + " Flux in: " + fluxIn);
        }

        public void ManageHeatRadiation()
        {
            double temperatureVal = 0;
            if (inAtmo)
            {
                // radiant heating in atmosphere
                temperatureVal = adjustedAmbient;
                temperatureVal *= temperatureVal;
                temperatureVal *= temperatureVal; //Doing it this way results in temp^4 very quickly

                fluxIn += frontalArea * temperatureVal * AIREMISS * SIGMA;

                temperatureVal = (ambient + (adjustedAmbient - ambient) * leeConst);
                temperatureVal *= temperatureVal;
                temperatureVal *= temperatureVal; //Doing it this way results in temp^4 very quickly

                fluxIn += leeArea * temperatureVal * AIREMISS * SIGMA;
            }
            // radiant cooling

            temperatureVal = temperature;
            temperatureVal *= temperatureVal;
            temperatureVal *= temperatureVal; //Doing it this way results in temp^4 very quickly

            fluxOut += (S - shieldArea) * temperatureVal * emissiveConst * SIGMA;
            if (hasShield)
                fluxOut += shieldArea * temperatureVal * shieldEmissiveConst * SIGMA;

            //Debug.Log("Part: " + part.name + "Radiation; Flux out: " + fluxOut + " Flux in: " + fluxIn);
        }

        public void ManageHeatAblation()
        {
            if (part.Resources.Contains(ablative) && lossExp > 0 && temperature > ablationTempThresh)
            {
                if (direction.magnitude == 0) // an empty vector means the shielding exists on all sides
                    dot = 1;
                else // check the angle between the shock front and the shield
                {
                    dot = Vector3.Dot(velocity.normalized, part.transform.TransformDirection(direction).normalized);
                    if (dot < 0f)
                        dot = 0f;
                }
                double ablativeAmount = part.Resources[ablative].amount;
                double loss = (double)lossConst * Math.Pow(dot, 0.25) * Math.Exp(-lossExp / temperature);
                loss *= ablativeAmount;
                part.Resources[ablative].amount -= loss * deltaTime;
                fluxOut += pyrolysisLoss * loss;
            }
        }

    }
}
