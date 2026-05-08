using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Scouts.Assassin
{
	/// <summary>
		/// Handles Hasisas: attack speed, critical damage and the Accelerate Reaction evasion attribute.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Hasisas_Buff)]
	public class Hasisas_BuffOverride : BuffHandler
	{
		private const float CritAttackRateBase = 0.10f;
		private const float CritAttackRatePerLevel = 0.02f;
		private const float AttackSpeedBonus = -120f;
		private const float EvasionBonusRate = 0.20f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM, AttackSpeedBonus);
			AddPropertyModifier(buff, buff.Target, PropertyName.CRTDR_RATE_BM, this.GetCritDamageRate(buff));

			if (buff.NumArg2 > 0)
				AddPropertyModifier(buff, buff.Target, PropertyName.DR_BM, buff.Target.Properties.GetFloat(PropertyName.DR) * EvasionBonusRate);

			if (buff.Target is Character character)
			{
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTDR_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_BM);

			if (buff.Target is Character character)
			{
				Send.ZC_OBJECT_PROPERTY(character);
				Send.ZC_MOVE_SPEED(character);
			}
		}

		private float GetCritDamageRate(Buff buff)
		{
			var level = Math.Max(1, (int)buff.NumArg1);
			return CritAttackRateBase + CritAttackRatePerLevel * (level - 1);
		}
	}
}
