using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace DeadlyReentry
{
    /// <summary>
    /// This class contains a curve relating stagnation temperature to velocity
    /// (accounting for some real gas effects, like changes in specific heat and 
    /// dissocation), methods to develop the curve from atmospheric composition and
    /// the ability to dump the data in a comma delineated format
    /// </summary>
    public class DREAtmTempCurve
    {
        private FloatCurve tempAdditionFromVelocity = null;
        private float referenceTemp = 300;

        public void CalculateNewDREAtmTempCurve(CelestialBody body)
        {
            tempAdditionFromVelocity = DREAtmDataOrganizer.CalculateNewTemperatureCurve(body);
            referenceTemp = DREAtmDataOrganizer.GetReferenceTemp(body);
            DumpToText(5);
        }

        public float EvaluateTempDiffCurve(float vel)
        {
            return tempAdditionFromVelocity.Evaluate(vel) + referenceTemp;
        }

        public void DumpToText(float velIncrements)
        {
            FileStream fs = File.Open(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/DeadlyReentry/temp_vs_vel_curve.csv", FileMode.CreateNew, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.ASCII);

            for(float v = 0; v < tempAdditionFromVelocity.maxTime; v += velIncrements)
            {
                sw.WriteLine(v + ", " + EvaluateTempDiffCurve(v));
            }

            sw.Close();
            sw = null;
            fs = null;
        }
    }
}
