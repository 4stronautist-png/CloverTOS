using System;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Actors.Pads;
using Yggdrasil.Logging;

namespace Melia.Zone.Pads.Handlers.Scouts.Assassin
{
	/// <summary>
	/// Handler for the Assassin_HallucinationSmoke,
	/// which creates a fog of... hallucinating... smoke?
	/// </summary>
	[Package("laima")]
	[PadHandler(PadName.Assassin_HallucinationSmoke)]
	public class Assassin_HallucinationSmokeOverride : ICreatePadHandler, IDestroyPadHandler, IEnterPadHandler, IUpdatePadHandler
	{
		/// <summary>
		/// Called when the pad is created.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		public void Created(object sender, PadTriggerArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;
			var skill = args.Skill;

			pad.SetUpdateInterval(1000);
			pad.Position = creator.Position;
			pad.Trigger.MaxConcurrentUseCount = 8;
			pad.Trigger.LifeTime = TimeSpan.FromSeconds(8);
			Send.ZC_NORMAL.PadUpdate(args.Creator, args.Trigger, PadName.Assassin_HallucinationSmoke, -0.7853982f, 0, 30, true);
		}

		public void Entered(object sender, PadTriggerActorArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;
			var target = args.Initiator;
			var skill = args.Skill;

			if (pad.Trigger.AtCapacity)
				return;

			if (!creator.CanDamage(target))
				return;

			pad.Trigger.ActivateCount++;
			this.ApplySmokeEffects(creator, target, skill);
		}

		public void Updated(object sender, PadTriggerArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;
			var skill = args.Skill;

			var targets = pad.Trigger.GetAttackableEntities(creator);

			foreach (var target in targets)
			{
				if (pad.Trigger.AtCapacity)
					return;

				if (target.IsDead)
					continue;

				pad.Trigger.ActivateCount++;
				this.ApplySmokeEffects(creator, target, skill);
			}
		}

		/// <summary>
		/// Called when the pad is destroyed.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		public void Destroyed(object sender, PadTriggerArgs args)
		{
			Send.ZC_NORMAL.PadUpdate(args.Creator, args.Trigger, PadName.Assassin_HallucinationSmoke, 0, 145.8735f, 30, false);
		}

		private void ApplySmokeEffects(ICombatEntity creator, ICombatEntity target, Skill skill)
		{
			target.StartBuff(BuffId.HallucinationSmoke_Debuff, skill.Level, 0, TimeSpan.FromSeconds(10), creator, skill.Id);

			if (!this.HasAbility(creator, AbilityId.Assassin18))
				return;

			if (creator is not Character character || character.Variables.Temp.Has("Melia.AssassinationTarget"))
				return;

			character.Variables.Temp.SetInt("Melia.AssassinationTarget", target.Handle);
			target.StartBuff(BuffId.Assassin_Target_Debuff, skill.Level, 0, TimeSpan.FromSeconds(10), creator, skill.Id);
		}

		private bool HasAbility(ICombatEntity caster, AbilityId abilityId)
			=> caster.IsAbilityActive(abilityId) || caster.GetAbilityLevel(abilityId) > 0;
	}
}
