# 存档系统基础

## 当前范围

`SaveGameManager` 将运行时对象转换为纯数据快照并写入 JSON。当前版本号为 `2`，默认文件名为 `chimera_save_0.json`，位置由 `Application.persistentDataPath` 决定。版本 1 会在读取时自动补齐居民性格字段并迁移到版本 2。

当前保存：

- 三种全局资源
- 居民身份、成长预留字段、特质 ID、熟练度、生命值、岗位和世界位置
- 建筑实例、位置、员工、工厂生产队列、总部招募进度、装配厂集合点
- 仓库堆叠、底盘/组件/芯片实例及其装配关系
- 已部署玩家机甲的档案、组件槽位、生命/护甲和位置

当前不保存战斗瞬时状态，例如敌人、弹道、临时 Buff、寻路路径和当前选中对象。读档会重载殖民地场景，再按照“资源与库存 → 居民档案 → 建筑与岗位 → 地图居民 → 已部署机甲”的顺序恢复。

## 稳定标识规则

- 图纸使用人工维护的业务 ID：`ChassisID`、`ComponentBaseID`、`AccessoryID`、`BuildingID`。
- 运行时物品、居民、生产任务和机甲继续使用各自的 `InstanceID`/`UnitID`。
- 建筑实例 ID 由场景、建筑图纸（场景预放建筑则使用脚本类型）和网格位置生成。
- 存档只写业务 ID，不直接序列化 `ScriptableObject` 引用或 Unity 资源 GUID。

已经发布并进入玩家存档的业务 ID 不应修改或复用。重命名显示名称和移动资源文件不影响存档。

## 调用入口

运行时可调用：

```csharp
SaveGameManager.Instance.SaveDefault();
SaveGameManager.Instance.LoadDefault();
SaveGameManager.Instance.HasDefaultSave();
```

编辑器 Play Mode 中临时提供 `F5` 保存、`F9` 读取，方便在正式存档 UI 接入前验证。

## 扩展规则

新增长期状态时，将字段放入 `SaveDataModels.cs` 的 DTO，而不是直接序列化 `MonoBehaviour` 或 `ScriptableObject`。捕获和恢复必须成对修改。若字段缺省值不能安全兼容旧数据，应提高 `GameSaveData.CurrentVersion` 并在读取阶段加入明确迁移步骤。
