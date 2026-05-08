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

namespace Melia.Zone.Skills.Handlers.Swordsmen.Matador
{
	internal static class MatadorSkillHelper
	{
		public static bool StartSkill(Skill skill, ICombatEntity caster, Position originPos, Position farPos)
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

		public static async Task AreaAttack(ICombatEntity caster, Skill skill, Position originPos, Position farPos, int length = 110, int width = 50, float delay = 200)
		{
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: length, width: width, angle: 10f);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);
			await SkillAttack(caster, skill, splashArea, hitDelay: 0, aniTime: delay, hits: new List<SkillHitInfo>());
		}

		public static async Task CircleAttack(ICombatEntity caster, Skill skill, float radius = 55, float delay = 200)
		{
			var splashParam = skill.GetSplashParameters(caster, caster.Position, caster.Position, length: 0, width: radius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);
			await SkillAttack(caster, skill, splashArea, hitDelay: 0, aniTime: delay, hits: new List<SkillHitInfo>());
		}

		public static void ApplyDebuffAround(ICombatEntity caster, Skill skill, BuffId buffId, TimeSpan duration, float radius = 70)
		{
			var splashParam = skill.GetSplashParameters(caster, caster.Position, caster.Position, length: 0, width: radius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);
			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea, hitType: skill.Data.HitType);

			foreach (var target in targets.LimitBySDR(caster, skill))
				target.StartBuff(buffId, skill.Level, 0, duration, caster);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_Capote)]
	public class Matador_CapoteOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!MatadorSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			caster.StartBuff(BuffId.Capote_Buff, skill.Level, 0, TimeSpan.FromSeconds(8 + skill.Level), caster, skill.Id);
			MatadorSkillHelper.ApplyDebuffAround(caster, skill, BuffId.Capote_Debuff, TimeSpan.FromSeconds(8 + skill.Level));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_Muleta)]
	public class Matador_MuletaOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!MatadorSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			caster.StartBuff(BuffId.Muleta_Buff, skill.Level, 0, TimeSpan.FromSeconds(8 + skill.Level), caster, skill.Id);
			skill.Run(MatadorSkillHelper.CircleAttack(caster, skill, radius: 55, delay: 200));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Muleta_Attack)]
	public class Matador_Muleta_AttackOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!MatadorSkillHelper.StartSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(MatadorSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 80, width: 55, delay: 160));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_Faena)]
	public class Matador_FaenaOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!MatadorSkillHelper.StartSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(MatadorSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 90, width: 80, delay: 200));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_Ole)]
	public class Matador_OleOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!MatadorSkillHelper.StartSkill(skill, caster, originPos, farPos))
				return;

			caster.StartBuff(BuffId.Ole_Buff, skill.Level, 0, TimeSpan.FromSeconds(5 + skill.Level), caster, skill.Id);
			MatadorSkillHelper.ApplyDebuffAround(caster, skill, BuffId.Ole_Debuff, TimeSpan.FromSeconds(5 + skill.Level), radius: 70);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_PasoDoble)]
	public class Matador_PasoDobleOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!MatadorSkillHelper.StartSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(MatadorSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 115, width: 50, delay: 400));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_BackSlide)]
	public class Matador_BackSlideOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!MatadorSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			caster.StartBuff(BuffId.Capote_Buff, skill.Level, 0, TimeSpan.FromSeconds(3), caster, skill.Id);
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_CorridaFinale)]
	public class Matador_CorridaFinaleOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!MatadorSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			skill.Run(MatadorSkillHelper.CircleAttack(caster, skill, radius: 80, delay: 300));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_CorridaFinale_Hidden)]
	public class Matador_CorridaFinale_HiddenOverride : ISelfSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!MatadorSkillHelper.StartSelfSkill(skill, caster, originPos))
				return;

			skill.Run(MatadorSkillHelper.CircleAttack(caster, skill, radius: 80, delay: 300));
		}
	}

	[Package("laima")]
	[SkillHandler(SkillId.Matador_Muleta_Faena)]
	public class Matador_Muleta_FaenaOverride : IGroundSkillHandler, IDynamicCasted
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!MatadorSkillHelper.StartSkill(skill, caster, originPos, farPos))
				return;

			skill.Run(MatadorSkillHelper.AreaAttack(caster, skill, originPos, farPos, length: 90, width: 80, delay: 200));
		}
	}
}
