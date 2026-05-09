using System;
using System.Collections.Generic;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Clerics.Kneller
{
	[Package("laima")]
	[SkillHandler(SkillId.Kneller_RestingGround_Cleric)]
	public class Kneller_RestingGround_ClericOverride : IGroundSkillHandler, IDynamicCasted
	{
		private const float Radius = 70f;
		private const float PartyShieldRange = 300f;
		private const int TickIntervalMs = 750;
		private const int MaxTargets = 10;
		private const int MaxMourningStacksPerTarget = 4;
		private const int MourningTicks = 4;
		private static readonly TimeSpan Duration = TimeSpan.FromSeconds(5);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			originPos = caster.Position;
			farPos = caster.Position.GetRelative(caster.Direction, 70f);

			if (!KnellerSkillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			if (caster.IsAbilityActive(AbilityId.Kneller12))
				this.ApplyTombWall(skill, caster);

			var graveyardPos = farPos;
			KnellerSkillHelper.PlayGroundEffect(caster, "SKL_TELEKINESIS_THROW", graveyardPos, 2.4f, 5000f);
			var ticks = Math.Max(1, skill.Data.MultiHitCount);
			var mourningStacks = new Dictionary<long, int>();
			KnellerSkillHelper.RunAndReset(skill, caster, () => this.AttackGraveyard(skill, caster, graveyardPos, ticks, mourningStacks));
		}

		private async System.Threading.Tasks.Task AttackGraveyard(Skill skill, ICombatEntity caster, Position graveyardPos, int ticks, Dictionary<long, int> mourningStacks)
		{
			for (var tick = 0; tick < ticks; tick++)
			{
				var currentTick = tick;
				KnellerSkillHelper.PlayGroundEffect(caster, "SKL_TELEKINESIS_THROW", graveyardPos, 2.4f, Math.Max(900f, TickIntervalMs));

				await KnellerSkillHelper.AttackArea(skill, caster, new Yggdrasil.Geometry.Shapes.CircleF(graveyardPos, Radius),
					afterHit: (hitTarget, result) =>
					{
						if (result.Damage <= 0)
							return;

						mourningStacks.TryGetValue(hitTarget.Handle, out var stacks);
						if (currentTick < MourningTicks && stacks < MaxMourningStacksPerTarget)
						{
							KnellerSkillHelper.ApplyMourning(caster, hitTarget, skill);
							mourningStacks[hitTarget.Handle] = stacks + 1;
						}

						if (caster.IsAbilityActive(AbilityId.Kneller10))
							KnellerSkillHelper.ApplyFrostGrave(caster, hitTarget);
					},
					hitCount: 1,
					maxTargets: MaxTargets);

				if (tick < ticks - 1)
					await skill.Wait(TimeSpan.FromMilliseconds(TickIntervalMs));
			}
		}

		private void ApplyTombWall(Skill skill, ICombatEntity caster)
		{
			caster.StartBuff(BuffId.RestingGround_Buff, skill.Level, 0, Duration, caster, skill.Id);

			if (caster is not Character character || character.Connection.Party == null)
				return;

			var members = caster.Map.GetPartyMembersInRange(character, PartyShieldRange, true);
			foreach (var member in members)
			{
				if (member == caster)
					continue;

				member.StartBuff(BuffId.RestingGround_Buff, skill.Level, 0, Duration, caster, skill.Id);
			}
		}
	}
}
