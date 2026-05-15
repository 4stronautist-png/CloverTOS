using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Archers.PiedPiper
{
	[Package("laima")]
	[SkillHandler(SkillId.PiedPiper_HamelnNagetier)]
	public class PiedPiperHamelnNagetier : IPassiveSkillHandler, ISelfSkillHandler, ISkillCombatAttackAfterCalcHandler
	{
		public const string LastTargetVar = "Melia.PiedPiper.BestFriend.LastTarget";

		public void Handle(Skill skill, ICombatEntity caster)
		{
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
		}

		public void OnAttackAfterCalc(Skill skill, ICombatEntity attacker, ICombatEntity target, Skill attackerSkill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (attacker is not Character character || target == null || target.IsDead || skillHitResult.Damage <= 0)
				return;

			if (attackerSkill == null || attackerSkill.Data.ClassName.Contains("DOT", System.StringComparison.OrdinalIgnoreCase) || attackerSkill.Data.ClassName.Contains("Dot", System.StringComparison.OrdinalIgnoreCase))
				return;

			character.Variables.Temp.SetInt(LastTargetVar, target.Handle);
		}
	}
}
