using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Scouts.Bulletmarker;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Scouts.Bulletmarker
{
	/// <summary>
	/// Switches the player's main attack while Double Gun Stance is active.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.DoubleGunStance_Buff)]
	public class DoubleGunStance_BuffOverride : BuffHandler
	{
		private const float MovingShotBonus = 1.5f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.MovingShot_BM, MovingShotBonus);

			if (buff.Target is Character character)
			{
				Send.ZC_NORMAL.SetMainAttackSkill(character, SkillId.DoubleGun_Attack);
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.MovingShot_BM);
			buff.Target.RemoveBuff(BuffId.Overheating_Buff);

			if (buff.Target is Character character)
			{
				Send.ZC_NORMAL.SetMainAttackSkill(character, SkillId.None);
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.DoubleGunStance_Buff)]
		public void OnDoubleGunAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (skill.Id != SkillId.DoubleGun_Attack)
				return;

			modifier.HitCount += 1;
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.DoubleGunStance_Buff)]
		public void OnDoubleGunAttackAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult result)
		{
			if (skill.Id != SkillId.DoubleGun_Attack || result.Damage <= 0)
				return;

			if (attacker.IsBuffActive(BuffId.Outrage_Buff))
				return;

			BulletmarkerSkillHelper.AddOverheating(attacker, SkillId.Bulletmarker_DoubleGunStance);
		}
	}
}
