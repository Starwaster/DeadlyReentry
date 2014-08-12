using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using KSP;

namespace DeadlyReentry
{
    /// <summary>
    /// This class contains curves relating stagnation temperature to velocity
    /// and Cp to velocity
    /// (accounting for some real gas effects, like changes in specific heat and 
    /// dissocation), methods to develop the curve from atmospheric composition and
    /// the ability to dump the data in a comma delineated format
    /// </summary>
    public class DREAtmTempCurve
    {
        public FloatCurve tempAdditionFromVelocity = new FloatCurve();
        public CurveData[] protoTempCurve = null;
        public FloatCurve velCpCurve = new FloatCurve();
        public CurveData[] protoVelCpCurve = null;
        public float referenceTemp = 300;

        public void CalculateNewDREAtmTempCurve(CelestialBody body, bool dumpText)
        {
            if (ReentryPhysics.multithreadedTempCurve)
                ThreadPool.QueueUserWorkItem(DREAtmDataOrganizer.CalculateNewTemperatureCurve, new tempCurveDataContainer(body, this, dumpText));
            else
                DREAtmDataOrganizer.CalculateNewTemperatureCurve(new tempCurveDataContainer(body, this, dumpText));
        }

        public float EvaluateTempDiffCurve(float vel)
        {
            if(protoTempCurve != null)
            {
                tempAdditionFromVelocity = new FloatCurve();
                foreach(CurveData data in protoTempCurve)
                {
                    tempAdditionFromVelocity.Add(data.x, data.y, data.dy_dx, data.dy_dx);
                }
                protoTempCurve = null;
            }
            return tempAdditionFromVelocity.Evaluate(vel);
        }
        public float EvaluateVelCpCurve(float vel)
        {
            if (protoVelCpCurve != null)
            {
                velCpCurve = new FloatCurve();
                foreach (CurveData data in protoVelCpCurve)
                {
                    velCpCurve.Add(data.x, data.y, data.dy_dx, data.dy_dx);
                }
                protoTempCurve = null;
            }
            return velCpCurve.Evaluate(vel);
        }

        public void DumpToText(float velIncrements, CelestialBody body)
        {
            FileStream fs = File.Open(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/DeadlyReentry/" + body.bodyName + "_T_vs_V_curve.csv", FileMode.Create, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);

            EvaluateTempDiffCurve(0);
            for(float v = 0; v < tempAdditionFromVelocity.maxTime; v += velIncrements)
            {
                float y = EvaluateTempDiffCurve(v) + referenceTemp;
                float z = EvaluateVelCpCurve(v);
                string s = v.ToString() + ", " + y.ToString();
                sw.WriteLine(s);
            }

            sw.Close();
            sw = null;
            fs = null;
        }
    }
}
