using System;
using Melia.Shared.L10N;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.World.Quests.Rewards
{
	/// <summary>
	/// A reward that scales to the character's current level threshold.
	/// </summary>
	public class LevelScaledExpReward : QuestReward
	{
		private readonly float _expRate;
		private readonly float _jobExpRate;

		public override string Icon => "expup_img";

		public LevelScaledExpReward(float expRate, float jobExpRate)
		{
			_expRate = Math.Max(0, expRate);
			_jobExpRate = Math.Max(0, jobExpRate);
		}

		public override void Give(Character character)
		{
			if (character == null)
				return;

			var maxExp = Math.Max(1, character.MaxExp);
			var maxJobExp = Math.Max(1, character.Job?.MaxExp ?? maxExp);
			var exp = Math.Max(1, (long)Math.Ceiling(maxExp * _expRate));
			var jobExp = Math.Max(1, (long)Math.Ceiling(maxJobExp * _jobExpRate));
			(exp, jobExp) = character.GetWorldScaledExperienceAmounts(exp, jobExp);

			character.GiveExp(exp, jobExp, null);
		}

		public override string ToString()
		{
			return string.Format(Localization.Get("{0:P0} EXP, {1:P0} Job EXP"), _expRate, _jobExpRate);
		}
	}
}
