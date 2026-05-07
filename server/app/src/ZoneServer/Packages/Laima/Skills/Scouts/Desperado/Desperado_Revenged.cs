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
	[SkillHandler(SkillId.Desperado_Revenged)]
	public class Desperado_RevengedOverride : IGroundSkillHandler
	{
		private const int ViolentCost = 2;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			caster.StartBuff(BuffId.Skill_NoDamage_Buff, skill.Level, 0, TimeSpan.FromMilliseconds(700), caster);
			var inputDirection = caster.Direction;
			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			DesperadoSkillHelper.SendSkillEffect(caster, target, originPos, farPos);
			DesperadoSkillHelper.MoveCasterAroundTarget(caster, target, inputDirection, farPos);
			DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_REVENGED_R");
			DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_revenged_shot");
			DesperadoSkillHelper.ApplyCustomEffect(caster);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			skill.Run(this.Attack(skill, caster, farPos, target));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position farPos, ICombatEntity focusedTarget)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(440));
			if (focusedTarget != null)
				caster.TurnTowards(focusedTarget);

			await skill.Wait(TimeSpan.FromMilliseconds(60));

			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, 6, ViolentCost);
			if (caster.TryGetBuff(BuffId.Desperado_Trail, out _))
			{
				if (caster.TryGetSkillLevel(SkillId.Desperado_Equilibrium, out var equilibriumLevel))
					modifier.SkillFactorBonus += 4f * equilibriumLevel;

				caster.RemoveBuff(BuffId.Desperado_Trail);
			}

			var splashParam = skill.GetSplashParameters(caster, caster.Position, farPos, length: 80f, width: 50f, angle: 90);
			var splashArea = skill.GetSplashArea(SplashType.Fan, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);
			var results = DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier);
			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);
		}
	}
}
