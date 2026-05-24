using System;
using System.Globalization;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Handler for the Necromancer skill Corpse Tower.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_CorpseTower)]
	public class Necromancer_CorpseTowerOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (caster is not Character owner)
				return;
			NecromancerSkillHelper.SyncCorpseParts(owner);
			if (!NecromancerSkillHelper.HasCorpseParts(owner, NecromancerSkillHelper.FleshAmalgamCorpsePartsCost))
			{
				caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
				return;
			}
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

			skill.Run(this.HandleSkill(caster, skill, originPos, farPos, target));
		}

		private async Task HandleSkill(ICombatEntity caster, Skill skill, Position originPos, Position farPos, ICombatEntity target)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(1000));
			if (caster is not Character character)
				return;

			NecromancerSkillHelper.SyncCorpseParts(character);
			if (!NecromancerSkillHelper.HasCorpseParts(character, NecromancerSkillHelper.FleshAmalgamCorpsePartsCost))
			{
				caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
				return;
			}

			var spawnPos = originPos.GetRelative(farPos, distance: 20f);
			var summonControlScale = 1f + Math.Clamp(NecromancerSkillHelper.GetSummonControlPercent(character), 0f, 100f) / 100f;
			var scaleText = summonControlScale.ToString("0.###", CultureInfo.InvariantCulture);
			var mob = MonsterSkillCreateMob(skill, caster, "pcskill_CorpseTower", spawnPos, 0f, "Flesh Amalgam", "BasicMonster_ATK", 0, 180f, "None", $"WlkMSPD#0#RunMSPD#0#Scale#{scaleText}");
			if (mob is Summon summon)
			{
				if (!NecromancerSkillHelper.TrySpendCorpseParts(character, NecromancerSkillHelper.FleshAmalgamCorpsePartsCost))
				{
					summon.TakeDamage(Math.Max(1, summon.Hp), character);
					caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
					return;
				}

				NecromancerSkillHelper.MarkNecromancerSummon(summon);
				summon.Vars.SetBool("Melia.Necromancer.FleshAmalgam", true);
				summon.Vars.SetBool("Melia.AI.Stationary", true);
				summon.Vars.SetInt("Melia.Necromancer.FleshAmalgam.Blocks", 4 + Math.Max(1, skill.Level));
				summon.Tendency = TendencyType.Peaceful;
				summon.Properties.SetFloat(PropertyName.FixedLife, character.MaxHp * 0.5f);
				NecromancerSkillHelper.ApplySummonAttackStats(summon, NecromancerSkillHelper.GetOwnerMagicAttack(character) * (0.45f * Math.Max(1, skill.Level)));
				summon.Properties.InvalidateAll();
				summon.Properties.SetFloat(PropertyName.HP, summon.MaxHp);
				summon.PlayGroundEffect("F_burstup003_poison", 1.3f, 0f);
				summon.PlayEffect("F_light034_green", 1.1f);
				if (target != null && !target.IsDead)
					summon.InsertHate(target, 5000);
				Send.ZC_UPDATE_ALL_STATUS(summon, 0);
			}
			else
			{
				caster.ServerMessage(Localization.Get("Could not summon Flesh Amalgam."));
			}
		}
	}
}
