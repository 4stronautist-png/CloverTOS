using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Scouts.Shinobi
{
	/// <summary>
	/// Keeps Bunshin clones tied to the owner's buff lifetime.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Bunshin_Debuff)]
	public class Bunshin_DebuffOverride : BuffHandler
	{
		public override void OnEnd(Buff buff)
		{
			if (buff.Target is not Character character || character.Map == null)
				return;

			foreach (var clone in character.Map.GetCharacters(c => c is DummyCharacter d && d.Owner == character && d.IsBuffActive(BuffId.Bunshin_Buff)))
			{
				Send.ZC_LEAVE(clone);
				character.Map.RemoveCharacter(clone);
			}
		}
	}
}
