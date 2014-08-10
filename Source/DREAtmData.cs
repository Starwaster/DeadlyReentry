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

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("DRE_ATM_COMPOSITIONS"))
            {
                foreach (ConfigNode atmNode in node.GetNodes("ATM_COMPOSITION"))
                {
                    DREAtmosphereComposition newComposition = new DREAtmosphereComposition();

                    newComposition.gasSpeciesAndMassFractions = new Dictionary<DREAtmosphericGasSpecies, float>();
                    foreach (ConfigNode gasSpeciesNode in atmNode.GetNodes("GAS_SPECIES"))
                    {
                        DREAtmosphericGasSpecies decompositionSpecies = DREAtmDataOrganizer.idOrganizedListOfGasSpecies[gasSpeciesNode.GetValue("id")];
                        float fraction = float.Parse(gasSpeciesNode.GetValue("fraction"));

                        newComposition.gasSpeciesAndMassFractions.Add(decompositionSpecies, fraction);
                    }

                    newComposition.referenceTemperature = float.Parse(atmNode.GetValue("referenceTemperature"));

                    string bodyName = atmNode.GetValue("bodyName");

                    foreach(CelestialBody body in FlightGlobals.Bodies)
                        if(body.name == bodyName)
                        {
                            bodyOrganizedListOfAtmospheres.Add(body, newComposition);
                            break;
                        }

                }
            }
        }

        public static FloatCurve CalculateNewTemperatureCurve(CelestialBody body)
        {
            DREAtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];
            Debug.Log("Beginning Temperature Curve Calculation");
            return atmosphere.TemperatureAsFunctionOfVelocity(100, 1, 25000);
        }

        public static float GetReferenceTemp(CelestialBody body)
        {
            DREAtmosphereComposition atmosphere = bodyOrganizedListOfAtmospheres[body];

            return atmosphere.referenceTemperature;
        }
    }

    public class DREAtmosphereComposition
    {
        public Dictionary<DREAtmosphericGasSpecies, float> gasSpeciesAndMassFractions;
        public float referenceTemperature;

        public FloatCurve TemperatureAsFunctionOfVelocity(int stepsBetweenCurvePoints, float dVForIntegration, float maxVel)
        {
            FloatCurve tempVsVelCurve = new FloatCurve();

            Dictionary<DREAtmosphericGasSpecies, Vector2> workingGasSpeciesAndMassFractions = CreateWorkingGasSpeciesAndMassFractionDict();

            float temp = referenceTemperature;
            float velocity = 0;

            StringBuilder debug = new StringBuilder();

            tempVsVelCurve.Add(velocity, 0, 0, 0);

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
                    tempVsVelCurve.Add(velocity, temp - referenceTemperature, dT_dV, dT_dV);

                i++;
                temp += dT_dV * dVForIntegration;
                velocity += dVForIntegration;

                debug.AppendLine("Cp: " + Cp + " dCp_dt: " + dCp_dt + " energyChange: " + energyChange + " vel: " + velocity + " temp: " + temp + " dT_dV: " + dT_dV);
            }
            Debug.Log(debug.ToString());

            return tempVsVelCurve;
        }

        #region dT_dV internal functions
        private float CalculateCp(Dictionary<DREAtmosphericGasSpecies, Vector2> workingGasSpeciesAndMassFractions, float temp)
        {
            float Cp = 0;

            foreach (KeyValuePair<DREAtmosphericGasSpecies, Vector2> pair in workingGasSpeciesAndMassFractions)
                Cp += pair.Key.CalculateCp(temp) * pair.Key.CalculatePhi(temp) * pair.Value.x;

            return Cp;
        }

        private float Calculate_dCp_dt(Dictionary<DREAtmosphericGasSpecies, Vector2> workingGasSpeciesAndMassFractions, float temp)
        {
            float dCp_dt = 0;

            foreach (KeyValuePair<DREAtmosphericGasSpecies, Vector2> pair in workingGasSpeciesAndMassFractions)
                dCp_dt += (pair.Key.Calculate_dCp_dT(temp) * pair.Key.CalculatePhi(temp) + pair.Key.Calculate_dPhi_dT(temp) * pair.Key.CalculateCp(temp)) * pair.Value.x;

            return dCp_dt;
        }

        private float CalculateEnergyLostThroughDecomposition(Dictionary<DREAtmosphericGasSpecies, Vector2> workingGasSpeciesAndMassFractions, float temp)
        {
            float energy = 0;

            foreach (KeyValuePair<DREAtmosphericGasSpecies, Vector2> pair in workingGasSpeciesAndMassFractions)
            {
                float heatOfFormationDecompositionSpecies = 0;
                foreach (KeyValuePair<DREAtmosphericGasSpecies, float> pairDecomposition in pair.Key.decompositionSpeciesWithFraction)
                {
                    heatOfFormationDecompositionSpecies += pairDecomposition.Key.GetHeatOfFormation() * pairDecomposition.Value;
                }

                energy += pair.Value.y * (pair.Key.GetHeatOfFormation() - heatOfFormationDecompositionSpecies) * pair.Key.Calculate_dPhi_dT(temp);
            }

            return energy;

        }

        private void UpdateCompositionDueToDecomposition(Dictionary<DREAtmosphericGasSpecies, Vector2> workingGasSpeciesAndMassFractions, float temp)
        {
            for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
            {
                KeyValuePair<DREAtmosphericGasSpecies, Vector2> pair = workingGasSpeciesAndMassFractions.ElementAt(i);
                float currentPhi = pair.Value.x;

                float tempDerivedPhi = pair.Key.CalculatePhi(temp) * pair.Value.x;
                float phiDiff = tempDerivedPhi - currentPhi;

                workingGasSpeciesAndMassFractions[pair.Key] = new Vector2(tempDerivedPhi, pair.Value.y);
                for (int j = 0; j < workingGasSpeciesAndMassFractions.Count; j++)
                {
                    KeyValuePair<DREAtmosphericGasSpecies, Vector2> DecompPair = workingGasSpeciesAndMassFractions.ElementAt(j);
                    if(pair.Key.decompositionSpeciesWithFraction.ContainsKey(DecompPair.Key))
                    {
                        workingGasSpeciesAndMassFractions[DecompPair.Key] = new Vector2(DecompPair.Value.x - phiDiff * pair.Key.decompositionSpeciesWithFraction[DecompPair.Key], DecompPair.Value.y);
                    }
                }
            }
        }

        #endregion

        #region Setup Functions
        private Dictionary<DREAtmosphericGasSpecies, Vector2> CreateWorkingGasSpeciesAndMassFractionDict()
        {
            Dictionary<DREAtmosphericGasSpecies, Vector2> workingGasSpeciesAndMassFractions = new Dictionary<DREAtmosphericGasSpecies, Vector2>();

            //First, copy over the data from the default atmosphere
            foreach (KeyValuePair<DREAtmosphericGasSpecies, float> pair in gasSpeciesAndMassFractions)
            {
                workingGasSpeciesAndMassFractions.Add(pair.Key, new Vector2(pair.Value, pair.Value));
            }
            //Then, go through each value and add its decomposition species; continue until no more items can be added to the dictionary
            int lastCount = workingGasSpeciesAndMassFractions.Count;
            do
            {
                for (int i = 0; i < workingGasSpeciesAndMassFractions.Count; i++)
                {
                    KeyValuePair<DREAtmosphericGasSpecies, Vector2> pair = workingGasSpeciesAndMassFractions.ElementAt(i);

                    foreach (KeyValuePair<DREAtmosphericGasSpecies, float> decompositionSpecies in pair.Key.decompositionSpeciesWithFraction)
                    {
                        if (!workingGasSpeciesAndMassFractions.ContainsKey(decompositionSpecies.Key))
                            workingGasSpeciesAndMassFractions.Add(decompositionSpecies.Key, new Vector2(0, decompositionSpecies.Value * pair.Value.y));
                        //else
                        //    workingGasSpeciesAndMassFractions[decompositionSpecies.Key] += new Vector2(0, decompositionSpecies.Value * pair.Value.y);
                    }
                }

                lastCount = workingGasSpeciesAndMassFractions.Count;
            } while (workingGasSpeciesAndMassFractions.Count > lastCount);

            StringBuilder debug = new StringBuilder();
            debug.AppendLine("Gas Species Initialized");
            foreach(KeyValuePair<DREAtmosphericGasSpecies, Vector2> pair in workingGasSpeciesAndMassFractions)
            {
                debug.AppendLine(pair.Key.id + " Mass Fraction: " + pair.Value.x);
            }
            Debug.Log(debug.ToString());

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
                        specificGasConstant = 8131.4f / molecularMass;

                        heatOfFormation = float.Parse(thisNode.GetValue("heatOfFormation")) / molecularMass * 1000000;

                        decompositionSpeciesWithFraction = new Dictionary<DREAtmosphericGasSpecies, float>();
                        foreach(ConfigNode decompositionGasSpeciesNode in thisNode.GetNodes("DECOMPOSITION_SPECIES"))
                        {
                            DREAtmosphericGasSpecies decompositionSpecies = DREAtmDataOrganizer.idOrganizedListOfGasSpecies[decompositionGasSpeciesNode.GetValue("id")];
                            float fraction = float.Parse(decompositionGasSpeciesNode.GetValue("fraction"));

                            decompositionSpeciesWithFraction.Add(decompositionSpecies, fraction);
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

            
            Debug.Log("Initialized Gas Species '" + id + "'\n\r");
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
    }
}
