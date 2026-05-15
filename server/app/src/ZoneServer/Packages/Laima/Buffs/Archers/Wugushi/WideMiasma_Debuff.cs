using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;

namespace Melia.Zone.Buffs.Handlers.Archers.Wugushi
{
	/// <summary>
	/// Healing reduction applied by Wide Miasma.
	/// </summary>
	/// <remarks>
	/// NumArg1: Skill level
	/// NumArg2: Heal reduction percentage in thousands
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.WideMiasma_Debuff)]
	public class WideMiasma_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			Send.ZC_NORMAL.PlayTextEffect(buff.Target, buff.Caster, "SHOW_BUFF_TEXT", (float)BuffId.WideMiasma_Debuff, null);
			buff.NotifyUpdate();
		}
	}
}
