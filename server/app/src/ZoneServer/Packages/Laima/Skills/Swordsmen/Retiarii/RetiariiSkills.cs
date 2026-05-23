using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.Helpers.SkillDamageHelper;

namespace Melia.Zone.Skills.Handlers.Swordsmen.Retiarii
{
	internal static class RetiariiSkillHelper
	{
		public static bool StartGroundSkill(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
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
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, ForceId.GetNew(), null);

			return true;
		}

		public static bool StartSelfSkill(Skill skill, ICombatEntity caster, Position originPos)
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
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			return true;
		}

		public static async Task AreaAttack(ICombatEntity caster, Skill skill, Position originPos, Position farPos, int length = 115, int width = 45, float delay = 200, BuffId debuffId = BuffId.None, TimeSpan? debuffDuration = null)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: length, width: width, angle: 10f);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);

			await SkillAttack(caster, skill, splashArea, hitDelay: 0, aniTime: delay, hits: new List<SkillHitInfo>());

			if (debuffId == BuffId.None)
				return;

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea, hitType: skill.Data.HitType);
			foreach (var target in targets.LimitBySDR(caster, skill))
				target.StartBuff(debuffId, skill.Level, 0, debuffDuration ?? TimeSpan.FromSeconds(5 + skill.Level), caster);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_FishingNetsDraw)]
	public class Retiarii_FishingNetsDrawOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!RetiariiSkillHelper.StartGroundSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(RetiariiSkillHelper.AreaAttack(caster, skill, originPos, farPos, debuffId: BuffId.FishingNetsDraw_Debuff, debuffDuration: TimeSpan.FromSeconds(5 + skill.Level)));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_ThrowingFishingNet)]
	public class Retiarii_ThrowingFishingNetOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!RetiariiSkillHelper.StartGroundSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(RetiariiSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 160, width: 45, debuffId: BuffId.ThrowingFishingNet_Debuff, debuffDuration: TimeSpan.FromSeconds(5 + skill.Level)));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_DaggerGuard)]
	public class Retiarii_DaggerGuardOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!RetiariiSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			caster.StartBuff(BuffId.DaggerGuard_Buff, skill.Level, 0, TimeSpan.FromSeconds(20), caster, skill.Id);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_TridentFinish)]
	public class Retiarii_TridentFinishOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!RetiariiSkillHelper.StartGroundSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(RetiariiSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 110, width: 45, delay: 200));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_EquipDesrption)]
	public class Retiarii_EquipDesrptionOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!RetiariiSkillHelper.StartGroundSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(RetiariiSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 100, width: 45, delay: 200, debuffId: BuffId.EquipDesrption_Debeff, debuffDuration: TimeSpan.FromSeconds(6 + skill.Level)));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_DaggerFinish)]
	public class Retiarii_DaggerFinishOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!RetiariiSkillHelper.StartGroundSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(RetiariiSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 100, width: 45, delay: 200));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_VitalPointProtection)]
	public class Retiarii_VitalPointProtectionOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!RetiariiSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			caster.StartBuff(BuffId.VitalProtection_Buff, skill.Level, 0, TimeSpan.FromSeconds(10 + skill.Level), caster, skill.Id);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Retiarii_BlandirCadena)]
	public class Retiarii_BlandirCadenaOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!RetiariiSkillHelper.StartGroundSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(RetiariiSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 125, width: 55, delay: 200));
		}
	}
}
