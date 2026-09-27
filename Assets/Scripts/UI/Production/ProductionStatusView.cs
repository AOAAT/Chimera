using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public enum ProductionViewState { Queued, Initializing, WaitingMaterials, WaitingHauler, InTransit, Producing, Paused, OutputFull, InputFull }

// Read-only presentation: observing an order must never reserve or consume materials.
public sealed class ProductionStatusView
{
    public ProductionViewState State;
    public string Label, Hint, Details;
    public Color Color => State == ProductionViewState.Producing ? ChimeraUITheme.HP :
        State == ProductionViewState.OutputFull || State == ProductionViewState.InputFull ? ChimeraUITheme.Danger :
        State == ProductionViewState.Paused || State == ProductionViewState.Queued ? ChimeraUITheme.SecondaryText : ChimeraUITheme.Accent;

    private static ProductionStatusView Make(ProductionViewState state,string label,string hint,string details=null)
        => new ProductionStatusView { State=state,Label=label,Hint=hint,Details=details??hint };

    public static ProductionStatusView Describe(ProductionTask task,FactoryBuilding factory,LogisticsManager logistics)
    {
        if(task == null) return Make(ProductionViewState.Queued,"无生产任务","选择上方配方添加任务。");
        if(task.IsPaused) return Make(ProductionViewState.Paused,"已暂停","点击“继续”恢复；已送达原料保留。");
        if(task.IsActivelyProducing)
            return Make(ProductionViewState.Producing,$"生产中 · {task.ActiveLineIndex+1}号线",
                $"剩余 {task.RemainingTime/Mathf.Max(.01f,task.EffectiveSpeed):0.#} 秒 · 速度 ×{task.EffectiveSpeed:0.##}",
                "原料已投入。派驻合适的居民可提高生产速度或增加并行生产线。");
        if(factory == null || logistics == null || !logistics.Ready)
            return Make(ProductionViewState.Initializing,"准备中","等待生产与物流系统就绪。");
        if(task.MaterialsConsumed || factory.TaskQueue.Count(x=>!x.IsPaused&&x.IsActivelyProducing)>=factory.ActiveProductionLineCount)
            return Make(ProductionViewState.Queued,"排队中","等待空闲生产线；可调整任务顺序。");

        var output=logistics.Get(LogisticsManager.OutputID(factory));
        int reserved=factory.TaskQueue.Count(x=>x.MaterialsConsumed);
        if((output?.Used??0)+reserved >= (output?.Capacity??factory.OutputCapacity))
            return Make(ProductionViewState.OutputFull,"出货区已满","搬走成品，并检查仓库剩余容量。",
                $"成品 {output?.Used??0:0.#} / 容量 {output?.Capacity??factory.OutputCapacity:0.#}；另有 {reserved} 个生产任务预留出货位置。\n安排居民搬运，或等待占位任务完成。仓库需允许接收物品且有空间。");

        var costs=LogisticsKeys.Resources(task.PaidCost).ToList();
        if(costs.Sum(x=>x.Amount)>factory.InputCapacity)
            return Make(ProductionViewState.InputFull,"原料区容量不足","当前原料区装不下这份配方。",
                $"配方需要 {costs.Sum(x=>x.Amount):0.#} 单位容量，原料区仅 {factory.InputCapacity:0.#}。\n需要增加建筑的原料区容量；继续搬运无法解决此问题。");
        string inputID=LogisticsManager.InputID(task);
        var input=logistics.Get(inputID);
        var missing=costs.Where(x=>(input?.Count(x.Key)??0)+.0001f<x.Amount).ToList();
        if(missing.Count==0) return Make(ProductionViewState.Queued,"原料已齐","等待生产线开始加工。");
        var jobs=logistics.Data.Jobs.Where(x=>x.TargetID==inputID).ToList();
        var lines=new List<string>();var shortages=new List<string>();
        foreach(var cost in missing)
        {
            float arrived=input?.Count(cost.Key)??0;
            float assigned=jobs.Where(x=>x.Key==cost.Key).Sum(x=>x.Amount);
            float carried=jobs.Where(x=>x.Key==cost.Key&&x.PickedUp).Sum(x=>x.Amount);
            float available=logistics.AvailableResource(cost.Key);
            float shortage=Mathf.Max(0,cost.Amount-arrived-assigned-available);
            lines.Add($"{LogisticsKeys.Name(cost.Key)}：已到 {arrived:0.#}/{cost.Amount:0.#} · 在途 {carried:0.#} · 待取 {assigned-carried:0.#}");
            if(shortage>.0001f)shortages.Add($"{LogisticsKeys.Name(cost.Key)} {shortage:0.#}");
        }
        string detail=string.Join("\n",lines);
        if(shortages.Count>0) detail+="\n缺少可用库存："+string.Join("、",shortages);
        if(jobs.Any(x=>x.PickedUp))
            return Make(ProductionViewState.InTransit,"运输途中",shortages.Count>0?"部分原料在途；仍需补充库存。":"居民已取货，等待原料送达。",detail+"\n若长时间未送达，请检查搬运路线和居民状态。");
        if(shortages.Count>0)
            return Make(ProductionViewState.WaitingMaterials,"等待原料","缺 "+string.Join("、",shortages)+"；补充库存。",detail+"\n补充对应资源；其他任务预留的库存不计入可用数量。");
        float committed=logistics.Data.Storages.Where(x=>x.ID!=inputID&&x.OwnerID==factory.PersistentID&&x.Kind==StorageKind.OrderInput&&x.Used+logistics.ReservedIn(x.ID)>.0001f).Sum(x=>x.Capacity);
        if(jobs.Count==0 && committed+costs.Sum(x=>x.Amount)>factory.InputCapacity)
            return Make(ProductionViewState.InputFull,"原料区被占用","等待其他任务消耗或退回原料。",detail+"\n其他订单已占用或预留原料区，需等待它们完成；取消任务后的退料也需要搬走。");
        // The planner admits only the first N non-paused orders.
        if(jobs.Count==0&&!factory.TaskQueue.Where(x=>!x.IsPaused).Take(factory.ActiveProductionLineCount).Contains(task))
            return Make(ProductionViewState.Queued,"排队中","等待前方订单；可调整任务顺序。",detail+"\n该订单尚未进入当前生产线的备料范围。");
        bool assignedWorker=jobs.Any(x=>!string.IsNullOrEmpty(x.WorkerID));
        return Make(ProductionViewState.WaitingHauler,"等待搬运",assignedWorker?"居民正在取货。":"安排空闲居民搬运原料。",
            detail+"\n"+(assignedWorker?"原料尚未取走，请检查取货路线。":"确认居民开启搬运工作且未入驻其他岗位；也需保证仓库与工厂入口可达。"));
    }
}
