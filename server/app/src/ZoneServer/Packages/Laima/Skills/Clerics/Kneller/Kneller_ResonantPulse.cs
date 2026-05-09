using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[SkillHandler(SkillId.Kneller_ResonantPulse_Cleric)]
	public class Kneller_ResonantPulse_ClericOverride : IGroundSkillHandler, IDynamicCasted
	{
		private const float Radius = 45f;
		private const int TickIntervalMs = 350;
		private const int MaxTargets = 10;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			originPos = caster.Position;
			farPos = caster.Position.GetRelative(caster.Direction, 140f);

			if (!KnellerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var start = caster.Position.GetRelative(caster.Direction, 20f);
			var end = caster.Position.GetRelative(caster.Direction, 140f);
			var ticks = Math.Max(1, skill.Data.MultiHitCount);
			KnellerSkillHelper.PlayGroundEffect(caster, "F_Featherfoot_Kundela", start, 1.0f, TickIntervalMs);

			KnellerSkillHelper.RunAndReset(skill, caster, () => KnellerSkillHelper.AttackCircleOverTime(skill, caster, tick =>
				{
					var ratio = ticks <= 1 ? 1f : tick / (float)(ticks - 1);
					return new Position(
						start.X + (end.X - start.X) * ratio,
						start.Y + (end.Y - start.Y) * ratio,
						start.Z + (end.Z - start.Z) * ratio);
				},
				ticks,
				TickIntervalMs,
				Radius,
				configureHit: (hitTarget, modifier) =>
				{
					if (caster.IsAbilityActive(AbilityId.Kneller11) && KnellerSkillHelper.ConsumeFrostGrave(caster, hitTarget))
						modifier.DamageMultiplier += 0.50f;
				},
				afterHit: (hitTarget, result) =>
				{
					if (result.Damage > 0 && caster.IsAbilityActive(AbilityId.Kneller108))
						this.PullTowards(skill, caster, hitTarget);
				},
				hitCountPerTick: 1,
				tickEffect: "F_Featherfoot_Kundela",
				tickEffectScale: 1.0f,
				maxTargets: MaxTargets));
		}

		private void PullTowards(Skill skill, ICombatEntity caster, ICombatEntity target)
		{
			if (!target.IsKnockdownable())
				return;

			var direction = target.Position.GetDirection(caster.Position);
			var hit = new SkillHitInfo(caster, target, skill, new SkillHitResult { Damage = 0, Result = HitResultType.Hit }, TimeSpan.Zero, TimeSpan.Zero)
			{
				KnockBackInfo = new KnockBackInfo(target, KnockBackType.KnockBack, 35, 10, direction)
			};
			hit.HitInfo.KnockBackType = KnockBackType.KnockBack;
			hit.ApplyKnockBack(target);
			Send.ZC_KNOCKBACK_INFO(target, hit.KnockBackInfo);
		}
	}
}
