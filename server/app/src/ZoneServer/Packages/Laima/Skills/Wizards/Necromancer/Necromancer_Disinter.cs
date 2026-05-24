using System;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Handler for the Necromancer skill Disinter.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_Disinter)]
	public class Necromancer_DisinterOverride : ISelfSkillHandler, IGroundSkillHandler, IDynamicCasted
	{
		private static readonly TimeSpan CastTime = TimeSpan.FromSeconds(6);
		private const int FixedCastReleaseGraceMs = 350;
		private const string CastStartedAtKey = "Melia.Necromancer.UntilDeath.CastStartedAt";
		private const string SkipNextHandleKey = "Melia.Necromancer.UntilDeath.SkipNextHandle";
		private const string ServerCastTokenKey = "Melia.Necromancer.UntilDeath.ServerCastToken";

		public void StartDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			caster.ClearTargets();
			skill.Vars.SetString(CastStartedAtKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
			skill.Vars.SetBool(SkipNextHandleKey, false);
			Send.ZC_NORMAL.SkillChangeAnimation(caster, SkillId.Elementalist_Meteor);
			caster.PlaySound("voice_wiz_meteor_cast", "voice_wiz_m_meteor_cast");
		}

		public void EndDynamicCast(Skill skill, ICombatEntity caster, float maxCastTime)
		{
			skill.Vars.SetBool(SkipNextHandleKey, true);
			caster.StopSound("voice_wiz_meteor_cast");
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
			=> this.ApplyUntilDeath(skill, caster, originPos, dir);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
			=> this.ApplyUntilDeath(skill, caster, originPos, originPos.GetDirection(farPos));

		private void ApplyUntilDeath(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (caster is not Character character)
				return;

			if (!this.HasStartedCast(skill))
			{
				this.BeginServerCast(skill, character, originPos, dir);
				return;
			}

			if (this.ConsumeSkipNextHandle(skill))
			{
				if (!this.HasCompletedFixedCast(skill))
				{
					Send.ZC_SKILL_CAST_CANCEL(caster);
					return;
				}
			}
			else if (!this.HasCompletedFixedCast(skill))
				return;

			this.FinishUntilDeath(skill, character, originPos, dir);
		}

		private void BeginServerCast(Skill skill, Character caster, Position originPos, Direction dir)
		{
			var token = Guid.NewGuid().ToString("N");
			skill.Vars.SetString(ServerCastTokenKey, token);
			skill.Vars.SetString(CastStartedAtKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
			skill.Vars.SetBool(SkipNextHandleKey, false);
			caster.ClearTargets();
			Send.ZC_NORMAL.SkillChangeAnimation(caster, SkillId.Elementalist_Meteor);
			caster.PlaySound("voice_wiz_meteor_cast", "voice_wiz_m_meteor_cast");
			skill.Run(this.FinishServerCastAfterDelay(skill, caster, originPos, dir, token));
		}

		private async Task FinishServerCastAfterDelay(Skill skill, Character caster, Position originPos, Direction dir, string token)
		{
			await skill.Wait(CastTime);
			if (caster.IsDead || skill.Vars.GetString(ServerCastTokenKey) != token)
				return;

			caster.StopSound("voice_wiz_meteor_cast");
			this.FinishUntilDeath(skill, caster, originPos, dir);
		}

		private void FinishUntilDeath(Skill skill, Character caster, Position originPos, Direction dir)
		{
			skill.Vars.Remove(CastStartedAtKey);
			skill.Vars.Remove(ServerCastTokenKey);
			NecromancerSkillHelper.SyncCorpseParts(caster);
			if (NecromancerSkillHelper.GetCorpseParts(caster) <= 0)
			{
				caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
				return;
			}
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, originPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, caster.Handle, originPos, dir, Position.Zero);
			if (!NecromancerSkillHelper.ApplyUntilDeath(caster, skill))
				caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
		}

		private bool HasStartedCast(Skill skill)
			=> !string.IsNullOrEmpty(skill.Vars.GetString(CastStartedAtKey));

		private bool HasCompletedFixedCast(Skill skill)
		{
			if (!long.TryParse(skill.Vars.GetString(CastStartedAtKey), out var startedAt))
				return false;

			var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startedAt;
			return elapsedMs >= CastTime.TotalMilliseconds - FixedCastReleaseGraceMs;
		}

		private bool ConsumeSkipNextHandle(Skill skill)
		{
			if (!skill.Vars.GetBool(SkipNextHandleKey, false))
				return false;

			skill.Vars.SetBool(SkipNextHandleKey, false);
			return true;
		}
	}
}
