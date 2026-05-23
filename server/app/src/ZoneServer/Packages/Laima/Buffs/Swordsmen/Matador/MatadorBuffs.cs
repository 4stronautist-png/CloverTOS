using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Swordsmen.Matador
{
	[Package("laima")]
	[BuffHandler(BuffId.Capote_Buff)]
	public class Capote_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM, 0.10f + buff.NumArg1 * 0.01f);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Capote_Debuff)]
	public class Capote_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			if (buff.Target is not Character)
				return;

			var penalty = 0.05f + buff.NumArg1 * 0.005f;
			AddPropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM, -penalty);
			AddPropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM, -penalty);
		}

		public override void OnEnd(Buff buff)
		{
			if (buff.Target is not Character)
				return;

			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Muleta_Buff)]
	public class Muleta_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM, 0.15f + buff.NumArg1 * 0.01f);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Ole_Buff)]
	public class Ole_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.CRTHR_RATE_BM, 0.05f + buff.NumArg1 * 0.005f);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTHR_RATE_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.Ole_Debuff)]
	public class Ole_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			if (buff.Target is not Character)
				return;

			AddPropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM, -0.10f);
		}

		public override void OnEnd(Buff buff)
		{
			if (buff.Target is not Character)
				return;

			RemovePropertyModifier(buff, buff.Target, PropertyName.DR_RATE_BM);
		}
	}
}
