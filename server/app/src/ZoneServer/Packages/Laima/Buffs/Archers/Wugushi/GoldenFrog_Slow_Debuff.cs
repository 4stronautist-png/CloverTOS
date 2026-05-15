using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;

namespace Melia.Zone.Buffs.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Reduces movement speed while the target stays near Golden Frog.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.GoldenFrog_Slow_Debuff)]
	public class GoldenFrog_Slow_DebuffOverride : BuffHandler
	{
		private const float MoveSpeedReductionRate = 0.35f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_SHOW_EMOTICON(buff.Target, "I_emo_slowdown", buff.Duration);
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.GoldenFrog_Slow_Debuff, null);

			var reduction = buff.Target.Properties.GetFloat(PropertyName.MSPD) * MoveSpeedReductionRate;
			AddPropertyModifier(buff, buff.Target, PropertyName.MSPD_BM, -reduction);
			Send.ZC_MSPD(buff.Target);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.MSPD_BM);
			Send.ZC_MSPD(buff.Target);
		}
	}
}
