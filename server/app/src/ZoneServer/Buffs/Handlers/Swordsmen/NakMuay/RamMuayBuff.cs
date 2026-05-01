using System;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Swordsmen.NakMuay
{
	/// <summary>
	/// Handles Ram Muay's temporary basic attack stance.
	/// </summary>
	[BuffHandler(BuffId.RamMuay_Buff)]
	public class RamMuayBuff : BuffHandler
	{
		private const string AddedNakMuayAttackVar = "RamMuay.AddedNakMuayAttack";
		private const string AddedNakMuaySokChiangNormal = "RamMuay.AddedNakMuaySokChiangNormal";
		private const string AddedNakMuayTeKhaNormal = "RamMuay.AddedNakMuayTeKhaNormal";
		private const string AddedNakMuayTeTrongNormal = "RamMuay.AddedNakMuayTeTrongNormal";
		
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var target = buff.Target;
			if (target is not Character character)
				return;

			var level = Math.Max(1, (int)buff.NumArg1);

			if (!character.Skills.TryGet(SkillId.NakMuay_Attack, out _))
			{
				// Match the official packet flow: add the temporary basic
				// attack skill first, but keep it out of the quickbar UI.
				character.Skills.Add(new Skill(character, SkillId.NakMuay_Attack, level, false));
				buff.Vars.SetInt(AddedNakMuayAttackVar, 1);
			}

			AddSkill(character, SkillId.NakMuay_SokChiang_Normal, SkillId.NakMuay_SokChiang, buff, AddedNakMuaySokChiangNormal);
			AddSkill(character, SkillId.NakMuay_TeKha_Normal, SkillId.NakMuay_TeKha, buff, AddedNakMuayTeKhaNormal);
			AddSkill(character, SkillId.NakMuay_TeTrong_Normal, SkillId.NakMuay_TeTrong, buff, AddedNakMuayTeTrongNormal);
			
			var atkSpdBonus = level * 20;
			AddPropertyModifier(buff, target, PropertyName.NormalASPD_BM, atkSpdBonus);
			
			Send.ZC_NORMAL.SetMainAttackSkill(character, SkillId.NakMuay_Attack);
			Send.ZC_NORMAL.SetSubAttackSkill(character, SkillId.NakMuay_Attack2);
			Send.ZC_STANCE_CHANGE(character);
		}

		public override void OnEnd(Buff buff)
		{
			var target = buff.Target;
			if (target is not Character character)
				return;

			Send.ZC_NORMAL.SetMainAttackSkill(character, SkillId.None);
			Send.ZC_NORMAL.SetSubAttackSkill(character, SkillId.None);


			if (buff.Vars.TryGetInt(AddedNakMuayAttackVar, out var addedNakMuayAttack) && addedNakMuayAttack != 0)
				character.Skills.Remove(SkillId.NakMuay_Attack);

			RemoveSkill(character, SkillId.NakMuay_SokChiang_Normal, buff, AddedNakMuaySokChiangNormal);
			RemoveSkill(character, SkillId.NakMuay_TeKha_Normal, buff, AddedNakMuayTeKhaNormal);
			RemoveSkill(character, SkillId.NakMuay_TeTrong_Normal, buff, AddedNakMuayTeTrongNormal);
			
			RemovePropertyModifier(buff, target, PropertyName.NormalASPD_BM);
			
			Send.ZC_STANCE_CHANGE(character);
		}

		private static void AddSkill(Character character, SkillId skillId, SkillId secondSkillId, Buff buff, string buffVariable)
		{
			if (character.Skills.TryGet(skillId, out _)) return;
			if (!character.Skills.TryGet(secondSkillId, out var secondSkill)) return;
			
			character.Skills.Add(new Skill(character, skillId, secondSkill.Level, false));
			buff.Vars.SetInt(buffVariable, 1);
		}
		
		private static void RemoveSkill(Character character, SkillId skillId, Buff buff, string buffVariable)
		{
			if (buff.Vars.TryGetInt(buffVariable, out var addedVariable) && addedVariable != 0)
				character.Skills.Remove(skillId);
		}
	}
}
