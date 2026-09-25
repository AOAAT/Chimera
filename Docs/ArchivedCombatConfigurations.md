# 未确定语义的历史战斗配置

这两个配置在清洗前已丢失实现脚本。正式场景不引用它们；只有未接入场景的 WPN1 原型引用其中一个。未擅自将旧 Power 解释为当前 EnginePower 或 CP。原始数据保存在此，Unity Assets 中移除损坏配置及原型中的失效动作引用。

## Assets/Data/6_ECA_Blocks (行为逻辑积木)/CombatECA/BoostDamageByPower.asset
GUID: `05427638238aeef4f8e08cdec66762e5`
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f1672b275d8872242ba0f33a1051a311, type: 3}
  m_Name: BoostDamageByPower
  m_EditorClassIdentifier: 
  PowerToDamageRatio: 1
```

## Assets/Data/1_Blueprints (装备与底盘图纸)/2_Components/Weapon/WPN1_聚能电磁炮/ECA/L1_BoostDamageByPower.asset
GUID: `2b3ffe28524a76b49b1b35ae8160d85c`
```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f1672b275d8872242ba0f33a1051a311, type: 3}
  m_Name: L1_BoostDamageByPower
  m_EditorClassIdentifier: 
  PowerToDamageRatio: 1
```
