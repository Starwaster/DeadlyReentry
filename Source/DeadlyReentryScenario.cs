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
		
		private int difficultySetting;
		
		public int DifficultySetting
		{
			get
			{
				return this.difficultySetting;
			}
			set
			{
				this.difficultySetting = value;
			}
		}
		
		public override void OnAwake ()
		{
			DeadlyReentryScenario.Instance = this;
		}
		
		public override void OnSave(ConfigNode node)
		{
			node.AddValue ("difficultySetting", difficultySetting);
		}
		
		public override void OnLoad(ConfigNode node)
		{
			if (node.HasValue ("difficultySetting"))
				difficultySetting = int.Parse (node.GetValue ("difficultySetting"));
		}
	}
}
