using System.Collections.Generic;
using UnityEngine;

public sealed class AssemblyComparison
{
    public float[] Before, After;
    public string Summary;

    public static AssemblyComparison Evaluate(ChassisDataSO chassis,InstancedComponent[] installed,int slot,InstancedComponent candidate)
    {
        if(chassis==null || installed==null || slot<0 || slot>=installed.Length)return null;
        // Only the temporary slot array changes. No inventory, profile or scene entities are touched.
        var before=new RuntimeChimeraData();before.Assemble(chassis,installed);
        var next=(InstancedComponent[])installed.Clone();next[slot]=candidate;
        var after=new RuntimeChimeraData();after.Assemble(chassis,next);
        var result=new AssemblyComparison { Before=Values(before),After=Values(after) };
        string[] names={"生命","护甲","格挡","质量","动力","移速"};
        string[] units={"","",""," t",""," m/s"};
        var lines=new List<string>();
        for(int i=0;i<names.Length;i++)
        {
            float delta=result.After[i]-result.Before[i];
            if(Mathf.Abs(delta)<.005f)continue;
            lines.Add($"{names[i]}  {result.Before[i]:0.##} → {result.After[i]:0.##}{units[i]}  ({delta:+0.##;-0.##;0})");
        }
        result.Summary=lines.Count==0 ? "整机六项基础属性没有变化。" : string.Join("\n",lines);
        return result;
    }
    private static float[] Values(RuntimeChimeraData data) => new[] { data.MaxHP,data.MaxAP,
        data.GetGlobalStat(StatType.AddedBlock),data.TotalMass,data.TotalEnginePower,
        GameFormulas.CalcMoveSpeed(data.TotalEnginePower,data.TotalMass,CombatSandbox.GetSpeed(1f)) };
}
