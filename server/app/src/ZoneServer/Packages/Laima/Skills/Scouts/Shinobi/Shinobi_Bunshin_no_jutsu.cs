using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Scouts.Shinobi
{
	/// <summary>
	/// Handles Bunshin no Jutsu by creating visible character clones.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Shinobi_Bunshin_no_jutsu)]
	public class Shinobi_Bunshin_no_jutsu : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (caster is not Character casterCharacter)
				return;

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			this.RemoveBunshinClones(casterCharacter);

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, originPos, ForceId.GetNew(), null);
			Send.ZC_PLAY_SOUND(casterCharacter, "skl_eff_bunshin");

			var cloneCount = Math.Clamp(skill.Level, 1, 5);
			var duration = TimeSpan.FromSeconds(20);

			for (var i = 0; i < cloneCount; ++i)
				this.CreateClone(casterCharacter, i, cloneCount, duration);

			casterCharacter.StartBuff(BuffId.Bunshin_Debuff, cloneCount, 0, duration, casterCharacter, skill.Id);
		}

		private void CreateClone(Character caster, int index, int cloneCount, TimeSpan duration)
		{
			var angle = -60f + (120f / Math.Max(1, cloneCount - 1)) * index;
			var front = caster.Position.GetRelative(caster.Direction, 100);
			var position = caster.Position.GetRelative(front, distance: 35, angle: angle);
			var clone = caster.Clone(position);

			Send.ZC_PLAY_ANI(clone, "BORN", false);
			Send.ZC_NORMAL.SetActorColor(clone, 255, 255, 255, 150, 0.1f);
			Send.ZC_NORMAL.Skill_DynamicCastStart(clone, SkillId.None);

			clone.StartBuff(BuffId.Bunshin_Buff, 1, 0, duration, caster);
		}

		private void RemoveBunshinClones(Character caster)
		{
			foreach (var clone in caster.Map.GetCharacters(c => c is DummyCharacter d && d.Owner == caster && d.IsBuffActive(BuffId.Bunshin_Buff)))
			{
				Send.ZC_LEAVE(clone);
				caster.Map.RemoveCharacter(clone);
			}
		}
	}
}
