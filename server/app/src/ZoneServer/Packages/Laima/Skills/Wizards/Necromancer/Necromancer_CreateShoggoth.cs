using System;
using System.Linq;
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
using Yggdrasil.Util;

namespace Melia.Zone.Skills.Handlers.Wizards.Necromancer
{
	/// <summary>
	/// Handler for the Necromancer skill Create Shoggoth.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Necromancer_CreateShoggoth)]
	public class Necromancer_CreateShoggothOverride : IGroundSkillHandler
	{
		protected TimeSpan DamageDelay { get; } = TimeSpan.FromMilliseconds(300);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (caster is not Character owner)
				return;
			NecromancerSkillHelper.SyncCorpseParts(owner);
			if (!NecromancerSkillHelper.HasCorpseParts(owner, NecromancerSkillHelper.CreateShoggothCorpsePartsCost))
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
			if (caster.IsAbilityActive(AbilityId.Necromancer8))
				skill.StartCooldown(TimeSpan.FromMinutes(2));
			caster.SetAttackState(true);

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

			skill.Run(this.HandleSkill(caster, skill, originPos, farPos));
		}

		private async Task HandleSkill(ICombatEntity caster, Skill skill, Position originPos, Position farPos)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(1500));
			if (caster is not Character owner)
				return;

			NecromancerSkillHelper.SyncCorpseParts(owner);
			if (!NecromancerSkillHelper.HasCorpseParts(owner, NecromancerSkillHelper.CreateShoggothCorpsePartsCost))
			{
				caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
				return;
			}

			var spawnPos = originPos.GetRelative(farPos, distance: 80f);
			foreach (var existing in NecromancerSkillHelper.GetNecromancerSummons(owner).Where(s => s.Id == NecromancerSkillHelper.ShoggothId && !s.IsDead))
				existing.TakeDamage(Math.Max(1, existing.Hp), owner);

			var enlargementLevel = caster.GetAbilityLevel(AbilityId.Necromancer8);
			var enlarged = enlargementLevel > 0 && RandomProvider.Get().NextDouble() < Math.Min(1.0, enlargementLevel * 0.005);
			var properties = enlarged
				? "WlkMSPD#80#RunMSPD#80#Scale#2#$NECRO_MON#1"
				: "WlkMSPD#120#RunMSPD#120#$NECRO_MON#1";
			var lifeTime = enlarged ? 300f : 900f;

			var mob = MonsterSkillCreateMob(skill, caster, "pcskill_shogogoth", spawnPos, 0f, "", "PC_Summon", 0, lifeTime, "None", properties);
			if (mob is Summon summon)
			{
				if (!NecromancerSkillHelper.TrySpendCorpseParts(owner, NecromancerSkillHelper.CreateShoggothCorpsePartsCost))
				{
					summon.TakeDamage(Math.Max(1, summon.Hp), owner);
					caster.ServerMessage(Localization.Get("Not enough Corpse Parts."));
					return;
				}

				NecromancerSkillHelper.MarkNecromancerSummon(summon);
				summon.Vars.SetBool("Melia.Necromancer.Shoggoth.Enlarged", enlarged);
				summon.Vars.SetFloat("Melia.Necromancer.DevourChance", 0.02f);
				this.ApplyNecronomiconCardRace(owner, summon);
				summon.StartBuff(BuffId.Ability_buff_PC_Summon, TimeSpan.Zero, summon);
				var attack = NecromancerSkillHelper.GetOwnerMagicAttack(owner) * (1f + Math.Max(1, skill.Level) * 0.10f);
				if (enlarged)
					attack *= 2f;
				NecromancerSkillHelper.ApplySummonAttackStats(summon, attack);
				summon.Properties.SetFloat(PropertyName.FixedLife, Math.Max(1f, owner.MaxHp * (enlarged ? 2f : 1f)));
				summon.Properties.InvalidateAll();
				summon.Properties.SetFloat(PropertyName.HP, summon.MaxHp);
				Send.ZC_UPDATE_ALL_STATUS(summon, 0);
			}
			else
			{
				caster.ServerMessage(Localization.Get("Could not summon Shoggoth."));
			}
		}

		private void ApplyNecronomiconCardRace(Character owner, Summon summon)
		{
			var etc = owner.Etc.Properties;
			for (var slot = 1; slot <= 4; slot++)
			{
				var cardClassId = slot switch
				{
					1 => (int)etc.GetFloat(PropertyName.Necro_bosscard1),
					2 => (int)etc.GetFloat(PropertyName.Necro_bosscard2),
					3 => (int)etc.GetFloat(PropertyName.Necro_bosscard3),
					4 => (int)etc.GetFloat(PropertyName.Necro_bosscard4),
					_ => 0,
				};

				if (cardClassId <= 0)
					continue;

				var card = owner.Inventory.FindItem(item => item.Id == cardClassId && item.Data.Group == ItemGroup.Card);
				if (card == null)
					continue;

				var monsterId = (int)card.Data.Script.NumArg1;
				if (!ZoneServer.Instance.Data.MonsterDb.TryFind(monsterId, out var monsterData))
					continue;

				summon.Vars.SetInt("Melia.OverrideRace", (int)monsterData.Race);
				return;
			}
		}
	}
}
