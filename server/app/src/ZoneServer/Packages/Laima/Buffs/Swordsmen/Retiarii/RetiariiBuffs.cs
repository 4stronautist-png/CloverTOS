using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Components;

namespace Melia.Zone.Buffs.Handlers.Swordsmen.Retiarii
{
	[Package("laima")]
	[BuffHandler(BuffId.FishingNetsDraw_Debuff)]
	public class FishingNetsDraw_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.AddState(StateType.Held);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveState(StateType.Held);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.ThrowingFishingNet_Debuff)]
	public class ThrowingFishingNet_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.AddState(StateType.Held);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveState(StateType.Held);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.FishingNetsDrawSilence_Debuff)]
	public class FishingNetsDrawSilence_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.AddState(StateType.Silenced);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveState(StateType.Silenced);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.ThrowingFishingNetSilence_Debuff)]
	public class ThrowingFishingNetSilence_DebuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			buff.Target.AddState(StateType.Silenced);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Target.RemoveState(StateType.Silenced);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.DaggerGuard_Buff)]
	public class DaggerGuard_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.BLK_BM, 20 + buff.NumArg1 * 5);
			AddPropertyModifier(buff, buff.Target, PropertyName.BLK_RATE_BM, 0.10f + buff.NumArg1 * 0.01f);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.BLK_BM);
			RemovePropertyModifier(buff, buff.Target, PropertyName.BLK_RATE_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.EquipDesrption_Debeff)]
	public class EquipDesrption_DebeffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.DEF_RATE_BM, -(0.10f + buff.NumArg1 * 0.01f));
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.DEF_RATE_BM);
		}
	}

	[Package("laima")]
	[BuffHandler(BuffId.VitalProtection_Buff)]
	public class VitalProtection_BuffOverride : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			AddPropertyModifier(buff, buff.Target, PropertyName.CRTDR_RATE_BM, 0.25f + buff.NumArg1 * 0.02f);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.CRTDR_RATE_BM);
		}
	}
}
