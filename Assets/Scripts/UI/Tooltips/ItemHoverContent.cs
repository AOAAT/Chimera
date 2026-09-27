using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// A recipe never constructs a random inventory instance just to preview its stats.
public sealed class ItemHoverContent
{
    public string Title, Subtitle, Body;
    public Sprite Icon;
    public float RefreshInterval; // Zero means a stable snapshot, as used by assembly candidates.

    public static string TypeName(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Core: return "核心";
            case ComponentType.Weapon: return "武器";
            case ComponentType.Movement: return "移动";
            case ComponentType.Support: return "辅助";
            case ComponentType.Factory: return "生产";
            default: return "组件";
        }
    }
    private static string Stats(IEnumerable<StatEntry> stats)
    {
        var lines = new List<string>();
        if (stats != null) foreach (var group in stats.Where(x => x != null).GroupBy(x => new { x.StatID, x.ModType }))
        {
            float number = group.Key.ModType == BuffModifierType.Multiplier ? group.Aggregate(1f,(product,x) => product*x.Value) : group.Sum(x => x.Value);
            string value = group.Key.ModType == BuffModifierType.Multiplier ? number.ToString("0.##") + " ×" :
                group.Key.StatID == StatType.CriticalChance ? number.ToString("P1") : number.ToString("0.##");
            lines.Add(StatTranslation.Get(group.Key.StatID) + "    " + value);
        }
        return lines.Count == 0 ? "无额外属性" : string.Join("\n", lines);
    }
    private static string Notes(string mechanics, string description)
    {
        string result = "";
        if (!string.IsNullOrWhiteSpace(mechanics) && mechanics != "...") result += "\n\n<b>特殊效果</b>\n" + mechanics;
        if (!string.IsNullOrWhiteSpace(description)) result += "\n\n" + description;
        return result;
    }
    public static ItemHoverContent Chassis(ChassisDataSO data, string customName = null)
    {
        if (data == null) return null;
        return new ItemHoverContent { Title = customName ?? data.ChassisName, Icon = data.ChassisSprite,
            Subtitle = "装甲底盘 · " + data.ChassisName,
            Body = "<b>底盘属性</b>\n" + Stats(data.BaseStats) + Notes(data.SpecialMechanicDesc, data.Description) };
    }
    public static ItemHoverContent Component(InstancedComponent item)
    {
        if (item?.BaseData == null) return null;
        var model = item.BaseData.GetModelData(item.CurrentMark);
        var body = new StringBuilder("<b>实际属性</b>\n" + Stats(ComponentStatResolver.Resolve(item)));
        body.Append($"\n\n<b>品质与词条</b>\n品质修正 {item.QualityScore:+0.0%;-0.0%;0.0%}");
        if (item.Affixes != null) foreach (var affix in item.Affixes.Where(x => x != null))
            body.Append("\n").Append(affix.DisplayName).Append("：").Append(affix.Description)
                .Append("\n").Append(Stats(affix.Modifiers));
        body.Append(Notes(model?.SpecialMechanicDesc, item.BaseData.Description));
        return new ItemHoverContent { Title = item.DisplayName, Icon = item.BaseData.ComponentIcon,
            Subtitle = item.BaseData.ComponentName + " · " + TypeName(item.BaseData.Type) +
                $" · Mk.{item.CurrentMark} · " + ComponentQualityUtility.GetName(item.Quality), Body = body.ToString() };
    }
    public static ItemHoverContent Recipe(Object definition, FactoryBuilding factory)
    {
        ItemHoverContent result;
        ResourceSet cost;
        float seconds;
        if (definition is ChassisDataSO chassis)
        {
            result = Chassis(chassis); cost = chassis.ProductionCost; seconds = chassis.BaseProductionTime;
            result.Subtitle = "生产配方 · 装甲底盘";
        }
        else if (definition is ComponentDataSO component)
        {
            var model = component.GetModelData(1);
            result = new ItemHoverContent { Title = component.ComponentName, Icon = component.ComponentIcon,
                Subtitle = "生产配方 · " + TypeName(component.Type) + " · Mk.1",
                Body = "<b>标准属性（未计品质）</b>\n" + Stats(model?.Stats) +
                    "\n\n成品品质与随机词条在生产完成后确定。" + Notes(model?.SpecialMechanicDesc, component.Description) };
            cost = model?.ProductionCost ?? new ResourceSet(); seconds = component.BaseProductionTime;
        }
        else return null;
        float effective = factory != null ? factory.GetEffectiveProductionTime(seconds) : seconds;
        result.Body = $"<b>生产需求</b>\n{UIFeedback.Cost(cost)}\n基础耗时 {seconds:0.#} 秒 · 当前预计 {effective:0.#} 秒\n不含等待原料与排队时间\n\n" + result.Body;
        return result;
    }
    public static ItemHoverContent Socket(SlotDefinition slot, InstancedComponent item = null)
    {
        if (item != null)
        {
            var result = Component(item);
            if (result != null) result.Subtitle = slot.SlotName + " · 已安装\n" + result.Subtitle;
            return result;
        }
        return new ItemHoverContent { Title = slot.SlotName, Subtitle = "空节点",
            Body = "<b>可安装类型</b>\n" + (slot.AllowedTypes == null ? "无" : string.Join("、", slot.AllowedTypes.Select(TypeName))) +
                "\n\n点击节点，选择适配组件。" };
    }
    public static ItemHoverContent Accessory(InstancedAccessory item) => item?.BaseData == null ? null :
        new ItemHoverContent { Title = item.BaseData.AccessoryName, Subtitle = "逻辑配件", Icon = item.BaseData.AccessoryIcon,
            Body = Notes(item.BaseData.SpecialMechanicDesc, item.BaseData.Description).Trim() };
}
