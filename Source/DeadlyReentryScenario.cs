using KSP;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngineInternal;
using UnityEngine;

namespace DeadlyReentry
{
	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[]{GameScenes.FLIGHT,GameScenes.SPACECENTER})]
	public class DeadlyReentryScenario : ScenarioModule
	{
		public DeadlyReentryScenario ()
		{
		}
		
        public static bool displayCrewGForceWarning = true;
		
		private static int difficultySetting = 1;
		
        public static int DifficultySetting
		{
			get
			{
				return difficultySetting;
			}
			set
			{
                if (difficultySetting != value)
                {
     				difficultySetting = value;
                    //DeadlyReentry.ReentryPhysics.LoadSettings();
                }
			}
		}

        private static string[] difficultyName = {"Easy", "Default", "Hard"};

        public static string DifficultyName
        {
            get
            {
                return difficultyName[difficultySetting];
            }
        }
		
        //public override void OnStart()
        //{
        //    ReentryPhysics.LoadSettings();
        //}
		
		public override void OnSave(ConfigNode node)
		{
			node.AddValue ("difficultySetting", difficultySetting);
            node.AddValue ("displayCrewGForceWarning", displayCrewGForceWarning);
		}
		
		public override void OnLoad(ConfigNode node)
		{
			if (node.HasValue ("difficultySetting"))
				difficultySetting = int.Parse (node.GetValue ("difficultySetting"));
            if (node.HasValue("displayCrewGForceWarning"))
                displayCrewGForceWarning = bool.Parse(node.GetValue("displayCrewGForceWarning"));
		}
        static void print(string msg)
        {
            MonoBehaviour.print("[DeadlyReentry.DeadlyReentryScenario] " + msg);
        }
	}
}
