using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Invoked by ItemHoverChecks inside the isolated play-mode fixture.
public static class UIClarityChecks
{
    static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception(message);
        File.AppendAllText("ClarityResults/checks.txt","PASS "+message+"\n");
    }
    public static void Production()
    {
        Directory.CreateDirectory("ClarityResults");File.WriteAllText("ClarityResults/checks.txt","");
        var manager=LogisticsManager.Instance;
        var factory=UnityEngine.Object.FindObjectOfType<FactoryBuilding>();
        var oldData=manager.Data;var oldQueue=factory.TaskQueue;
        float oldInput=factory.InputCapacity;int oldOutput=factory.OutputCapacity,oldLines=factory.MaxProductionLines;
        try
        {
            manager.Data=new LogisticsSaveData();factory.InputCapacity=100;factory.OutputCapacity=2;factory.MaxProductionLines=1;
            var task=new ProductionTask(null,"状态测试",null,100,new ResourceSet {Scrap=10}) {UsesLogistics=true};
            factory.TaskQueue=new List<ProductionTask>{task};
            Action<ProductionViewState,string> expect=(state,message)=>{
                string snapshot=JsonUtility.ToJson(manager.Data)+JsonUtility.ToJson(task);
                var status=ProductionStatusView.Describe(task,factory,manager);
                Check(status.State==state,message+" (actual "+status.State+")");
                Check(!string.IsNullOrWhiteSpace(status.Hint)&&snapshot==JsonUtility.ToJson(manager.Data)+JsonUtility.ToJson(task),"status provides a hint without mutating order or logistics");
            };
            task.LogisticsStatus="生产中（故意过期）";
            expect(ProductionViewState.WaitingMaterials,"missing stock ignores stale cached status");
            var source=new LogisticsStorage{ID="clarity:warehouse",Kind=StorageKind.Warehouse};source.Add(LogisticsKeys.Scrap,10);manager.Data.Storages.Add(source);
            expect(ProductionViewState.WaitingHauler,"available stock waits for a hauler");
            var job=new HaulJob{SourceID=source.ID,TargetID=LogisticsManager.InputID(task),Key=LogisticsKeys.Scrap,Amount=10};manager.Data.Jobs.Add(job);
            expect(ProductionViewState.WaitingHauler,"reserved stock is not reported as missing");
            job.WorkerID="worker";
            Check(ProductionStatusView.Describe(task,factory,manager).Hint.Contains("正在取货"),"assigned hauler distinguishes pickup from transit");
            source.Remove(LogisticsKeys.Scrap,10);job.PickedUp=true;
            expect(ProductionViewState.InTransit,"picked-up materials are in transit");
            task.PaidCost=new ResourceSet{Scrap=15};
            Check(ProductionStatusView.Describe(task,factory,manager).Details.Contains("缺少可用库存"),"partial shipment also explains remaining shortage");
            task.PaidCost=new ResourceSet{Scrap=10};manager.Data.Jobs.Clear();
            var input=new LogisticsStorage{ID=LogisticsManager.InputID(task),OwnerID=factory.PersistentID,Kind=StorageKind.OrderInput,Capacity=10};input.Add(LogisticsKeys.Scrap,10);manager.Data.Storages.Add(input);
            expect(ProductionViewState.Queued,"delivered ingredients wait for processing");
            task.IsActivelyProducing=true;task.ActiveLineIndex=0;task.EffectiveSpeed=2;task.CurrentProgress=20;
            expect(ProductionViewState.Producing,"running line reports production");
            Check(ProductionStatusView.Describe(task,factory,manager).Hint.Contains("40 秒"),"remaining time reflects actual production speed");
            task.IsPaused=true;expect(ProductionViewState.Paused,"pause immediately overrides last-frame production flag");
            task.IsPaused=false;task.IsActivelyProducing=false;input.Cargo.Clear();
            var output=new LogisticsStorage{ID=LogisticsManager.OutputID(factory),Kind=StorageKind.FactoryOutput,Capacity=2};output.Add("component:test",2);manager.Data.Storages.Add(output);
            expect(ProductionViewState.OutputFull,"full output explains blockage");
            output.Cargo.Clear();task.PaidCost=new ResourceSet{Scrap=101};expect(ProductionViewState.InputFull,"oversized recipe cannot be solved by more haulers");
            task.PaidCost=new ResourceSet{Scrap=10};source.Add(LogisticsKeys.Scrap,10);
            var ahead=new ProductionTask(null,"前方任务",null,100,new ResourceSet());factory.TaskQueue.Insert(0,ahead);
            expect(ProductionViewState.Queued,"orders outside the admitted lines report queuing");
            ahead.IsPaused=true;expect(ProductionViewState.WaitingHauler,"paused earlier order does not block admission");
            ahead.IsPaused=false;ahead.MaterialsConsumed=true;output.Add("component:test",1);
            expect(ProductionViewState.OutputFull,"in-progress reservations count toward output capacity");
            output.Cargo.Clear();ahead.MaterialsConsumed=false;factory.TaskQueue.Remove(ahead);
            manager.Data.Storages.Add(new LogisticsStorage{ID="other-input",OwnerID=factory.PersistentID,Kind=StorageKind.OrderInput,Capacity=95,Cargo=new List<CargoStack>{new CargoStack(LogisticsKeys.Scrap,1)}});
            expect(ProductionViewState.InputFull,"another order's reserved input capacity is explained");
        }
        finally {manager.Data=oldData;factory.TaskQueue=oldQueue;factory.InputCapacity=oldInput;factory.OutputCapacity=oldOutput;factory.MaxProductionLines=oldLines;}
    }
    public static void Comparison(AssemblyWorkshopUI workshop,ChassisDataSO chassis,int socket,InstancedComponent candidate)
    {
        var inventory=PlayerInventoryManager.Instance;
        string before=JsonUtility.ToJson(inventory.CaptureSaveData())+JsonUtility.ToJson(LogisticsManager.Instance.Data);
        var profile=(SavedUnitProfile)typeof(AssemblyWorkshopUI).GetField("currentEditingProfile",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(workshop);
        string profileBefore=JsonUtility.ToJson(profile);
        var content=workshop.BuildCandidateHover(socket,candidate);
        Check(content.Body.Contains("整机变化")&&content.Body.Contains("品质修正"),"candidate hover combines mech comparison and actual instance data");
        var empty=new InstancedComponent[chassis.Sockets.Count];
        var install=AssemblyComparison.Evaluate(chassis,empty,socket,candidate);
        Check(empty.All(x=>x==null),"preview never writes the input slot array");
        var equipped=(InstancedComponent[])empty.Clone();equipped[socket]=candidate;
        var actual=new RuntimeChimeraData();actual.Assemble(chassis,equipped);
        Check(Mathf.Approximately(install.After[0],actual.MaxHP)&&Mathf.Approximately(install.After[1],actual.MaxAP)&&Mathf.Approximately(install.After[3],actual.TotalMass),"preview matches actual assembler with instance quality and affix");
        var remove=AssemblyComparison.Evaluate(chassis,equipped,socket,null);
        Check(remove.After.SequenceEqual(install.Before)&&remove.Before.SequenceEqual(install.After),"unequip reverses all six preview attributes");
        var same=AssemblyComparison.Evaluate(chassis,equipped,socket,candidate);
        Check(same.Before.SequenceEqual(same.After),"replacing identical component does not double-count it");
        Check(before==JsonUtility.ToJson(inventory.CaptureSaveData())+JsonUtility.ToJson(LogisticsManager.Instance.Data)&&profileBefore==JsonUtility.ToJson(profile),"all preview paths preserve inventory, reservations and edited profile");
        var badges=workshop.ChassisVisualRoot.GetComponentsInChildren<AssemblySocketBadge>();
        Check(badges.Length==chassis.Sockets.Count&&badges.All(x=>x.GetComponentsInChildren<UnityEngine.UI.Graphic>().Where(g=>g.name=="NodeState"||g.name=="NodeNumber").All(g=>!g.raycastTarget)),"node badges exist and never intercept clicks");
        var card=RightInventoryPanelUI.Instance.ContentRoot.GetComponentsInChildren<InventoryItemSlotUI>().First(x=>x.ItemLevelText!=null&&x.ItemLevelText.gameObject.activeSelf);
        Check(!card.ItemLevelText.text.Contains("%")&&!card.ItemLevelText.enableAutoSizing&&card.ItemLevelText.fontSize>=16,"card quality label stays compact with a readable fixed font");
        File.AppendAllText("ClarityResults/checks.txt","COMPLETE\n");
    }
}
