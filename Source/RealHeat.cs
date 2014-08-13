using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using KSP;

namespace RealHeat
{
    public class ModuleHeat : PartModule
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

		[KSPField(isPersistant = false)]
        public float area = -1f;

        [KSPField(isPersistant = true)]
        public float heatMass = -1f; // mass to use for heating calculations. Allows one part to be both pod and shield.

        [KSPField(isPersistant = true)]
        public float heatCapacity = 1f; // in J/g-K

        [KSPField(isPersistant = true)]
        public float emissiveConst = 0; // coefficient for emission

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
        public float ablationTempThresh = 300f;

        // per-frame shared members
        protected double counter = 0; // for initial delay
		protected double deltaTime = 0; // seconds since last FixedUpdate
        protected double ambient = 0; // ambient temperature (C)
        protected double density = 1.225; // ambient density (kg/m^3)
        protected double shockwave; // shockwave temperature (C)
        protected double Cp; // specific heat
        protected double Cd; // Drag coefficient
        protected double S; // surface area
        protected Vector3 velocity; // velocity vector in local reference space (m/s)
        protected double speed; // velocity magnitude (m/s)
        protected double fluxIn = 0; // heat flux in, kW/m^2
        protected double fluxOut = 0; // heat flux out, kW/m^2

        public const double CTOK = 273.15; // convert Celsius to Kelvin
        public const double SIGMA = 5.670373e-8; // Stefan–Boltzmann constant


        public float heatConductivity = 0.0f;


        [KSPField]
        private bool is_debugging = false;
        private ModuleParachute parachute = null;
        private PartModule realChute = null;
        private Type rCType = null;
		private PartModule FARPartModule = null;

        public Dictionary<string, double> nodeArea;

        public override void OnAwake()
        {
            base.OnAwake();
            if (part && part.Modules != null) // thanks, FlowerChild!
            {
                if (part.Modules.Contains("ModuleParachute"))
                    parachute = (ModuleParachute)part.Modules["ModuleParachute"];
                if (part.Modules.Contains("RealChuteModule"))
                {
                    realChute = part.Modules["RealChuteModule"];
                    rCType = realChute.GetType();
                }
				FARPartModule = null;

                nodeArea = new Dictionary<string, double>();

            }
        }

        public override void OnStart(StartState state)
        {
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
            }
            else if (part.Modules.Contains("FARWingAerodynamicModel"))
            {
                    FARPartModule = part.Modules["FARWingAerodynamicModel"];
            }
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
            if (GetShieldedStateFromFAR() == true)
            	return true;
            
            Ray ray = new Ray(part.transform.position - direction.normalized * (1.0f+adjustCollider), direction.normalized);
			RaycastHit[] hits = Physics.RaycastAll (ray, 10);
			foreach (RaycastHit hit in hits) {
				if(hit.rigidbody != null && hit.collider != part.collider) {
					return true;
				}
			}
			return false;
		}

        public virtual double CalculateFluxOut()
        {
            return 0;
        }

        public virtual double CalculateFluxIn()
        {
            return 0;
        }

        public double CalculateTemperatureDelta()
        {
            return 0;
        }

        public void FixedUpdate()
        {
            Rigidbody rb = part.Rigidbody;
            if (rb == null || part.physicalSignificance == Part.PhysicalSignificance.NONE)
            {
                if (is_debugging != RealHeatUtils.debugging)
                {
                    is_debugging = RealHeatUtils.debugging;
                    Fields["displayShockwave"].guiActive = RealHeatUtils.debugging;
					Fields["displayAmbient"].guiActive = RealHeatUtils.debugging;
                	Fields["displayFluxIn"].guiActive = RealHeatUtils.debugging;
                	Fields["displayFluxOut"].guiActive = RealHeatUtils.debugging;
                    Fields["displayGForce"].guiActive = RealHeatUtils.debugging;
                    Fields["gExperienced"].guiActive = RealHeatUtils.debugging;
                }

                velocity = (rb.velocity + RealHeatUtils.frameVelocity);

            }

            ManageHeatConduction();
            ManageHeatConvection(velocity);
            ManageHeatRadiation();

            displayTemperature = part.temperature;
        }

        public void HeatExchange(Part p)
        {
            //FIXME: This is just KSP's stock heat system.
            float sqrMagnitude = (this.part.transform.position - p.transform.position).sqrMagnitude;
            if (sqrMagnitude < 25f)
            {
                float num = part.temperature * this.heatConductivity * Time.deltaTime * (1f - sqrMagnitude / 25f);
                p.temperature += num;
                part.temperature -= num;
            }
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
                float cFactor = radius2 * heatConductivity * Time.deltaTime;
                if (part.transform != null)
                {
                    if (!nodeArea.ContainsKey(node.id))
                        nodeArea.Add(node.id, part.temperature);
                    logLine += " temp " + nodeArea[node.id];

                    float d = 1f + (part.transform.position - node.position).magnitude;
                    float exchange = cFactor * (part.temperature - nodeArea[node.id]) / d;
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
                            float cFactor2 = radius2 * Time.deltaTime;
                            float deltaT = (nodeArea[node.id] - p.temperature);
                            nodeArea[node.id] += deltaT * cFactor2 * heatConductivity * 4f;
                            p.temperature -= deltaT * cFactor2 * heatConductivity * 4f;
                        }
                        else
                        {
                            logLine += " (Node: " + otherNode.id + " + [" + otherNode.size + "m]) ";
                            ModuleHeatSystem heatModule = (ModuleHeatSystem)p.Modules["ModuleHeatSystem"];
                            if (heatModule == null)
                            {
                                // something has gone VERY wrong.
                                Debug.Log("   !!! NO HEAT MODULE");
                            }
                            else if (heatModule.heatConductivity > 0f)
                            {
                                if (!heatModule.nodeTemperature.ContainsKey(otherNode.id))
                                    heatModule.nodeTemperature.Add(otherNode.id, p.temperature);
                                if (otherNode.size < node.size)
                                {
                                    radius2 = otherNode.size * otherNode.size;
                                    if (otherNode.size == 0)
                                        radius2 = 0.25f;
                                }
                                float cFactor2 = radius2 * Time.deltaTime;

                                float deltaT = (heatModule.nodeTemperature[otherNode.id] - nodeArea[node.id]);

                                nodeArea[node.id] += deltaT * cFactor2 * (heatConductivity + heatModule.heatConductivity);
                                heatModule.nodeTemperature[otherNode.id] -= deltaT * cFactor2 * (heatConductivity + heatModule.heatConductivity);
                                logLine += "flow: " + (deltaT * cFactor2).ToString();
                            }
                        }
                    }
                }
                Debug.Log(logLine + "\n");
            }

            part.temperature += accumulatedExchange;

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
            part.temperature += ReentryHeat(velocity);

            //FIXME: This is just KSP's stock heat system.
            //part.temperature -= (this.heatDissipation * Time.deltaTime);
        }

        public void ManageHeatRadiation()
        {
        }

    }

    public class ModuleHeatShield : ModuleHeat
    {
        [KSPField(isPersistant = true)]
        public int deployAnimationController; // for deployable shields

        [KSPField(isPersistant = true)]
        public FloatCurve loss = new FloatCurve();

        [KSPField(isPersistant = true)]
        public FloatCurve dissipation = new FloatCurve();
    }
}
