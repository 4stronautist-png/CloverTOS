using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Buffs;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.ProjectEpic
{
	// Generated from Project_Epic skilltree on 2026-05-06 06:54:10.
	// Covers missing handlers only; existing server handlers are intentionally not listed.
	// Classes: Alchemist, Appraiser, Bulletmarker, Cannoneer, Centurion, Chaplain, Cleric, Common, Corsair, Daoshi, Dragoon, Druid, Elementalist, Enchanter, Exorcist, Featherfoot, GM, Hackapell, Hunter, Inquisitor, Kabbalist, Linker, Mergen, Miko, Murmillo, Onmyoji, Pardoner, Peltasta, PiedPiper, PlagueDoctor, Priest, Psychokino, Pyromancer, Rancer, RuneCaster, Sage, Sapper, Scout, Shadowmancer, Shinobi, Squire, Templer, Warlock, Wizard, Wugushi, Zealot
	internal static class ProjectEpicBackfillHelper
	{
		public static bool TryStart(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			return true;
		}

		public static bool TryStartSelf(Skill skill, ICombatEntity caster, Position originPos)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return false;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);
			Send.ZC_SKILL_READY(caster, skill, ZoneServer.Instance.World.CreateSkillHandle(), originPos, Position.Zero);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, caster.Direction, Position.Zero);
			return true;
		}

		public static async Task AttackArea(ICombatEntity caster, Skill skill, Position originPos, Position farPos, int length = 110, int width = 50, float delay = 200)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: length, width: width, angle: 10f);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
			await SkillAttack(caster, skill, splashArea, hitDelay: 0, aniTime: delay, hits: new List<SkillHitInfo>());
		}

		public static async Task AttackSelfArea(ICombatEntity caster, Skill skill, int radius = 70, float delay = 200)
		{
			var splashParam = skill.GetSplashParameters(caster, caster.Position, caster.Position, length: 0, width: radius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);
			await SkillAttack(caster, skill, splashArea, hitDelay: 0, aniTime: delay, hits: new List<SkillHitInfo>());
		}

		public static void StartVisualGround(ICombatEntity caster, Skill skill, Position farPos)
			=> Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

		public static void StartVisualForceGround(ICombatEntity caster, Skill skill, Position farPos)
			=> Send.ZC_SKILL_FORCE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

		public static void StartVisualTarget(ICombatEntity caster, Skill skill, ICombatEntity target)
			=> Send.ZC_SKILL_FORCE_TARGET(caster, target, skill, null);

		public static void StartVisualSelf(ICombatEntity caster, Skill skill)
			=> Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

		public static bool TryApplyMatchingSelfBuff(Skill skill, ICombatEntity caster)
		{
			foreach (var candidate in GetSelfBuffCandidates(skill.Data.ClassName))
			{
				if (!ZoneServer.Instance.Data.BuffDb.TryFind(candidate, out var buffData))
					continue;

				caster.StartBuff(buffData.Id, skill.Level, 0, Buff.DefaultDuration, caster, skill.Id);

				if (caster is Character character)
				{
					Send.ZC_OBJECT_PROPERTY(character);
					Send.ZC_MOVE_SPEED(character);
				}

				return true;
			}

			return false;
		}

		private static IEnumerable<string> GetSelfBuffCandidates(string skillClassName)
		{
			yield return skillClassName + "_Buff";
			yield return skillClassName + "_Debuff";

			var separator = skillClassName.IndexOf('_');
			if (separator < 0 || separator + 1 >= skillClassName.Length)
				yield break;

			var shortName = skillClassName.Substring(separator + 1);
			yield return shortName + "_Buff";
			yield return shortName + "_Debuff";
		}
	}

	[Package("laima")]
	[SkillHandler(
		SkillId.Alchemist_Combustion,
		SkillId.Alchemist_Dig,
		SkillId.Alchemist_ItemAwakening,
		SkillId.Alchemist_MagnumOpus,
		SkillId.Alchemist_Roasting,
		SkillId.Alchemist_Tincturing,
		SkillId.Appraiser_Apprise,
		SkillId.Appraiser_Blindside,
		SkillId.Appraiser_Devaluation,
		SkillId.Appraiser_Forgery,
		SkillId.Appraiser_Overestimate,
		SkillId.Cannoneer_CannonBlast,
		SkillId.Cannoneer_SiegeBurst,
		SkillId.Cannoneer_SmokeGrenade,
		SkillId.Cannoneer_SweepingCannon,
		SkillId.Centurion_Conscript,
		SkillId.Centurion_PhalanxFormation,
		SkillId.Centurion_Rotate,
		SkillId.Centurion_SchiltronFormation,
		SkillId.Centurion_SpecialForceFormation,
		SkillId.Centurion_TercioFormation,
		SkillId.Centurion_Testudo,
		SkillId.Centurion_WedgeFormation,
		SkillId.Centurion_WingedFormation,
		SkillId.Chaplain_Aspergillum,
		SkillId.Chaplain_BuildCappella,
		SkillId.Chaplain_LastRites,
		SkillId.Chaplain_MagnusExorcismus,
		SkillId.Cleric_DeprotectedZone,
		SkillId.Common_ForcedAttackCancel,
		SkillId.Common_SummonRemove,
		SkillId.Daoshi_BegoneDemon,
		SkillId.Daoshi_CreepingDeath,
		SkillId.Daoshi_DarkSight,
		SkillId.Daoshi_DivinePunishment,
		SkillId.Daoshi_ElevateMagicSquare,
		SkillId.Daoshi_PhantomEradication,
		SkillId.Daoshi_StormCalling,
		SkillId.Daoshi_TriDisaster,
		SkillId.Dragoon_Dethrone,
		SkillId.Dragoon_Dragon_Soar,
		SkillId.Dragoon_DragonFall,
		SkillId.Dragoon_Dragontooth,
		SkillId.Dragoon_DragoonHelmet,
		SkillId.Dragoon_Gae_Bulg,
		SkillId.Dragoon_Serpentine,
		SkillId.Druid_Carnivory,
		SkillId.Druid_Chortasmata,
		SkillId.Druid_HengeStone,
		SkillId.Druid_Lycanthropy,
		SkillId.Druid_Seedbomb,
		SkillId.Druid_ShapeShifting,
		SkillId.Druid_StereaTrofh,
		SkillId.Druid_Telepath,
		SkillId.Druid_ThornVine,
		SkillId.Druid_Transform,
		SkillId.Enchanter_Agility,
		SkillId.Enchanter_EnchantArmor,
		SkillId.Enchanter_EnchantLightning,
		SkillId.Enchanter_LightningHands,
		SkillId.Enchanter_OverReinforce,
		SkillId.Exorcist_AquaBenedicta,
		SkillId.Exorcist_Katadikazo,
		SkillId.Exorcist_Koinonia,
		SkillId.Exorcist_Rubric,
		SkillId.Featherfoot_BloodBath,
		SkillId.Featherfoot_BloodCurse,
		SkillId.Featherfoot_BloodSucking,
		SkillId.Featherfoot_BonePointing,
		SkillId.Featherfoot_Enervation,
		SkillId.Featherfoot_KundelaSlash,
		SkillId.Featherfoot_Kurdaitcha,
		SkillId.Featherfoot_Levitation,
		SkillId.Featherfoot_Ngadhundi,
		SkillId.GM_ATKBuff,
		SkillId.GM_CooldownBuff,
		SkillId.GM_DEFBuff,
		SkillId.GM_RegenerateBuff,
		SkillId.GM_StatBuff,
		SkillId.Hackapell_CavalryCharge,
		SkillId.Hackapell_GrindCutter,
		SkillId.Hackapell_InfiniteAssault,
		SkillId.Hackapell_Skarphuggning,
		SkillId.Hackapell_StormBolt,
		SkillId.Hunter_Coursing,
		SkillId.Hunter_Hounding,
		SkillId.Hunter_Pointing,
		SkillId.Hunter_Retrieve,
		SkillId.Hunter_RushDog,
		SkillId.Hunter_Snatching,
		SkillId.Inquisitor_BreakingWheel,
		SkillId.Inquisitor_BreastRipper,
		SkillId.Inquisitor_GodSmash,
		SkillId.Inquisitor_IronMaiden,
		SkillId.Inquisitor_Judgment,
		SkillId.Inquisitor_MalleusMaleficarum,
		SkillId.Inquisitor_PearofAnguish,
		SkillId.Kabbalist_Ayin_sof,
		SkillId.Kabbalist_Clone,
		SkillId.Kabbalist_Gematria,
		SkillId.Kabbalist_Gevura,
		SkillId.Kabbalist_Merkabah,
		SkillId.Kabbalist_Multiple_Hit_Chance,
		SkillId.Kabbalist_Nachash,
		SkillId.Kabbalist_Notarikon,
		SkillId.Kabbalist_RevengedSevenfold,
		SkillId.Kabbalist_TheTreeOfSepiroth,
		SkillId.Mergen_ArrowRain,
		SkillId.Mergen_DownFall,
		SkillId.Mergen_FocusFire,
		SkillId.Mergen_JumpShot,
		SkillId.Mergen_ParthianShaft,
		SkillId.Mergen_TrickShot,
		SkillId.Mergen_Unload,
		SkillId.Mergen_Zenith,
		SkillId.Miko_Gohei,
		SkillId.Miko_Hamaya,
		SkillId.Miko_HoukiBroom,
		SkillId.Miko_KaguraDance,
		SkillId.Miko_Kasiwade,
		SkillId.Murmillo_CassisCrista,
		SkillId.Murmillo_EmperorsBane,
		SkillId.Murmillo_EvadeThrust,
		SkillId.Murmillo_Headbutt,
		SkillId.Murmillo_ScutumHit,
		SkillId.Onmyoji_FireFoxShikigami,
		SkillId.Onmyoji_GreenwoodShikigami,
		SkillId.Onmyoji_Toyou,
		SkillId.Onmyoji_WaterShikigami,
		SkillId.Onmyoji_WhiteTigerHowling,
		SkillId.Onmyoji_YinYangConsonance,
		SkillId.Pardoner_Oblation,
		SkillId.Pardoner_SpellShop,
		SkillId.Peltasta_ButterFly,
		SkillId.PlagueDoctor_BeakMask,
		SkillId.PlagueDoctor_Bloodletting,
		SkillId.PlagueDoctor_Disenchant,
		SkillId.PlagueDoctor_Fumigate,
		SkillId.PlagueDoctor_HealingFactor,
		SkillId.PlagueDoctor_Incineration,
		SkillId.PlagueDoctor_Methadone,
		SkillId.PlagueDoctor_Pandemic,
		SkillId.PlagueDoctor_PlagueVapours,
		SkillId.Priest_Exorcise,
		SkillId.Psychokino_Teleportation,
		SkillId.Pyromancer_Flare,
		SkillId.Rancer_Chage,
		SkillId.Rancer_Crush,
		SkillId.Rancer_GiganteMarcha,
		SkillId.Rancer_Joust,
		SkillId.Rancer_Quintain,
		SkillId.Rancer_SpillAttack,
		SkillId.RuneCaster_Algiz,
		SkillId.RuneCaster_Hagalaz,
		SkillId.RuneCaster_Isa,
		SkillId.RuneCaster_Thurisaz,
		SkillId.RuneCaster_Tiwaz,
		SkillId.Sage_Blink,
		SkillId.Sage_HoleOfDarkness,
		SkillId.Sage_MicroDimension,
		SkillId.Sage_MissileHole,
		SkillId.Sage_Portal,
		SkillId.Sage_PortalShop,
		SkillId.Sage_UltimateDimension,
		SkillId.Sapper_CollarBomb,
		SkillId.Sapper_Cover,
		SkillId.Sapper_StakeStockades,
		SkillId.Scout_FlareShot,
		SkillId.Scout_Scan,
		SkillId.Scout_Undistance,
		SkillId.Shadowmancer_InfernalShadow,
		SkillId.Shadowmancer_ShadowCondensation,
		SkillId.Shadowmancer_ShadowConjuration,
		SkillId.Shadowmancer_ShadowFatter,
		SkillId.Shinobi_Katon_no_jutsu,
		SkillId.Shinobi_Kunai,
		SkillId.Shinobi_Mijin_no_jutsu,
		SkillId.Shinobi_Mokuton_no_jutsu,
		SkillId.Squire_Camp,
		SkillId.Squire_EquipmentTouchUp,
		SkillId.Squire_FoodTable,
		SkillId.Squire_Repair,
		SkillId.Templer_BattleOrders,
		SkillId.Templer_BuildForge,
		SkillId.Templer_BuildGuildTower,
		SkillId.Templer_BuildShieldCharger,
		SkillId.Templer_MortalSlash,
		SkillId.Templer_NonInvasiveArea,
		SkillId.Templer_ReduceCraftTime,
		SkillId.Templer_ShareBuff,
		SkillId.Templer_SummonGuildMember,
		SkillId.Templer_WarpToGuildMember,
		SkillId.Warlock_DarkTheurge,
		SkillId.Warlock_DemonScratch,
		SkillId.Warlock_Drain,
		SkillId.Warlock_EvilSacrifice,
		SkillId.Warlock_Invocation,
		SkillId.Warlock_Mastema,
		SkillId.Warlock_PoleofAgony,
		SkillId.Wizard_Sleep,
		SkillId.Wizard_Surespell,
		SkillId.Wugushi_Detoxify,
		SkillId.Zealot_BeadyEyed,
		SkillId.Zealot_EmphasisTrust,
		SkillId.Zealot_FanaticIllusion,
		SkillId.Zealot_Fanaticism,
		SkillId.Zealot_Immolation,
		SkillId.Zealot_Invulnerable)]
	public class ProjectEpicMeleeGroundBackfill : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!ProjectEpicBackfillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			ProjectEpicBackfillHelper.StartVisualGround(caster, skill, farPos);
			skill.Run(ProjectEpicBackfillHelper.AttackArea(caster, skill, originPos, farPos));
		}
	}

	[Package("laima")]
	[SkillHandler(
		SkillId.Cannoneer_Bazooka,
		SkillId.Corsair_SubweaponCancel,
		SkillId.Enchanter_EnchantEarth,
		SkillId.Exorcist_Engkrateia,
		SkillId.Exorcist_Entity,
		SkillId.Exorcist_Gregorate,
		SkillId.Hackapell_HakkaPalle,
		SkillId.Murmillo_Sprint,
		SkillId.Onmyoji_GenbuArmor,
		SkillId.Rancer_Commence,
		SkillId.Rancer_Prevent,
		SkillId.Scout_Camouflage,
		SkillId.Shadowmancer_Hallucination,
		SkillId.Shadowmancer_ShadowPool,
		SkillId.Zealot_BlindFaith)]
	public class ProjectEpicSelfBackfill : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!ProjectEpicBackfillHelper.TryStartSelf(skill, caster, originPos))
				return;

			ProjectEpicBackfillHelper.StartVisualSelf(caster, skill);
			ProjectEpicBackfillHelper.TryApplyMatchingSelfBuff(skill, caster);
		}
	}

	[Package("laima")]
	[SkillHandler(
		SkillId.Alchemist_AlchemisticMissile,
		SkillId.Cannoneer_CannonBarrage,
		SkillId.Cannoneer_CannonShot,
		SkillId.Cannoneer_ShootDown,
		SkillId.Common_ForcedAttack,
		SkillId.Elementalist_FreezingSphere,
		SkillId.Linker_SpiritShock,
		SkillId.Scout_SplitArrow)]
	public class ProjectEpicForceBackfill : IForceSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!ProjectEpicBackfillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			ProjectEpicBackfillHelper.StartVisualTarget(caster, skill, target);
			skill.Run(ProjectEpicBackfillHelper.AttackArea(caster, skill, originPos, target?.Position ?? farPos));
		}
	}

	[Package("laima")]
	[SkillHandler(
		SkillId.Sage_DimensionCompression,
		SkillId.Shadowmancer_ShadowThorn)]
	public class ProjectEpicTargetGroundBackfill : ITargetGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!ProjectEpicBackfillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			ProjectEpicBackfillHelper.StartVisualGround(caster, skill, farPos);
			skill.Run(ProjectEpicBackfillHelper.AttackArea(caster, skill, originPos, farPos));
		}
	}

	[Package("laima")]
	[SkillHandler(
		SkillId.Scout_FluFlu,
		SkillId.Warlock_Sabbath)]
	public class ProjectEpicForceGroundBackfill : IForceGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!ProjectEpicBackfillHelper.TryStart(skill, caster, originPos, farPos))
				return;

			ProjectEpicBackfillHelper.StartVisualForceGround(caster, skill, farPos);
			skill.Run(ProjectEpicBackfillHelper.AttackArea(caster, skill, originPos, farPos));
		}
	}
}
