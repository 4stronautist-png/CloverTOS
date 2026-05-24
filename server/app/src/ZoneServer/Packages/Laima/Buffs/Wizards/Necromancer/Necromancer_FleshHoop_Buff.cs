using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Defensive shield for Flesh Defense.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.FleshHoop_Buff)]
	public class Necromancer_FleshHoop_BuffOverride : BuffHandler, IBuffCombatDefenseAfterCalcHandler
	{
		private const string ShieldValueKey = "Melia.Necromancer.FleshDefenseShield";

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var rate = Math.Max(0, buff.NumArg1) / 100f;
			var reinforceMultiplier = 1f;
			if (buff.Caster is ICombatEntity caster && caster.TryGetSkill(SkillId.Necromancer_FleshHoop, out var fleshDefenseSkill))
			{
				var reinforceRate = ScriptableFunctions.Skill.Get("SCR_Get_AbilityReinforceRate");
				reinforceMultiplier += reinforceRate(fleshDefenseSkill);
			}

			var shield = (int)MathF.Floor(buff.Target.MaxHp * rate * reinforceMultiplier);
			buff.Vars.SetInt(ShieldValueKey, Math.Max(1, shield));
			Send.ZC_UPDATE_SHIELD(buff.Target, Math.Max(1, shield));
		}

		public override void OnEnd(Buff buff)
		{
			buff.Vars.Remove(ShieldValueKey);
			Send.ZC_UPDATE_SHIELD(buff.Target, 0);
		}

		public void OnDefenseAfterCalc(Buff buff, ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			var remaining = buff.Vars.GetInt(ShieldValueKey, 0);
			if (remaining <= 0)
			{
				target.RemoveBuff(BuffId.FleshHoop_Buff);
				return;
			}

			var absorbed = Math.Min(remaining, (int)skillHitResult.Damage);
			skillHitResult.Damage -= absorbed;
			remaining -= absorbed;
			buff.Vars.SetInt(ShieldValueKey, remaining);
			Send.ZC_UPDATE_SHIELD(target, remaining);

			if (skillHitResult.Damage <= 0)
			{
				skillHitResult.Effect = HitEffect.SAFETY;
				skillHitResult.Result = HitResultType.Miss;
			}

			if (remaining <= 0)
				target.RemoveBuff(BuffId.FleshHoop_Buff);
		}
	}
}
