using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Yggdrasil.Logging;

namespace Melia.Zone.Skills.Handlers.Swordsmen.Eskrimer
{
	[Package("laima")]
	[SkillHandler(SkillId.Escrimeur_PassataSotto)]
	public class Escrimeur_PassataSotto : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			Log.Info("Escrimeur_PassataSotto: Handle called. HasMaximumToucher={0}, SkillLevel={1}.", EskrimerSkillHelper.HasMaximumToucher(caster), skill.Level);

			if (!EskrimerSkillHelper.HasMaximumToucher(caster))
			{
				EskrimerSkillHelper.SetPasataSotoAvailability(caster, false);
				return;
			}

			if (!EskrimerSkillHelper.TryBeginGroundSkill(skill, caster, originPos, farPos, target))
				return;

			skill.Run(EskrimerSkillHelper.ExecutePasataSoto(caster, skill, originPos, farPos));
		}
	}
}
