using System;
using System.Reflection;
using UnityEngine;
using KSP;

namespace Animation2Value
{
    public class ModuleAnimation2Value : PartModule
    {
        [KSPField(isPersistant = false)]
        public string valueModule = "";

        [KSPField(isPersistant = false)]
        public string valueName = "";

        [KSPField(isPersistant = false)]
        public string transformName = "";

        [KSPField(isPersistant = false)]
        public FloatCurve valueCurve = new FloatCurve();

        protected Animation[] anims;
        protected PartModule module;
        protected Type moduleType;
        protected FieldInfo field;
        protected PropertyInfo property;
		protected float currentTime;
		protected PartModule FSwheel = null;
		protected Type fswType = null;
		protected bool fswHack = false;

        public override void OnStart(PartModule.StartState state)
        {
            if (state != PartModule.StartState.Editor)
            {
                anims = part.FindModelAnimators(transformName);
                if ((anims == null) || (anims.Length == 0))
                {
                    print("ModuleAnimation2Value - animation not found: " + transformName);
                }

                moduleType = part.GetType();
                if (valueModule != "")
                {
                    if (part.Modules.Contains(valueModule))
                    {
                        module = part.Modules[valueModule];
                        moduleType = module.GetType();
                    }
                    else
                    {
                        print("ModuleAnimation2Value - module not found: " + valueModule);
                    }
                }

                field = moduleType.GetField(valueName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    property = moduleType.GetProperty(valueName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (property == null)
                    {
                        print("ModuleAnimation2Value - field/property not found: " + valueName);
                    }
                }
				if ((anims != null) && (anims.Length > 0) && ((field != null) || (property != null)))
				{
					// Check to see if our part contains the Firespitter module FSwheel
					if (part.Modules.Contains("FSwheel"))
					{
						FSwheel = part.Modules["FSwheel"];
						if (FSwheel != null)
							fswType = FSwheel.GetType ();
						// Check to see if our animation is the same as the one used by FSwheel
						if (transformName == (string)fswType.GetField ("transformName").GetValue (FSwheel))
							fswHack = true;
					}
				}
            }

            base.OnStart(state);
        }

        public void FixedUpdate()
        {
            if (FlightGlobals.ready)
            {
                if ((anims != null) && (anims.Length > 0) && ((field != null) || (property != null)))
                {
                    object target = part;

                    if (module != null)
                    {
                        target = module;
                    }

					currentTime = anims[0][transformName].normalizedTime;

					// Workaround hack for Firespitter FSwheel issue.
					// Corrects a problem where non-playing animation time is always 0. 
					if (fswHack && (string)fswType.GetField ("deploymentState").GetValue (FSwheel) == "Retracted")
					{
						// Retracted wheel should always be time index 1
						currentTime = 1f;
					}


                    if (field != null)
                    {
                        field.SetValue(target, valueCurve.Evaluate(currentTime));
                    }
                    else
                    {
                        property.SetValue(target, valueCurve.Evaluate(currentTime), null);
                    }
                }
            }
        }
    }
}
