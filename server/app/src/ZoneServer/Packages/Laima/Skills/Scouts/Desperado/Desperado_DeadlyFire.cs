using System;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	[Package("laima")]
	[SkillHandler(SkillId.Desperado_DeadlyFire)]
	public class Desperado_DeadlyFireOverride : IGroundSkillHandler
	{
		private const int ViolentCost = 3;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			DesperadoSkillHelper.SendSkillEffect(caster, target, originPos, farPos);
			DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_DEADLYFIRE");
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_deadlyfire_shot_1");
			DesperadoSkillHelper.ApplyCustomEffect(caster);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			skill.Run(this.Attack(skill, caster, farPos));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position farPos)
		{
			var splashParam = skill.GetSplashParameters(caster, caster.Position, farPos, length: 130f, width: 45f, angle: 35);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);

			foreach (var target in targets)
				target.StartBuff(BuffId.DeadlyFire_Debuff, skill.Level, 0, TimeSpan.FromSeconds(1), caster, skill.Id);

			await skill.Wait(TimeSpan.FromMilliseconds(850));
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_deadlyfire_shot_2");

			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, 6, ViolentCost);
			if (caster.TryGetBuff(BuffId.Desperado_Weight, out _))
			{
				if (caster.TryGetSkillLevel(SkillId.Desperado_Equilibrium, out var equilibriumLevel))
					modifier.SkillFactorBonus += 5f * equilibriumLevel;

				caster.RemoveBuff(BuffId.Desperado_Weight);
			}

			var results = DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier);
			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);

			await skill.Wait(TimeSpan.FromMilliseconds(50));
			DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_DEADLYFIRE_END");
			DesperadoSkillHelper.LeapCaster(caster, caster.Position.GetDirection(farPos).Backwards, 40, moveTime: 0.15f);
			DesperadoSkillHelper.ApplyCustomEffect(caster);
		}
	}
}
