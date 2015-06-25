using System;
using UnityEngine;
using KSP;
using ModularFI;

namespace DeadlyReentry
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class DREFlightIntegrator : MonoBehaviour
    {
        public DREFlightIntegrator()
        {
        }


        //private static voidThermalDataDelegate updateConvectionOverride;

        //ModularFlightIntegrator.voidThermalDataDelegate
        


        //solarFlux 
        //solarFluxMultiplier;
        //bodyEmissiveFlux
        // bodyAlbedoFlux;
    
        //densityThermalLerp


        public void Start()
        {
            return;
            print("Attempting to register ProcessUpdateConvectionOverride with ModularFlightIntegrator");
            bool result=false;
            result =  ModularFlightIntegrator.RegisterUpdateConvectionOverride(ProcessUpdateConvection);
            if (!result)
                print("Unable to override stock convection heating!");
            
            result = false;
            print("Attempting to register ProcessUpdateRadiationOverride with ModularFlightIntegrator");
            result = ModularFlightIntegrator.RegisterUpdateRadiationOverride(ProcessUpdateRadiation);
            if (!result)
                print("Unable to override stock radiant heating!");
        }


        protected void ProcessUpdateConvection (ModularFlightIntegrator fi, ModularFlightIntegrator.PartThermalData ptd)
        {
        }


        protected void ProcessUpdateRadiation(ModularFlightIntegrator fi, ModularFlightIntegrator.PartThermalData ptd)
        {
        }

        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.DREFlightIntegrator] " + msg);
        }
    }
}

