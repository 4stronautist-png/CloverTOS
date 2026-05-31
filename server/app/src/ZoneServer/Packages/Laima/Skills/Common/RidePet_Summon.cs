using System;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Buffs;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Zone.Skills.Handlers.Common
{
	[Package("laima")]
	[SkillHandler(SkillId.RidePet_Summon)]
	public class RidePet_SummonOverride : ISelfSkillHandler, IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (caster is Character character)
				this.ToggleMount(skill, character, originPos);
		}

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (caster is Character character)
				this.ToggleMount(skill, character, originPos);
		}

		private void ToggleMount(Skill skill, Character character, Position originPos)
		{
			if (character.IsRiding)
			{
				character.RemoveBuff(BuffId.RidingCompanion);
				return;
			}

			var companion = this.GetMountCompanion(character);
			if (companion == null)
			{
				character.ServerMessage(Localization.Get("Mount is not selected."));
				return;
			}

			if (!character.CanMount())
			{
				character.ServerMessage(Localization.Get("You cannot mount right now."));
				return;
			}

			if (!companion.IsActivated)
				companion.SetCompanionState(true);

			if (companion.IsDead || companion.IsBird)
				return;

			if (!character.TrySpendSp(skill))
			{
				character.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			Send.ZC_SKILL_MELEE_TARGET(character, skill, character);

			character.StartBuff(BuffId.RidingCompanion, TimeSpan.Zero, companion);
			Send.ZC_NORMAL.RidePet(character.Connection, character, companion);
		}

		private Companion GetMountCompanion(Character character)
		{
			var active = character.Companions.ActiveGroundCompanion;
			if (active != null && active.CompanionData.CanRide)
				return active;

			var account = character.Connection?.Account;
			if (account?.Properties.GetFloat(PropertyName.RidePet_13) > 0
				&& character.Companions.TryGetCompanion(c => c.Data.ClassName == "RidePet_ep14rider_1", out var davidson))
			{
				return davidson;
			}

			return character.Companions.GetList()
				.FirstOrDefault(c => c.CompanionData.CanRide && !c.IsBird);
		}
	}
}
