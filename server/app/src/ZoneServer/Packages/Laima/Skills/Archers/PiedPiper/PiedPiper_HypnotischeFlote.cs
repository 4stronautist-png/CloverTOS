using System;
using System.Threading.Tasks;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_HypnotischeFlote)]
	public class PiedPiperHypnotischeFlote : IGroundSkillHandler, IDynamicCasted
	{
		private const float Range = 160f;
		private const int MaxTargets = 13;
		private const int DurationSeconds = 10;
		private const int ConfuseDurationSeconds = 3;
		private const string ChannelingKey = "Melia.PiedPiper.HypnotischeFlote.Channeling";
		private const string SoundName = "skl_archer_piedpiper_1";
		private const string EndSoundName = "skl_archer_piedpiper_final_1";

		public void StartDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Melia.Shared.L10N.Localization.Get("Not enough SP."));
				Send.ZC_SKILL_CAST_CANCEL(caster);
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			skill.Vars.Set(ChannelingKey, true);

			PiedPiperSkillHelper.SummonMouseFromSong(caster, skill);
			PiedPiperSkillHelper.StartVisibleBuff(caster, BuffId.Fluting_Buff, skill.Level, 0, TimeSpan.FromSeconds(DurationSeconds), caster, skill.Id);
			PiedPiperSkillHelper.SafePlayEffect(caster, "I_buff_Fluting_Buff", 3f);
			PiedPiperSkillHelper.SafePlayEffect(caster, "PiedPiper_Fluting", 1f);
			PiedPiperSkillHelper.SafePlaySound(caster, SoundName, loop: true);
			skill.Run(this.Channel(skill, caster));
		}

		public void EndDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			this.EndChannel(skill, caster, true);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (skill.Vars.GetBool(ChannelingKey))
			{
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			if (!PiedPiperSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			this.ApplyTargets(skill, caster);
			skill.Run(skill.Wait(TimeSpan.FromSeconds(DurationSeconds)).ContinueWith(_ => this.EndChannel(skill, caster, true)));
		}

		private async Task Channel(Skill skill, ICombatEntity caster)
		{
			while (caster.IsCasting(skill) && skill.Vars.GetBool(ChannelingKey))
			{
				this.ApplyTargets(skill, caster);
				await skill.Wait(TimeSpan.FromMilliseconds(500));
			}
		}

		private void ApplyTargets(Skill skill, ICombatEntity caster)
		{
			var enemies = PiedPiperSkillHelper.GetEnemiesInSongRange(caster, Range)
				.Where(e => CanHypnotize(caster, e))
				.Take(MaxTargets)
				.ToList();

			for (var i = 0; i < enemies.Count; ++i)
			{
				var buff = PiedPiperSkillHelper.StartVisibleBuff(enemies[i], BuffId.Fluting_DeBuff, skill.Level, i, TimeSpan.FromMilliseconds(900), caster, skill.Id);
				buff?.SetUpdateTime(250);
			}
		}

		private void EndChannel(Skill skill, ICombatEntity caster, bool confuseTargets)
		{
			if (!skill.Vars.GetBool(ChannelingKey))
				return;

			skill.Vars.Set(ChannelingKey, false);
			caster.RemoveBuff(BuffId.Fluting_Buff);
			PiedPiperSkillHelper.SafeStopSound(caster, SoundName);
			PiedPiperSkillHelper.SafeStopSound(caster, EndSoundName);
			PiedPiperSkillHelper.SafePlaySound(caster, EndSoundName);

			if (!confuseTargets)
				return;

			foreach (var enemy in PiedPiperSkillHelper.GetEnemiesInSongRange(caster, Range + 80).Where(e => e.IsBuffActive(BuffId.Fluting_DeBuff)).Take(MaxTargets))
			{
				enemy.RemoveBuff(BuffId.Fluting_DeBuff);
				PiedPiperSkillHelper.StartVisibleBuff(enemy, BuffId.Confuse, skill.Level, 0, TimeSpan.FromSeconds(ConfuseDurationSeconds), caster, skill.Id);
			}
		}

		private static bool CanHypnotize(ICombatEntity caster, ICombatEntity target)
		{
			if (target is not Mob mob || mob.Rank == MonsterRank.Boss)
				return false;

			return mob.Rank != MonsterRank.Elite || caster.IsAbilityActive(AbilityId.PiedPiper6);
		}
	}
}
