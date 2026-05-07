using System;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Effects;
using Yggdrasil.Util;

namespace Melia.Zone.Skills.Handlers.Scouts.Desperado
{
	[Package("laima")]
	[SkillHandler(SkillId.Desperado_RussianRoulette)]
	public class Desperado_RussianRouletteOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			if (caster.IsAbilityActive(AbilityId.Desperado24))
				caster.RemoveBuff(BuffId.Violent);

			DesperadoSkillHelper.ResetCoreCooldowns(caster);

			var unluckyChance = caster.IsAbilityActive(AbilityId.Desperado24) ? 20 : 10;
			var isUnlucky = RandomProvider.Get().Next(100) < unluckyChance;
			if (caster is Character character)
				DesperadoSkillHelper.SendDesperadoAnim(character, isUnlucky ? 2 : 1);

			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);
			skill.Run(this.ResolveRoulette(skill, caster, isUnlucky));
		}

		private async Task ResolveRoulette(Skill skill, ICombatEntity caster, bool isUnlucky)
		{
			DesperadoSkillHelper.PlaySoundIfKnown(caster, isUnlucky ? "voice_cleric_m_omikuji_cast_fail" : "voice_scout_m_hasisas_cast");

			await skill.Wait(TimeSpan.FromMilliseconds(400));
			if (isUnlucky)
			{
				DesperadoSkillHelper.PlayEffectNodeIfKnown(caster, "AerialExplosion_Fire_Orange_01", 1f, "Dummy_R_HAND");
				DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_russianroulette_fail");
				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 50, "UNLUCKY");
			}
			else
			{
				DesperadoSkillHelper.PlayEffectNodeIfKnown(caster, "AerialExplosion_PartyPopper_Random_01", 1f, "Bip01 Spine");
				DesperadoSkillHelper.PlaySoundIfKnown(caster, "skl_eff_desperado_russianroulette_success");
				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 50, "SUCCESS");
			}

			await skill.Wait(TimeSpan.FromMilliseconds(300));
			if (isUnlucky)
			{
				var cooldown = caster.IsAbilityActive(AbilityId.Desperado21) ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(20);
				skill.StartCooldown(cooldown);
				return;
			}

			caster.StartBuff(BuffId.Violent, skill.Level, 0, TimeSpan.Zero, caster, skill.Id, buff => buff.OverbuffCounter = 6);
		}
	}
}
