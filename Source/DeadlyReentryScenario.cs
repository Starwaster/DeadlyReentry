using KSP;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyReentry
{
	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[]{GameScenes.FLIGHT,GameScenes.SPACECENTER})]
	public class DeadlyReentryScenario : ScenarioModule
	{
		public DeadlyReentryScenario ()
		{
		}
		
		public static DeadlyReentryScenario Instance;
		
		private int difficultySetting = 1;
		
		public int DifficultySetting
		{
			get
			{
				return this.difficultySetting;
			}
			set
			{
                if (this.difficultySetting != value)
                {
     				this.difficultySetting = value;
                    DeadlyReentry.ReentryPhysics.LoadSettings();
                }
			}
		}

        private static string[] difficultyName = {"Easy", "Default", "Hard"};

        public string DifficultyName
        {
            get
            {
                return difficultyName[this.difficultySetting];
            }
        }
		
		public override void OnAwake ()
		{
			DeadlyReentryScenario.Instance = this;
            this.difficultySetting = 1;
		}

        //public override void OnStart()
        //{
        //    ReentryPhysics.LoadSettings();
        //}
		
		public override void OnSave(ConfigNode node)
		{
			node.AddValue ("difficultySetting", difficultySetting);
		}
		
		public override void OnLoad(ConfigNode node)
		{
			if (node.HasValue ("difficultySetting"))
				difficultySetting = int.Parse (node.GetValue ("difficultySetting"));
            DeadlyReentry.ReentryPhysics.LoadSettings();
		}
	}
}
