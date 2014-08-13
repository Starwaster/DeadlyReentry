using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace RealHeat
{
    public static class AtmDataOrganizer
    {
        public static Dictionary<string, AtmosphericGasSpecies> idOrganizedListOfGasSpecies;
        public static Dictionary<CelestialBody, AtmosphereComposition> bodyOrganizedListOfAtmospheres;
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
            idOrganizedListOfGasSpecies = new Dictionary<string, AtmosphericGasSpecies>();
            bodyOrganizedListOfAtmospheres = new Dictionary<CelestialBody, AtmosphereComposition>();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("DRE_ATM_GAS_SPECIES"))
            {
                foreach (ConfigNode gasSpeciesNode in node.GetNodes("GAS_SPECIES"))
                {
                    string id = gasSpeciesNode.GetValue("id");
                    AtmosphericGasSpecies newSpecies = new AtmosphericGasSpecies(id);

                    idOrganizedListOfGasSpecies.Add(id, newSpecies);
                }
            }
            foreach (KeyValuePair<string, AtmosphericGasSpecies> pair in idOrganizedListOfGasSpecies)
                pair.Value.Initialize();

            AtmosphereComposition defaultOxygenatedRocky = new AtmosphereComposition(), 
                defaultUnoxygenatedRocky = new AtmosphereComposition(), 
                defaultGasGiant = new AtmosphereComposition();

            float gasGiantRadius = 3000;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("DRE_ATM_COMPOSITIONS"))
            {
                foreach (ConfigNode atmNode in node.GetNodes("ATM_COMPOSITION"))
                {
                    AtmosphereComposition newComposition = new AtmosphereComposition();

                    newComposition.gasSpeciesAndMassFractions = new Dictionary<AtmosphericGasSpecies, float>();

                    foreach (ConfigNode gasSpeciesNode in atmNode.GetNodes("GAS_SPECIES"))
                    {
                        AtmosphericGasSpecies decompositionSpecies = AtmDataOrganizer.idOrganizedListOfGasSpecies[gasSpeciesNode.GetValue("id")];

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
                                            newComposition.gasConstant = (float)((double)(AtmosphericGasSpecies.UniversalGasConstant) / (double)(ftmp));
                                        }
                                    }
                                }
                            }
                            if (!found)
                            {
                                double weight = 0f;
                                double gamma = 0;
                                foreach (KeyValuePair<AtmosphericGasSpecies, float> kvp in newComposition.gasSpeciesAndMassFractions)
                                {
                                    weight += kvp.Key.GetMolecularMass() * kvp.Value;
                                    double Cp = kvp.Key.CalculateCp(newComposition.referenceTemperature);
                                    gamma += (Cp / (Cp - (double)kvp.Key.GetSpecificGasConstant())) * (double)kvp.Value;
                                }
                                newComposition.gasConstant = (float)((double)(AtmosphericGasSpecies.UniversalGasConstant) / weight);
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
                AtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[container.body];
                //Debug.Log("Beginning Temperature Curve Calculation");
                Curves result = atmosphere.TemperatureAsFunctionOfVelocity(100, 5, atmosphere.maxSimVelocity);
                container.callingCurve.protoTempCurve = result.temp;
                container.callingCurve.protoVelCpCurve = result.cp;
                container.callingCurve.referenceTemp = GetReferenceTemp(container.body);
                if (container.dumpToText)
                    container.callingCurve.DumpToText(5, container.body);
            }
            catch (Exception e)
            {
                Debug.LogError("DRE Exception in Temperature Curve Calculation: " + e.StackTrace);
            }
            AtmTempCurve.recalculatingCurve = false;
        }

        public static float GetReferenceTemp(CelestialBody body)
        {
            AtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];

            return atmosphere.referenceTemperature;
        }

        public static float GetSpecHeatRatio(CelestialBody body)
        {
            if ((object)body == null)
                return 0f;
            if (bodyOrganizedListOfAtmospheres.ContainsKey(body))
            {
                AtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];
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
                AtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];
                return atmosphere.gasConstant;
            }
            return 287.103f;
        }
    }

    public class AtmosphereComposition
    {
        public Dictionary<AtmosphericGasSpecies, float> gasSpeciesAndMassFractions;
        public float referenceTemperature;
        public float maxSimVelocity;
        public float specHeatRatio;
        public float gasConstant;

        public Curves TemperatureAsFunctionOfVelocity(int stepsBetweenCurvePoints, float dVForIntegration, float maxVel)
        {
            List<CurveData> tempVsVelCurve = new List<CurveData>();
            List<CurveData> velCpCurve = new List<CurveData>();

            Dictionary<AtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions = CreateWorkingGasSpeciesAndMassFractionDict();

            float temp = referenceTemperature;
            float velocity = 0;

            //StringBuilder debug = new StringBuilder();
            //float oldCp = CalculateCp(workingGasSpeciesAndMassFractions, temp);
            while (velocity < maxVel)
            {
                int i = 0;
                UpdateCompositionDueToDecomposition(workingGasSpeciesAndMassFractions, temp);
                float Cp = CalculateCp(workingGasSpeciesAndMassFractions, temp);
                float dCp_dt = Calculate_dCp_dt(workingGasSpeciesAndMassFractions, temp);
                float energyChange = CalculateEnergyLostThroughDecomposition(workingGasSpeciesAndMassFractions, temp);

                float dT_dV = Cp + dCp_dt * temp + energyChange;
                dT_dV = velocity / dT_dV;

                float dCp_dV = dCp_dt * dT_dV;
                //float dCp_dV = (Cp - oldCp) / dVForIntegration;
                if (i <= stepsBetweenCurvePoints)
                {
                    tempVsVelCurve.Add(new CurveData(velocity, temp - referenceTemperature, dT_dV));
                    velCpCurve.Add(new CurveData(velocity, Cp, dCp_dV));
                }

                i++;
                temp += dT_dV * dVForIntegration;
                velocity += dVForIntegration;
                //oldCp = Cp;
                //debug.AppendLine("Cp: " + Cp + " dCp_dt: " + dCp_dt + " energyChange: " + energyChange + " vel: " + velocity + " temp: " + temp + " dT_dV: " + dT_dV);
            }
            //Debug.Log(debug.ToString());
            Curves retval = new Curves();
            retval.temp = tempVsVelCurve.ToArray();
            retval.cp = velCpCurve.ToArray();
            return retval;
        }

        #region dT_dV internal functions
        private float CalculateCp(Dictionary<AtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            float Cp = 0;

            foreach (KeyValuePair<AtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
                Cp += pair.Key.CalculateCp(temp) * pair.Key.CalculatePhi(temp) * pair.Value[0];

            return Cp;
        }

        private float Calculate_dCp_dt(Dictionary<AtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            float dCp_dt = 0;

            foreach (KeyValuePair<AtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
                dCp_dt += (pair.Key.Calculate_dCp_dT(temp) * pair.Key.CalculatePhi(temp) + pair.Key.Calculate_dPhi_dT(temp) * pair.Key.CalculateCp(temp)) * pair.Value[0];

            return dCp_dt;
        }

        private float CalculateEnergyLostThroughDecomposition(Dictionary<AtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            float energy = 0;

            foreach (KeyValuePair<AtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
            {
                float heatOfFormationDecompositionSpecies = 0;
                foreach (KeyValuePair<AtmosphericGasSpecies, float> pairDecomposition in pair.Key.decompositionSpeciesWithFraction)
                {
                    heatOfFormationDecompositionSpecies += pairDecomposition.Key.GetHeatOfFormation() * pairDecomposition.Value;
                }

                energy += pair.Value[1] * (pair.Key.GetHeatOfFormation() - heatOfFormationDecompositionSpecies) * pair.Key.Calculate_dPhi_dT(temp);
            }

            return energy;

        }

        private void UpdateCompositionDueToDecomposition(Dictionary<AtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions, float temp)
        {
            for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
            {
                KeyValuePair<AtmosphericGasSpecies, float[]> pair = workingGasSpeciesAndMassFractions.ElementAt(i);
                float currentPhi = pair.Value[0];

                float tempDerivedPhi = pair.Key.CalculatePhi(temp) * pair.Value[0];
                float phiDiff = tempDerivedPhi - currentPhi;

                workingGasSpeciesAndMassFractions[pair.Key] = new float[] {tempDerivedPhi, pair.Value[1]};
                for (int j = 0; j < workingGasSpeciesAndMassFractions.Count; j++)
                {
                    KeyValuePair<AtmosphericGasSpecies, float[]> DecompPair = workingGasSpeciesAndMassFractions.ElementAt(j);
                    if(pair.Key.decompositionSpeciesWithFraction.ContainsKey(DecompPair.Key))
                    {
                        workingGasSpeciesAndMassFractions[DecompPair.Key] = new float[]{DecompPair.Value[0] - phiDiff * pair.Key.decompositionSpeciesWithFraction[DecompPair.Key], DecompPair.Value[1]};
                    }
                }
            }
        }

        #endregion

        #region Setup Functions
        private Dictionary<AtmosphericGasSpecies, float[]> CreateWorkingGasSpeciesAndMassFractionDict()
        {
            Dictionary<AtmosphericGasSpecies, float[]> workingGasSpeciesAndMassFractions = new Dictionary<AtmosphericGasSpecies, float[]>();

            //First, copy over the data from the default atmosphere
            foreach (KeyValuePair<AtmosphericGasSpecies, float> pair in gasSpeciesAndMassFractions)
            {
                workingGasSpeciesAndMassFractions.Add(pair.Key, new float[]{pair.Value, pair.Value});
            }
            //Then, go through each value and add its decomposition species; continue until no more items can be added to the dictionary
            int lastCount = workingGasSpeciesAndMassFractions.Count;
            do
            {
                for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
                {
                    KeyValuePair<AtmosphericGasSpecies, float[]> pair = workingGasSpeciesAndMassFractions.ElementAt(i);

                    foreach (KeyValuePair<AtmosphericGasSpecies, float> decompositionSpecies in pair.Key.decompositionSpeciesWithFraction)
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
                KeyValuePair<AtmosphericGasSpecies, float[]> pair = workingGasSpeciesAndMassFractions.ElementAt(i);

                foreach (KeyValuePair<AtmosphericGasSpecies, float> decompositionSpecies in pair.Key.decompositionSpeciesWithFraction)
                {
                    float[] tmp = workingGasSpeciesAndMassFractions[decompositionSpecies.Key];
                    tmp[1] += decompositionSpecies.Value * pair.Value[1];
                    workingGasSpeciesAndMassFractions[decompositionSpecies.Key] = tmp;
                }
            }          

            StringBuilder debug = new StringBuilder();
            //debug.AppendLine("Gas Species Initialized");
            //foreach(KeyValuePair<AtmosphericGasSpecies, float[]> pair in workingGasSpeciesAndMassFractions)
            //{
            //    debug.AppendLine(pair.Key.id + " Mass Fraction: " + pair.Value[0]);
            //}
            //Debug.Log(debug.ToString());

            return workingGasSpeciesAndMassFractions;
        }
        #endregion
    }

    public class AtmosphericGasSpecies
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
        public Dictionary<AtmosphericGasSpecies, float> decompositionSpeciesWithFraction;

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

        public AtmosphericGasSpecies(string thisId)
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

                        decompositionSpeciesWithFraction = new Dictionary<AtmosphericGasSpecies, float>();
                        foreach(ConfigNode decompositionGasSpeciesNode in thisNode.GetNodes("DECOMPOSITION_SPECIES"))
                        {
                            AtmosphericGasSpecies decompositionSpecies = AtmDataOrganizer.idOrganizedListOfGasSpecies[decompositionGasSpeciesNode.GetValue("id")];
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

    public struct Curves
    {
        public CurveData[] temp;
        public CurveData[] cp;
    }

    public class tempCurveDataContainer
    {
        public tempCurveDataContainer(CelestialBody body, AtmTempCurve callingCurve, bool dumpToText)
        {
            this.body = body;
            this.callingCurve = callingCurve;
            this.dumpToText = dumpToText;
        }
        public CelestialBody body;
        public AtmTempCurve callingCurve;
        public bool dumpToText;
    }

}
