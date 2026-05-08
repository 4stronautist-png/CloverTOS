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
	[SkillHandler(SkillId.Desperado_Equilibrium)]
	public class Desperado_EquilibriumOverride : IGroundSkillHandler
	{
		private const int ViolentCost = 1;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			var retreatShot = caster.IsBuffActive(BuffId.Equilibrium_toggle_Buff);

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
			DesperadoSkillHelper.SendSkillEffect(caster, target, originPos, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			if (retreatShot)
			{
				DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_equilibrium_back_shot");
				DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_BACKDASH");
				DesperadoSkillHelper.ApplyCustomEffect(caster);
				DesperadoSkillHelper.LeapCaster(caster, caster.Position.GetDirection(farPos).Backwards, 60);
				if (caster.IsAbilityActive(AbilityId.Desperado22))
					caster.StartBuff(BuffId.Desperado_Trail, skill.Level, 0, TimeSpan.FromSeconds(3), caster, skill.Id);
			}
			else
			{
				DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_equilibrium_rush_shot");
				DesperadoSkillHelper.PlayAnimationIfKnown(caster, "SKL_DESPERADO_DASH");
				DesperadoSkillHelper.ApplyCustomEffect(caster);
				DesperadoSkillHelper.LeapCaster(caster, caster.Position.GetDirection(farPos), 80);
				caster.StartBuff(BuffId.Equilibrium_toggle_Buff, skill.Level, 0, TimeSpan.FromSeconds(4), caster, skill.Id);
				caster.RemoveCooldown(CooldownId.Desperado_Equilibrium);
				if (caster.IsAbilityActive(AbilityId.Desperado23))
					caster.StartBuff(BuffId.Desperado_Weight, skill.Level, 0, TimeSpan.FromSeconds(3), caster, skill.Id);
			}

			skill.Run(this.Attack(skill, caster, farPos, retreatShot));
		}

		private async Task Attack(Skill skill, ICombatEntity caster, Position farPos, bool retreatShot)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(retreatShot ? 500 : 500));

			var modifier = DesperadoSkillHelper.BuildModifier(caster, skill, retreatShot ? 3 : 6, ViolentCost);
			var splashParam = skill.GetSplashParameters(caster, caster.Position, farPos, length: retreatShot ? 100f : 80f, width: 45f, angle: retreatShot ? 90 : 70);
			var splashArea = skill.GetSplashArea(retreatShot ? SplashType.Fan : SplashType.Square, splashParam);
			var targets = DesperadoSkillHelper.GetTargets(caster, skill, splashArea);
			var results = DesperadoSkillHelper.DealDamage(caster, skill, targets, modifier);
			DesperadoSkillHelper.ApplyBadGuyOnAccurateHit(caster, skill, results);

			if (retreatShot)
				caster.RemoveBuff(BuffId.Equilibrium_toggle_Buff);
		}
	}
}
