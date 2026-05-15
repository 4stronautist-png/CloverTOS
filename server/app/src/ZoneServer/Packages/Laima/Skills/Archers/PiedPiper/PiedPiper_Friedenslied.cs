using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Components;
using Melia.Zone.World.Actors.Monsters;
using Yggdrasil.Util;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_Friedenslied)]
	public class PiedPiperFriedenslied : IGroundSkillHandler, IMeleeGroundSkillHandler, IDynamicCasted, ICancelSkillHandler
	{
		private const float Range = 160f;
		private const int MaxTargets = 14;
		private const int DurationSeconds = 5;
		private const string ChannelingKey = "Melia.PiedPiper.Friedenslied.Channeling";
		private const string ChannelStartTicksKey = "Melia.PiedPiper.Friedenslied.ChannelStartTicks";
		private const string SoundName = "skl_eff_piedpiper_friedenslied_melody";

		public void StartDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Melia.Shared.L10N.Localization.Get("Not enough SP."));
				Send.ZC_SKILL_CAST_CANCEL(caster);
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(false);
			skill.Vars.Set(ChannelingKey, true);
			skill.Vars.Set(ChannelStartTicksKey, DateTime.UtcNow.Ticks);

			PiedPiperSkillHelper.SummonMouseFromSong(caster, skill);
			PiedPiperSkillHelper.SafePlaySound(caster, SoundName, loop: true);

			this.ApplyTargets(skill, caster, null, TimeSpan.FromMilliseconds(900));
			skill.Run(this.Channel(skill, caster, null));
		}

		public void EndDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			if (!skill.Vars.GetBool(ChannelingKey))
				return;

			caster.SetCastingState(true, skill);
			Send.ZC_NORMAL.Skill_DynamicCastStart(caster, skill.Id);
		}

		public void Handle(Skill skill, ICombatEntity caster)
		{
			this.EndChannel(skill, caster);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
			=> this.ExecuteFallback(skill, caster, originPos, farPos, null);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
			=> this.ExecuteFallback(skill, caster, originPos, farPos, targets);

		private void ExecuteFallback(Skill skill, ICombatEntity caster, Position originPos, Position farPos, IList<ICombatEntity> targets)
		{
			if (skill.Vars.GetBool(ChannelingKey))
			{
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			if (!PiedPiperSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			PiedPiperSkillHelper.SummonMouseFromSong(caster, skill);
			PiedPiperSkillHelper.SafePlaySound(caster, SoundName, loop: true);
			skill.Vars.Set(ChannelingKey, true);
			skill.Vars.Set(ChannelStartTicksKey, DateTime.UtcNow.Ticks);

			skill.Run(this.Channel(skill, caster, targets));
			skill.Run(skill.Wait(TimeSpan.FromSeconds(DurationSeconds)).ContinueWith(_ => this.EndChannel(skill, caster)));
		}

		private async Task Channel(Skill skill, ICombatEntity caster, IList<ICombatEntity> targets = null)
		{
			var deadline = DateTime.UtcNow.AddSeconds(DurationSeconds);
			var graceUntil = DateTime.UtcNow.AddMilliseconds(450);
			while (skill.Vars.GetBool(ChannelingKey) && DateTime.UtcNow < deadline)
			{
				if (DateTime.UtcNow >= graceUntil && IsSilenced(caster))
					break;

				this.ApplyTargets(skill, caster, targets, TimeSpan.FromMilliseconds(900));
				await skill.Wait(TimeSpan.FromMilliseconds(500));
			}

			if (skill.Vars.GetBool(ChannelingKey))
				this.EndChannel(skill, caster);
		}

		private void ApplyTargets(Skill skill, ICombatEntity caster, IList<ICombatEntity> targets, TimeSpan duration)
		{
			var smileClap = caster.IsAbilityActive(AbilityId.PiedPiper22);
			foreach (var enemy in PiedPiperSkillHelper.GetEnemiesFromHitListOrRange(caster, targets, Range).Where(CanAffect).Take(MaxTargets))
			{
				if (!smileClap || RandomProvider.Get().Next(100) < 50)
					PiedPiperSkillHelper.StartVisibleBuff(enemy, smileClap ? BuffId.Friedenslied_Abil_Debuff : BuffId.Friedenslied_Debuff, skill.Level, 0, duration, caster, skill.Id);

				enemy.RemoveRandomBuff();
			}

			if (smileClap)
				return;

			foreach (var ally in caster.SelectObjects(caster.Position, Range, RelationType.Friendly).Where(ally => ally != null && !ally.IsDead && ally.Handle != caster.Handle).GroupBy(ally => ally.Handle).Select(group => group.First()).Take(MaxTargets))
			{
				PiedPiperSkillHelper.StartVisibleBuff(ally, BuffId.Friedenslied_Buff, skill.Level, 0, duration, caster, skill.Id);
				PiedPiperSkillHelper.StartVisibleBuff(ally, BuffId.Skill_NoDamage_Buff, skill.Level, 0, duration, caster, skill.Id);
			}
		}

		private void EndChannel(Skill skill, ICombatEntity caster)
		{
			if (!skill.Vars.GetBool(ChannelingKey))
				return;

			skill.Vars.Set(ChannelingKey, false);
			skill.Vars.Remove(ChannelStartTicksKey);
			PiedPiperSkillHelper.SafeStopSound(caster, SoundName);
			caster.RemoveBuff(BuffId.Friedenslied_Buff);
			if (caster is Character character)
				character.Variables.Temp.Remove("Melia.Cast.Skill");
			caster.SetCastingState(false, skill);
			caster.SetAttackState(false);
			Send.ZC_NORMAL.Skill_DynamicCastEnd(caster, skill.Id, DurationSeconds);
			Send.ZC_SKILL_DISABLE(caster);
		}

		private static bool CanAffect(ICombatEntity target)
			=> target is not Mob mob || mob.Rank != MonsterRank.Boss;

		private static bool IsSilenced(ICombatEntity caster)
			=> caster.Components.Get<StateLockComponent>()?.IsStateActive(StateType.Silenced) == true;
	}
}
