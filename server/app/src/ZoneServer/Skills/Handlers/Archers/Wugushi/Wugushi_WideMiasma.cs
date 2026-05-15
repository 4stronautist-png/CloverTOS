using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Handler for the Wugushi skill Wide Miasma.
	/// </summary>
	[SkillHandler(SkillId.Wugushi_WideMiasma)]
	public class Wugushi_WideMiasma : IGroundSkillHandler
	{
		private static readonly TimeSpan HemotoxicMiasmaDuration = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Handles the skill, insert debuffs to enemies inside of the effect area
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="originPos"></param>
		/// <param name="farPos"></param>
		/// <param name="targets"></param>
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			WugushiSkillHelper.ApplyPoisonMasteryIndicator(caster);

			var radius = WugushiSkillHelper.GetWideMiasmaRadius(caster);
			var stealthDuration = WugushiSkillHelper.GetWideMiasmaStealthDuration(caster);
			caster.StartBuff(BuffId.WideMiasma_Buff, skill.Level, 0, stealthDuration, caster);
			caster.StartBuff(BuffId.Hemotoxic_Miasma_Buff, skill.Level, 0, HemotoxicMiasmaDuration, caster, skill.Id);

			Send.ZC_SKILL_READY(caster, skill, caster.Position, caster.Position);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, caster.Handle, caster.Position, caster.Direction, farPos);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, caster.Position, null);
			Send.ZC_GROUND_EFFECT(caster, caster.Position, "F_archer_WideMiasma_ground_loop", Math.Max(0.1f, radius / 60f), Math.Max(3f, (float)stealthDuration.TotalSeconds));
		}
	}
}
