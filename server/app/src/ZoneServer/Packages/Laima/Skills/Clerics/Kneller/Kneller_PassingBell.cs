using System;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Yggdrasil.Geometry.Shapes;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[SkillHandler(SkillId.Kneller_PassingBell_Cleric)]
	public class Kneller_PassingBell_ClericOverride : IGroundSkillHandler, ITargetSkillHandler, IDynamicCasted
	{
		private const string RunningKey = "Melia.Kneller.PassingBell.Running";
		private const string ShameCastKey = "Melia.Kneller.PassingBell.ShameCast";
		private const string ShameCastReadyKey = "Melia.Kneller.PassingBell.ShameCastReady";
		private const string ShameCastStartedAtKey = "Melia.Kneller.PassingBell.ShameCastStartedAt";
		private const string SkipNextHandleKey = "Melia.Kneller.PassingBell.SkipNextHandle";
		private const float Radius = 45f;
		private const int TickIntervalMs = 1000;
		private const int PulseCount = 5;
		private const int MaxTargets = 10;
		private const int StacksPerTick = 2;
		private const int MaxShameStacks = 10;
		private const int FixedShameCastReleaseGraceMs = 350;
		private const string BellSound = "Kneller_PassingBell";
		private const string BellSoundFallback = "chapel_bell_sound_01";
		private const string ShamePassiveEffect = "eff_pc_rogue_backstab_bloodburst";
		private const float ShamePassiveEffectScale = 0.2f;
		private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan FixedShameCastTime = TimeSpan.FromSeconds(2);
		private static readonly TimeSpan ShameDuration = TimeSpan.FromSeconds(25);
		private static readonly TimeSpan ShamePvpDuration = TimeSpan.FromSeconds(12.5);
		private static readonly TimeSpan ShameCooldown = TimeSpan.FromSeconds(70);

		public void Handle(Skill skill, ICombatEntity caster, ICombatEntity target)
		{
			if (this.ConsumeSkipNextHandle(skill))
				return;

			var originPos = caster.Position;
			var farPos = target?.Position ?? caster.Position.GetRelative(caster.Direction, Radius);
			this.Cast(skill, caster, originPos, farPos);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (this.ConsumeSkipNextHandle(skill))
				return;

			originPos = caster.Position;
			farPos = target?.Position ?? caster.Position.GetRelative(caster.Direction, Radius);
			this.Cast(skill, caster, originPos, farPos);
		}

		public void StartDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			if (skill.IsOnCooldown || skill.Vars.GetBool(RunningKey) || skill.Vars.GetBool(ShameCastKey))
				return;

			if (caster.IsAbilityActive(AbilityId.Kneller9))
			{
				this.StartFixedShameCast(skill, caster);
				return;
			}

			var originPos = caster.Position;
			var farPos = caster.Position.GetRelative(caster.Direction, Radius);
			this.Cast(skill, caster, originPos, farPos);
		}

		public void EndDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			if (caster.IsAbilityActive(AbilityId.Kneller9))
			{
				skill.Vars.SetBool(SkipNextHandleKey, true);
				if (skill.Vars.GetBool(ShameCastKey))
					this.FinishFixedShameCast(skill, caster, true, this.GetFixedShameCastDebuffLevel(skill));

				return;
			}

			if (!skill.Vars.GetBool(RunningKey))
				return;

			skill.Vars.SetBool(SkipNextHandleKey, true);
		}

		private void Cast(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (skill.Vars.GetBool(RunningKey))
				return;

			if (!this.TryStartChannel(skill, caster, originPos, farPos))
				return;

			skill.Vars.SetBool(RunningKey, true);
			var duration = DefaultDuration;
			caster.StartBuff(BuffId.PassingBell_Buff, skill.Level, 0, duration, caster, skill.Id);
			this.PlayBellSound(caster);

			skill.PrepareCancellation();
			skill.Run(this.RingShameBell(skill, caster, farPos));
		}

		private void StartFixedShameCast(Skill skill, ICombatEntity caster)
		{
			skill.Vars.SetBool(ShameCastKey, true);
			skill.Vars.SetBool(ShameCastReadyKey, false);
			skill.Vars.SetBool(SkipNextHandleKey, true);
			skill.Vars.SetString(ShameCastStartedAtKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
			caster.StopMove();
			caster.SetCastingState(true, skill);
			caster.SetAttackState(true);
			this.PlayMourningChimeCastAnimation(skill, caster);
			this.PlayBellSound(caster);
			skill.PrepareCancellation();
			skill.Run(this.FinishFixedShameCastWhenReady(skill, caster));
		}

		private void FinishFixedShameCast(Skill skill, ICombatEntity caster, bool completed, int debuffLevel = 0)
		{
			if (completed && !caster.IsDead)
			{
				this.PlayBellSound(caster);
				this.CastShameAllAtOnce(skill, caster, debuffLevel);
			}

			caster.SetCastingState(false, skill);
			skill.Vars.SetBool(ShameCastKey, false);
			skill.Vars.SetBool(ShameCastReadyKey, false);
			skill.Vars.SetString(ShameCastStartedAtKey, "");
			skill.Vars.SetBool(SkipNextHandleKey, true);
			KnellerSkillHelper.ResetAction(caster, skill);
		}

		private void CastShameAllAtOnce(Skill skill, ICombatEntity caster, int debuffLevel)
		{
			var originPos = caster.Position;
			var farPos = caster.Position.GetRelative(caster.Direction, Radius);
			if (!this.TryStartPassiveBurst(skill, caster, originPos, farPos))
				return;

			this.PlayBellSound(caster);
			this.ApplyShameAt(skill, caster, caster.Position, MaxShameStacks, true, debuffLevel);
			skill.StartCooldown(ShameCooldown);
			KnellerSkillHelper.ResetAction(caster, skill);
		}

		private async Task FinishFixedShameCastWhenReady(Skill skill, ICombatEntity caster)
		{
			try
			{
				await skill.Wait(FixedShameCastTime);
					if (skill.Vars.GetBool(ShameCastKey))
					{
						skill.Vars.SetBool(ShameCastReadyKey, true);
						this.FinishFixedShameCast(skill, caster, true, skill.Level);
					}
				}
			catch (OperationCanceledException)
			{
				if (skill.Vars.GetBool(ShameCastKey))
					this.FinishFixedShameCast(skill, caster, false);
			}
		}

		private void PlayMourningChimeCastAnimation(Skill skill, ICombatEntity caster)
		{
			var originPos = caster.Position;
			var farPos = caster.Position.GetRelative(caster.Direction, Radius);

			if (caster.TryGetSkill(SkillId.Kneller_MourningChime_Cleric, out var mourningChime))
			{
				Send.ZC_SKILL_READY(caster, mourningChime, originPos, farPos);
				return;
			}

			Send.ZC_NORMAL.SkillChangeAnimation(caster, SkillId.Kneller_MourningChime_Cleric);
			Send.ZC_SKILL_READY(caster, skill, originPos, farPos);
		}

		private bool TryStartPassiveBurst(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Melia.Shared.L10N.Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.TurnTowards(farPos);
			caster.SetAttackState(true);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), farPos);
			return true;
		}

		private async System.Threading.Tasks.Task RingShameBell(Skill skill, ICombatEntity caster, Position effectCenter)
		{
			try
			{
				for (var tick = 0; tick < PulseCount; tick++)
				{
					if (caster.IsDead)
						break;

					this.PlayBellSound(caster);
						this.ApplyShameAt(skill, caster, effectCenter, StacksPerTick, false);

					if (tick < PulseCount - 1)
						await skill.Wait(TimeSpan.FromMilliseconds(TickIntervalMs));
				}
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				caster.StopBuff(BuffId.PassingBell_Buff);
				skill.Vars.SetBool(RunningKey, false);
				KnellerSkillHelper.ResetAction(caster, skill);
			}
		}

		private void ApplyShameAt(Skill skill, ICombatEntity caster, Position center, int stacks, bool passiveBurst, int debuffLevel = 0)
		{
			var area = new CircleF(center, Radius);
			var targets = passiveBurst
				? caster.Map.GetAttackableEnemiesIn(caster, area)
				: caster.Map.GetAttackableEnemiesIn(caster, area)
					.Concat(caster.Map.GetAttackableEnemiesIn(caster, new CircleF(caster.Position, Radius)));
			var duration = this.GetShameDuration(caster);

			foreach (var target in targets.Distinct().Take(MaxTargets))
			{
				if (target == null || target.IsDead || !caster.CanDamage(target))
					continue;

				var currentStacks = target.TryGetBuff(BuffId.Mourning_Dummy_Debuff, out var currentShame)
					? currentShame.OverbuffCounter
					: 0;
				var nextStacks = Math.Min(MaxShameStacks, Math.Max(1, currentStacks + stacks));
				var appliedDebuffLevel = debuffLevel > 0 ? Math.Min(skill.Level, debuffLevel) : skill.Level;
				var appliedShame = target.StartBuff(BuffId.Mourning_Dummy_Debuff, appliedDebuffLevel, 0, duration, caster, skill.Id,
					buff => buff.OverbuffCounter = nextStacks);
				var visibleShame = target.StartBuff(BuffId.Hwarang_Target_Debuff, appliedDebuffLevel, 0, duration, caster, skill.Id,
					buff => buff.OverbuffCounter = nextStacks);

				if (appliedShame != null)
					appliedShame.NotifyUpdate();

				visibleShame?.NotifyUpdate();

				if (passiveBurst)
					target.PlayEffect(ShamePassiveEffect, ShamePassiveEffectScale);
			}
		}

		private bool TryStartChannel(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Melia.Shared.L10N.Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.TurnTowards(farPos);
			caster.SetAttackState(true);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), farPos);
			return true;
		}

		private TimeSpan GetShameDuration(ICombatEntity caster)
			=> caster.Map.IsPVP ? ShamePvpDuration : ShameDuration;

		private bool HasCompletedFixedShameCast(Skill skill)
		{
			if (skill.Vars.GetBool(ShameCastReadyKey))
				return true;

			if (!long.TryParse(skill.Vars.GetString(ShameCastStartedAtKey), out var startedAt))
				return false;

			var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedAt;
			return elapsedMs >= FixedShameCastTime.TotalMilliseconds - FixedShameCastReleaseGraceMs;
		}

		private int GetFixedShameCastDebuffLevel(Skill skill)
		{
			if (!long.TryParse(skill.Vars.GetString(ShameCastStartedAtKey), out var startedAt))
				return 1;

			var elapsedMs = Math.Max(1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedAt);
			var castRatio = Math.Clamp(elapsedMs / FixedShameCastTime.TotalMilliseconds, 0.0, 1.0);
			return Math.Max(1, Math.Min(skill.Level, (int)Math.Ceiling(skill.Level * castRatio)));
		}

		private void PlayBellSound(ICombatEntity caster)
		{
			caster.PlaySound(BellSound);
			caster.PlaySound(BellSoundFallback);
		}

		private bool ConsumeSkipNextHandle(Skill skill)
		{
			if (!skill.Vars.GetBool(SkipNextHandleKey))
				return false;

			skill.Vars.SetBool(SkipNextHandleKey, false);
			return true;
		}
	}
}
