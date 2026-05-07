using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Yggdrasil.Util;

namespace Melia.Zone.Skills.Handlers.Scouts.Bulletmarker
{
	[Package("laima")]
	[SkillHandler(SkillId.Bulletmarker_MozambiqueDrill)]
	public class Bulletmarker_MozambiqueDrill : IForceSkillHandler, IDynamicCasted
	{
		private const int BleedingChance = 50;
		private const int BleedingDurationSeconds = 8;
		private const int BleedingTickCount = 8;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!BulletmarkerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			var modifier = BulletmarkerSkillHelper.CreateModifier(skill);

			if (BulletmarkerSkillHelper.TryConsumeOutrage(caster))
			{
				modifier.FinalDamageMultiplier *= 0.775f;
				modifier.HitCount *= 2;
			}

			if (caster.TryGetActiveAbilityLevel(AbilityId.Bulletmarker9, out var ignoreDefenseLevel))
				modifier.DefensePenetrationRate += Math.Min(1f, ignoreDefenseLevel * 0.02f);

			var ricochetChance = caster.IsAbilityActive(AbilityId.Bulletmarker10) ? 100 : 0;

			skill.Run(BulletmarkerSkillHelper.AttackTarget(skill, caster, target, modifier, (hitTarget, hitResult) =>
			{
				if (RandomProvider.Get().Next(100) < BleedingChance)
				{
					var bleedDamagePerTick = Math.Max(1, hitResult.Damage / BleedingTickCount);
					hitTarget.StartBuff(BuffId.HeavyBleeding, skill.Level, bleedDamagePerTick, TimeSpan.FromSeconds(BleedingDurationSeconds), caster, skill.Id);
				}

				BulletmarkerSkillHelper.TryRicochet(caster, skill, hitTarget, modifier, ricochetChance);
			}));
		}
	}
}
