using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace DeadlyReentry
{
    public static class DREAtmDataOrganizer
    {
        public static Dictionary<string, DREAtmosphericGasSpecies> idOrganizedListOfGasSpecies;
        public static Dictionary<CelestialBody, DREAtmosphereComposition> bodyOrganizedListOfAtmospheres;
        static ConfigNode FARAeroData = null;
        static bool FARFound = false;

        public static void GetFARNode()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("FARAeroData"))
                FARAeroData = node;

            FARFound = true;
        }

        public static void LoadConfigNodes()
        {
            idOrganizedListOfGasSpecies = new Dictionary<string, DREAtmosphericGasSpecies>();
            bodyOrganizedListOfAtmospheres = new Dictionary<CelestialBody, DREAtmosphereComposition>();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("DRE_ATM_GAS_SPECIES"))
            {
                foreach (ConfigNode gasSpeciesNode in node.GetNodes("GAS_SPECIES"))
                {
                    string id = gasSpeciesNode.GetValue("id");
                    DREAtmosphericGasSpecies newSpecies = new DREAtmosphericGasSpecies(id);

                    idOrganizedListOfGasSpecies.Add(id, newSpecies);
                }
            }
            foreach (KeyValuePair<string, DREAtmosphericGasSpecies> pair in idOrganizedListOfGasSpecies)
                pair.Value.Initialize();

            DREAtmosphereComposition defaultOxygenatedRocky = new DREAtmosphereComposition(), 
                defaultUnoxygenatedRocky = new DREAtmosphereComposition(), 
                defaultGasGiant = new DREAtmosphereComposition();

            float gasGiantRadius = 3000;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("DRE_ATM_COMPOSITIONS"))
            {
                foreach (ConfigNode atmNode in node.GetNodes("ATM_COMPOSITION"))
                {
                    DREAtmosphereComposition newComposition = new DREAtmosphereComposition();

                    newComposition.gasSpeciesAndMassFractions = new Dictionary<DREAtmosphericGasSpecies, float>();

                    foreach (ConfigNode gasSpeciesNode in atmNode.GetNodes("GAS_SPECIES"))
                    {
                        DREAtmosphericGasSpecies decompositionSpecies = DREAtmDataOrganizer.idOrganizedListOfGasSpecies[gasSpeciesNode.GetValue("id")];

                        float massFraction = float.Parse(gasSpeciesNode.GetValue("massFraction"));
                        newComposition.gasSpeciesAndMassFractions.Add(decompositionSpecies, massFraction);
                    }

                    newComposition.referenceTemperature = float.Parse(atmNode.GetValue("referenceTemperature"));
                    newComposition.maxSimVelocity = float.Parse(atmNode.GetValue("maxSimVelocity"));

                    string bodyName = atmNode.GetValue("bodyName");
                    if (!FARFound)
                        GetFARNode();

                    for(int idx = 0; idx < FlightGlobals.Bodies.Count; idx++)
                    {
                        CelestialBody body = FlightGlobals.Bodies[idx];
                        if(body.name == bodyName)
                        {
                            bool found = false;
                            if ((object)FARAeroData != null)
                            {
                                foreach(ConfigNode bodyNode in FARAeroData.nodes)
                                {
                                    if(int.Parse(bodyNode.GetValue("index")) == idx)
                                    {
                                        found = true;
                                        float ftmp;
                                        if(bodyNode.HasValue("specHeatRatio"))
                                        {
                                            float.TryParse(bodyNode.GetValue("specHeatRatio"), out ftmp);
                                            newComposition.specHeatRatio = ftmp;
                                        }
                                        if (bodyNode.HasValue("gasMolecularWeight"))
                                        {
                                            float.TryParse(bodyNode.GetValue("gasMolecularWeight"), out ftmp);
                                            newComposition.gasConstant = (float)((double)(DREAtmosphericGasSpecies.UniversalGasConstant) / (double)(ftmp));
                                        }
                                    }
                                }
                            }
                            if (!found)
                            {
                                double weight = 0f;
                                double gamma = 0;
                                foreach (KeyValuePair<DREAtmosphericGasSpecies, float> kvp in newComposition.gasSpeciesAndMassFractions)
                                {
                                    weight += kvp.Key.GetMolecularMass() * kvp.Value;
                                    double Cp = kvp.Key.CalculateCp(newComposition.referenceTemperature);
                                    gamma += (Cp / (Cp - (double)kvp.Key.GetSpecificGasConstant())) * (double)kvp.Value;
                                }
                                newComposition.gasConstant = (float)((double)(DREAtmosphericGasSpecies.UniversalGasConstant) / weight);
                                newComposition.specHeatRatio = (float)gamma;

                            }
                            bodyOrganizedListOfAtmospheres.Add(body, newComposition);
                            break;
                        }
                    }

                    if (bodyName == "defaultOxygenatedRocky")
                        defaultOxygenatedRocky = newComposition;
                    else if (bodyName == "defaultUnoxygenatedRocky")
                        defaultUnoxygenatedRocky = newComposition;
                    else if (bodyName == "defaultGasGiant")
                        defaultGasGiant = newComposition;
                }
                gasGiantRadius = float.Parse(node.GetValue("gasGiantRadius"));
            }

            foreach(CelestialBody body in FlightGlobals.Bodies)
            {
                if(!bodyOrganizedListOfAtmospheres.ContainsKey(body))
                {
                    if (body.Radius > gasGiantRadius)
                        bodyOrganizedListOfAtmospheres.Add(body, defaultGasGiant);
                    else if(body.atmosphereContainsOxygen)
                        bodyOrganizedListOfAtmospheres.Add(body, defaultOxygenatedRocky);
                    else
                        bodyOrganizedListOfAtmospheres.Add(body, defaultUnoxygenatedRocky);
                }
            }
        }

        public static void CalculateNewTemperatureCurve(object o)
        {
            try
            {
                tempCurveDataContainer container = (tempCurveDataContainer)o;
                DREAtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[container.body];
                //Debug.Log("Beginning Temperature Curve Calculation");
                container.callingCurve.protoTempCurve = atmosphere.TemperatureAsFunctionOfVelocity(100, 5, atmosphere.maxSimVelocity);
                container.callingCurve.referenceTemp = GetReferenceTemp(container.body);
                if (container.dumpToText)
                    container.callingCurve.DumpToText(5, container.body);
            }
            catch (Exception e)
            {
                Debug.LogError("DRE Exception in Temperature Curve Calculation: " + e.StackTrace);
            }
        }

        public static float GetReferenceTemp(CelestialBody body)
        {
            DREAtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];

            return atmosphere.referenceTemperature;
        }

        public static float GetSpecHeatRatio(CelestialBody body)
        {
            if ((object)body == null)
                return 0f;
            if (bodyOrganizedListOfAtmospheres.ContainsKey(body))
            {
                DREAtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];
                return atmosphere.specHeatRatio;
            }
            return 1.4f;
        }
        public static float GetGasConstant(CelestialBody body)
        {
            if ((object)body == null)
                return 0f;
            if (bodyOrganizedListOfAtmospheres.ContainsKey(body))
            {
                DREAtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];
                return atmosphere.gasConstant;
            }
            return 287.103f;
        }
    }

    public class DREAtmosphereComposition
    {
        public Dictionary<DREAtmosphericGasSpecies, float> gasSpeciesAndMassFractions;
        public float referenceTemperature;
        public float maxSimVelocity;
        public float specHeatRatio;
        public float gasConstant;

        public CurveData[] TemperatureAsFunctionOfVelocity(int stepsBetweenCurvePoints, float dVForIntegration, float maxVel)
        {
            List<CurveData> tempVsVelCurve = new List<CurveData>();

            Dictionary<DREAtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions = CreateWorkingGasSpeciesAndMassFractionDict();

            float temp = referenceTemperature;
            float velocity = 0;

            //StringBuilder debug = new StringBuilder();

            while (velocity < maxVel)
            {
                int i = 0;
                UpdateCompositionDueToDecomposition(workingGasSpeciesAndMassFractions, temp);
                float Cp = CalculateCp(workingGasSpeciesAndMassFractions, temp);
                float dCp_dt = Calculate_dCp_dt(workingGasSpeciesAndMassFractions, temp);
                float energyChange = CalculateEnergyLostThroughDecomposition(workingGasSpeciesAndMassFractions, temp);

                float dT_dV = Cp + dCp_dt * temp + energyChange;
                dT_dV = velocity / dT_dV;

                if (i <= stepsBetweenCurvePoints)
                    tempVsVelCurve.Add(new CurveData(velocity, temp - referenceTemperature, dT_dV));

                i++;
                temp += dT_dV * dVForIntegration;
                velocity += dVForIntegration;

                //debug.AppendLine("Cp: " + Cp + " dCp_dt: " + dCp_dt + " energyChange: " + energyChange + " vel: " + velocity + " temp: " + temp + " dT_dV: " + dT_dV);
            }
            //Debug.Log(debug.ToString());

            return tempVsVelCurve.ToArray();
        }

        #region dT_dV internal functions
        private float CalculateCp(Dictionary<DREAtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            float Cp = 0;

            foreach (KeyValuePair<DREAtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
                Cp += pair.Key.CalculateCp(temp) * pair.Key.CalculatePhi(temp) * pair.Value[0];

            return Cp;
        }

        private float Calculate_dCp_dt(Dictionary<DREAtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            float dCp_dt = 0;

            foreach (KeyValuePair<DREAtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
                dCp_dt += (pair.Key.Calculate_dCp_dT(temp) * pair.Key.CalculatePhi(temp) + pair.Key.Calculate_dPhi_dT(temp) * pair.Key.CalculateCp(temp)) * pair.Value[0];

            return dCp_dt;
        }

        private float CalculateEnergyLostThroughDecomposition(Dictionary<DREAtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            float energy = 0;

            foreach (KeyValuePair<DREAtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
            {
                float heatOfFormationDecompositionSpecies = 0;
                foreach (KeyValuePair<DREAtmosphericGasSpecies, float> pairDecomposition in pair.Key.decompositionSpeciesWithFraction)
                {
                    heatOfFormationDecompositionSpecies += pairDecomposition.Key.GetHeatOfFormation() * pairDecomposition.Value;
                }

                energy += pair.Value[1] * (pair.Key.GetHeatOfFormation() - heatOfFormationDecompositionSpecies) * pair.Key.Calculate_dPhi_dT(temp);
            }

            return energy;

        }

        private void UpdateCompositionDueToDecomposition(Dictionary<DREAtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
            {
                KeyValuePair<DREAtmosphericGasSpecies, float[]> pair = workingGasSpeciesAndMassFractions.ElementAt(i);
                float currentPhi = pair.Value[0];

                float tempDerivedPhi = pair.Key.CalculatePhi(temp) * pair.Value[0];
                float phiDiff = tempDerivedPhi - currentPhi;

                workingGasSpeciesAndMassFractions[pair.Key] = new float[] {tempDerivedPhi, pair.Value[1]};
                for (int j = 0; j < workingGasSpeciesAndMassFractions.Count; j++)
                {
                    KeyValuePair<DREAtmosphericGasSpecies, float[]> DecompPair = workingGasSpeciesAndMassFractions.ElementAt(j);
                    if(pair.Key.decompositionSpeciesWithFraction.ContainsKey(DecompPair.Key))
                    {
                        workingGasSpeciesAndMassFractions[DecompPair.Key] = new float[]{DecompPair.Value[0] - phiDiff * pair.Key.decompositionSpeciesWithFraction[DecompPair.Key], DecompPair.Value[1]};
                    }
                }
            }
        }

        #endregion

        #region Setup Functions
        private Dictionary<DREAtmosphericGasSpecies, float[]> CreateWorkingGasSpeciesAndMassFractionDict()
        {
            Dictionary<DREAtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions = new Dictionary<DREAtmosphericGasSpecies, float[]>();

            //First, copy over the data from the default atmosphere
            foreach (KeyValuePair<DREAtmosphericGasSpecies, float> pair in gasSpeciesAndMassFractions)
            {
                workingGasSpeciesAndMassFractions.Add(pair.Key, new float[]{pair.Value, pair.Value});
            }
            //Then, go through each value and add its decomposition species; continue until no more items can be added to the dictionary
            int lastCount = workingGasSpeciesAndMassFractions.Count;
            do
            {
                for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
                {
                    KeyValuePair<DREAtmosphericGasSpecies, float[]> pair = workingGasSpeciesAndMassFractions.ElementAt(i);

                    foreach (KeyValuePair<DREAtmosphericGasSpecies, float> decompositionSpecies in pair.Key.decompositionSpeciesWithFraction)
                    {
                        if (!workingGasSpeciesAndMassFractions.ContainsKey(decompositionSpecies.Key))
                            workingGasSpeciesAndMassFractions.Add(decompositionSpecies.Key, new float[]{0, 0});
                    }
                }

                lastCount = workingGasSpeciesAndMassFractions.Count;
            } while (workingGasSpeciesAndMassFractions.Count > lastCount);

            //Set max concentrations
            for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
            {
                KeyValuePair<DREAtmosphericGasSpecies, float[]> pair = workingGasSpeciesAndMassFractions.ElementAt(i);

                foreach (KeyValuePair<DREAtmosphericGasSpecies, float> decompositionSpecies in pair.Key.decompositionSpeciesWithFraction)
                {
                    float[] tmp = workingGasSpeciesAndMassFractions[decompositionSpecies.Key];
                    tmp[1] += decompositionSpecies.Value * pair.Value[1];
                    workingGasSpeciesAndMassFractions[decompositionSpecies.Key] = tmp;
                }
            }          

            StringBuilder debug = new StringBuilder();
            //debug.AppendLine("Gas Species Initialized");
            //foreach(KeyValuePair<DREAtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
            //{
            //    debug.AppendLine(pair.Key.id + " Mass Fraction: " + pair.Value[0]);
            //}
            //Debug.Log(debug.ToString());

            return workingGasSpeciesAndMassFractions;
        }
        #endregion
    }

    public class DREAtmosphericGasSpecies
    {
        public string id;

        //Number of degrees of freedom in this gas species at high and low temperatures; used to determine Cp
        private int degFreedomLowTemp;
        private int degFreedomHighTemp;

        //Temperature at which this gas reaches the low and high degFreedoms in K
        private float tempLowDegFreedom;
        private float tempHighDegFreedom;

        //Energy (in J/kg) needed to form this compound
        private float heatOfFormation;

        //Species that this gas decomposes into at high temperatures
        public Dictionary<DREAtmosphericGasSpecies, float> decompositionSpeciesWithFraction;

        //Temperatures that this gas begins to decompose at and temperature that it ends at, in K
        private float tempBeginDecomposition;
        private float tempEndDecomposition;

        //Specific heats at constant pressure, based on degFreedom
        private float CpLowTemp;
        private float CpHighTemp;

        private float[] constantsCpCurve = new float[4];
        private float[] constantsDecompositionCurve = new float[4];

        //Specific gas constant, Universal Gas Constant / Molecular Mass
        private float specificGasConstant;

        public const float UniversalGasConstant = 8314.5f;

        public DREAtmosphericGasSpecies(string thisId)
        {
            id = thisId;
        }

        public void Initialize()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("DRE_ATM_GAS_SPECIES"))
            {
                foreach(ConfigNode gasSpeciesNode in node.GetNodes("GAS_SPECIES"))
                {
                    if (gasSpeciesNode.GetValue("id") == this.id)
                    {
                        Debug.Log("Loading '" + id + "' data");
                        ConfigNode thisNode = gasSpeciesNode;

                        degFreedomLowTemp = int.Parse(thisNode.GetValue("degFreedomLowTemp"));
                        degFreedomHighTemp = int.Parse(thisNode.GetValue("degFreedomHighTemp"));

                        tempLowDegFreedom = float.Parse(thisNode.GetValue("tempLowDegFreedom"));
                        tempHighDegFreedom = float.Parse(thisNode.GetValue("tempHighDegFreedom"));


                        tempBeginDecomposition = float.Parse(thisNode.GetValue("tempBeginDecomposition"));
                        tempEndDecomposition = float.Parse(thisNode.GetValue("tempEndDecomposition"));

                        float molecularMass = float.Parse(thisNode.GetValue("molecularMass"));
                        specificGasConstant = UniversalGasConstant / molecularMass;

                        heatOfFormation = float.Parse(thisNode.GetValue("heatOfFormation")) / molecularMass * 1000000;

                        decompositionSpeciesWithFraction = new Dictionary<DREAtmosphericGasSpecies, float>();
                        foreach(ConfigNode decompositionGasSpeciesNode in thisNode.GetNodes("DECOMPOSITION_SPECIES"))
                        {
                            DREAtmosphericGasSpecies decompositionSpecies = DREAtmDataOrganizer.idOrganizedListOfGasSpecies[decompositionGasSpeciesNode.GetValue("id")];
                            float massFraction = float.Parse(decompositionGasSpeciesNode.GetValue("massFraction"));

                            decompositionSpeciesWithFraction.Add(decompositionSpecies, massFraction);
                        }

                        break;
                    }
                }
            }

            CpLowTemp = ((float)degFreedomLowTemp * 0.5f + 1f) * specificGasConstant;
            CpHighTemp = ((float)degFreedomHighTemp * 0.5f + 1f) * specificGasConstant;

            float tmp = tempLowDegFreedom - tempHighDegFreedom;
            tmp = tmp * tmp * tmp;
            tmp = 1 / tmp;

            tmp *= (CpHighTemp - CpLowTemp);

            constantsCpCurve[0] = 2f * tmp;
            constantsCpCurve[1] = -3f * (tempLowDegFreedom + tempHighDegFreedom) * tmp;
            constantsCpCurve[2] = 6f * tempLowDegFreedom * tempHighDegFreedom * tmp;
            constantsCpCurve[3] = tempLowDegFreedom * tempLowDegFreedom * (tempLowDegFreedom - 3f * tempHighDegFreedom) * tmp + CpLowTemp;


            tmp = tempBeginDecomposition - tempEndDecomposition;
            tmp = tmp * tmp * tmp;
            tmp = 1 / tmp;

            constantsDecompositionCurve[0] = -2f * tmp;
            constantsDecompositionCurve[1] = 3f * (tempBeginDecomposition + tempEndDecomposition) * tmp;
            constantsDecompositionCurve[2] = -6f * tempBeginDecomposition * tempEndDecomposition * tmp;
            constantsDecompositionCurve[3] = -tempBeginDecomposition * tempBeginDecomposition * (tempBeginDecomposition - 3f * tempEndDecomposition) * tmp + 1f;

            
            //Debug.Log("Initialized Gas Species '" + id + "'\n\r");
        }

        public float CalculateCp(float temp)
        {
            if (temp <= tempLowDegFreedom)
                return CpLowTemp;
            else if (temp >= tempHighDegFreedom)
                return CpHighTemp;
            else
            {
                float Cp = constantsCpCurve[0] * temp;
                Cp += constantsCpCurve[1];
                Cp *= temp;
                Cp += constantsCpCurve[2];
                Cp *= temp;
                Cp += constantsCpCurve[3];
                return Cp;
            }
        }

        public float Calculate_dCp_dT(float temp)
        {
            if (temp <= tempLowDegFreedom)
                return 0f;
            else if (temp >= tempHighDegFreedom)
                return 0f;
            else
            {
                float dCp_dT = 3f * constantsCpCurve[0] * temp;
                dCp_dT += 2f * constantsCpCurve[1];
                dCp_dT *= temp;
                dCp_dT += constantsCpCurve[2];
                return dCp_dT;
            }
        }

        public float CalculatePhi(float temp)
        {
            if (temp <= tempBeginDecomposition)
                return 1f;
            else if (temp >= tempEndDecomposition)
                return 0f;
            else
            {
                float Phi = constantsDecompositionCurve[0] * temp;
                Phi += constantsDecompositionCurve[1];
                Phi *= temp;
                Phi += constantsDecompositionCurve[2];
                Phi *= temp;
                Phi += constantsDecompositionCurve[3];
                return Phi;
            }
        }

        public float Calculate_dPhi_dT(float temp)
        {
            if (temp <= tempBeginDecomposition)
                return 0f;
            else if (temp >= tempEndDecomposition)
                return 0f;
            else
            {
                float dPhi_dT = 3f * constantsDecompositionCurve[0] * temp;
                dPhi_dT += 2f * constantsDecompositionCurve[1];
                dPhi_dT *= temp;
                dPhi_dT += constantsDecompositionCurve[2];
                return dPhi_dT;
            }
        }

        public float GetHeatOfFormation()
        {
            return heatOfFormation;
        }

        public float GetSpecificGasConstant()
        {
            return specificGasConstant;
        }

        public float GetMolecularMass()
        {
            return UniversalGasConstant / specificGasConstant;
        }
    }
    public struct CurveData
    {
        public float x;
        public float y;
        public float dy_dx;

        public CurveData(float x, float y, float dy_dx)
        {
            this.x = x;
            this.y = y;
            this.dy_dx = dy_dx;
        }
    }

    public class tempCurveDataContainer
    {
        public tempCurveDataContainer(CelestialBody body, DREAtmTempCurve callingCurve, bool dumpToText)
        {
            this.body = body;
            this.callingCurve = callingCurve;
            this.dumpToText = dumpToText;
        }
        public CelestialBody body;
        public DREAtmTempCurve callingCurve;
        public bool dumpToText;
    }

}
