using System;
using System.Linq;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
	using Melia.Zone.Skills;
	using Melia.Zone.Skills.Combat;
	using Melia.Zone.Skills.Handlers.Archers.Wugushi;
	using Melia.Zone.Skills.SplashAreas;
	using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers
{
	/// <summary>
	/// Handle for the Virus Debuff, which ticks poison damage while active,
	/// spreads on death, and has a 20% chance to spread to one nearby enemy
	/// when the infected target is struck.
	/// </summary>
	/// <remarks>
	/// NumArg1: Skill Level
	/// NumArg2: Snapshotted damage per tick (calculated on buff application)
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Virus_Debuff)]
	public class Virus_DebuffOverride : DamageOverTimeBuffHandler
	{
		private const int MaxSpreadOnDeathAmount = 5;
		private const int MaxSpreadCount = 5;
		private const string RemainingDurationVar = "Melia.Virus_Debuff.RemainingDuration";

		public override void WhileActive(Buff buff)
		{
			buff.Vars.Set(RemainingDurationVar, buff.RemainingDuration);
			base.WhileActive(buff);
			this.SpreadVirus(buff, MaxSpreadCount);
		}

		public override void OnEnd(Buff buff)
		{
			if (buff.Target.IsDead)
				this.SpreadVirusOnDeath(buff);
		}

		protected override HitType GetHitType(Buff buff)
		{
			return HitType.Poison;
		}

		/// <summary>
		/// When the poisoned target is struck, spread poison to nearby enemies.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Virus_Debuff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.TryGetBuff(BuffId.Virus_Debuff, out var buff))
				return;

			if (buff.Caster is not ICombatEntity caster)
				return;

			this.SpreadVirus(buff, MaxSpreadCount);
		}

		private void SpreadVirusOnDeath(Buff buff)
		{
			if (buff.Caster is not ICombatEntity caster)
				return;

			var target = buff.Target;

			if (target.Map == null)
				return;

			var targetsInRange = target.Map.GetAttackableEnemiesInPosition(caster, target.Position, this.GetSpreadRange(caster));
			var spreadTargets = targetsInRange
				.Where(a => a != target && !a.IsBuffActive(BuffId.Virus_Debuff))
				.Take(MaxSpreadOnDeathAmount);

			var damage = buff.NumArg2;

			if (!buff.Vars.TryGet<TimeSpan>(RemainingDurationVar, out var remainingDuration) || remainingDuration <= TimeSpan.Zero)
				return;

			foreach (var spreadTarget in spreadTargets)
				spreadTarget.StartBuff(BuffId.Virus_Debuff, buff.NumArg1, damage, remainingDuration, caster, buff.SkillId);
		}

		private void SpreadVirus(Buff buff, int maxTargets)
		{
			if (buff.Caster is not ICombatEntity caster)
				return;

			var target = buff.Target;
			if (target.IsDead || target.Map == null || buff.RemainingDuration <= TimeSpan.Zero)
				return;

			var spreadRange = this.GetSpreadRange(caster);
			var spreadTargets = target.Map.GetAttackableEnemiesIn(caster, new Circle(target.Position, spreadRange))
				.Where(e => e != target && !e.IsBuffActive(BuffId.Virus_Debuff))
				.Take(maxTargets);

			foreach (var spreadTarget in spreadTargets)
				spreadTarget.StartBuff(BuffId.Virus_Debuff, buff.NumArg1, buff.NumArg2, buff.RemainingDuration, caster, buff.SkillId);
		}

		private float GetSpreadRange(ICombatEntity caster)
		{
			return WugushiSkillHelper.GetWugongGuSpreadRange(caster);
		}
	}
}
