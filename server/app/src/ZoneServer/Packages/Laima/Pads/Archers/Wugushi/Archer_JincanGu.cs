using System;
using System.Linq;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Network;
using Melia.Zone.Pads.Handlers;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Actors.Pads;
using static Melia.Zone.Pads.Helpers.PadHelper;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Pads.HandlersOverride.Archers.Wugushi
{
	[Package("laima")]
	[PadHandler(PadName.Archer_JincanGu)]
	public class Archer_JincanGuOverride : ICreatePadHandler, IDestroyPadHandler, IEnterPadHandler, ILeavePadHandler, IUpdatePadHandler
	{
		private const int MaxTargets = 4;
		private const string TutuMonsterClassName = "pcskill_tutu";
		private const string FadeStartedVar = "Melia.GoldenFrog.FadeStarted";
		private static readonly TimeSpan SlowRefreshDuration = TimeSpan.FromMilliseconds(1500);

		public void Created(object sender, PadTriggerArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;

			Send.ZC_NORMAL.PadUpdate(creator, pad, PadName.Archer_JincanGu, 0f, 0f, 30f, true);
			pad.SetRange(100f);
			pad.SetUpdateInterval(750);
			pad.Trigger.LifeTime = TimeSpan.FromMilliseconds(10000);

			var monster = PadCreateMonster(pad, TutuMonsterClassName, pad.Position, 0f, 0, 10f, "HitProof#YES", "None", 1, true, "None", "None", false, "SET_PVE_NODAMAGE");
			if (monster is Mob mob)
			{
				mob.SetHittable(false);
				mob.MonsterType = RelationType.Friendly;
				mob.Faction = FactionType.Law;
				mob.StartBuff(BuffId.Invincible);
			}
		}

		public void Destroyed(object sender, PadTriggerArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;

			Send.ZC_NORMAL.PadUpdate(creator, pad, PadName.Archer_JincanGu, 0f, 0f, 30f, false);
		}

		public void Entered(object sender, PadTriggerActorArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;
			var initiator = args.Initiator;
			var skill = pad.Skill;

			if (!creator.IsEnemy(initiator))
				return;

			this.ApplySlow(creator, initiator);

			if (initiator.IsBuffActive(BuffId.JincanGu_Abil_Debuff))
				return;

			var damage = (int)SCR_SkillHit(creator, initiator, skill).Damage;
			if (damage <= 0)
				return;

			AddPadBuff(creator, initiator, pad, BuffId.JincanGu_Abil_Debuff, skill.Level, damage, 60000, 1, 100);
		}

		public void Left(object sender, PadTriggerActorArgs args)
		{
			args.Initiator.RemoveBuff(BuffId.GoldenFrog_Slow_Debuff);
		}

		public void Updated(object sender, PadTriggerArgs args)
		{
			var pad = args.Trigger;
			var creator = args.Creator;
			var skill = pad.Skill;

			this.TryFadeOutTutu(pad);

			var targets = pad.Trigger.GetAttackableEntities(creator).ToList();
			foreach (var target in targets)
				this.ApplySlow(creator, target);

			var poisonTargets = targets
				.OrderBy(t => t.IsBuffActive(BuffId.JincanGu_Abil_Debuff) ? 1 : 0)
				.Take(MaxTargets);

			foreach (var target in poisonTargets)
			{
				if (target.IsBuffActive(BuffId.JincanGu_Abil_Debuff))
					continue;

				var damage = (int)SCR_SkillHit(creator, target, skill).Damage;
				if (damage <= 0)
					continue;

				AddPadBuff(creator, target, pad, BuffId.JincanGu_Abil_Debuff, skill.Level, damage, 60000, 1, 100);
			}
		}

		private void ApplySlow(ICombatEntity creator, ICombatEntity target)
		{
			target.StartBuff(BuffId.GoldenFrog_Slow_Debuff, 1f, 0f, SlowRefreshDuration, creator, SkillId.Wugushi_JincanGu);
		}

		private void TryFadeOutTutu(Pad pad)
		{
			if (pad.Trigger.RemainingLifeTime > TimeSpan.FromMilliseconds(1200))
				return;

			if (pad.Variables.Has(FadeStartedVar))
				return;

			pad.Variables.Set(FadeStartedVar, true);

			if (pad.Monster != null)
				Send.ZC_NORMAL.FadeOut(pad.Monster, TimeSpan.FromMilliseconds(1000));
		}
	}
}
