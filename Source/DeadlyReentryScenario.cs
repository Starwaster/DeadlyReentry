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
		public bool displayParachuteWarning = true;
        public bool displayCrewGForceWarning = true;
		
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
                    //DeadlyReentry.ReentryPhysics.LoadSettings();
                }
			}
		}

        private static string[] difficultyName = {"Easy", "Default", "Hard", "Alternate.Model", "RSS"};

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
            return;
			node.AddValue ("difficultySetting", difficultySetting);
			node.AddValue ("displayParachuteWarning", displayParachuteWarning);
		}
		
		public override void OnLoad(ConfigNode node)
		{
            return;
			if (node.HasValue ("difficultySetting"))
				difficultySetting = int.Parse (node.GetValue ("difficultySetting"));

			if (node.HasValue("displayParachuteWarning"))
				bool.TryParse(node.GetValue("displayParachuteWarning"), out displayParachuteWarning);
		}
	}
}
