# 项目目录与场景组织

2026-09-27 整理。当前目录索引以本文为准；早期架构分析保留历史背景，不代表现行路径和功能状态。

## Project 窗口

```text
Assets/
  Scripts/                  游戏运行代码
    Core/                   公共类型与公式
    Save/                   存档模型、捕获恢复和配置解析
    World/                  网格、寻路、相机、操作指令与场景分组辅助
    Residents/              人口、居民身份、工作贡献和人物表现
    Buildings/              建筑、岗位、工厂、组装厂、仓库及建造检查
    Production/             生产任务与制造逻辑
    Logistics/              货物位置、容器、运输任务与调度
    Economy/                可用资源汇总与资源 API
    Items/                  库存、品质、词条及物品定义类型
    Mechs/                  机甲实体、属性解算、移动 AI 与技能控制
    Combat/                 战斗实体、导演、敌人评分、配置类型与 ECA
    UI/                     按实际用途组织，不再区分“新 UI/旧 UI”
      Assembly/ Inventory/ Residents/ Production/ Logistics/
      HUD/ Buildings/ Combat/ Menus/ Tooltips/ Theme/ Common/
    Presentation/           音乐音效、特效、飘字、世界 HUD 与视觉反馈
    GameplayEvents/         事件动作基础
    Diagnostics/            测试台、开发控制台、装配调试辅助
  Editor/                   编辑器程序集
    Art/ UI/ World/          美术、界面、地图编辑工具
    Inspectors/ Validation/ 自定义 Inspector 与项目检查
    ProjectOrganizationAuthoring.cs  本次迁移及可撤销场景整理菜单
  Data/                     ScriptableObject 配置资产
    Buildings/ Residents/ Mechs/ Combat/ ECA/ Audio/
  Prefabs/                  通过场景或配置引用的预制体
    Buildings/ Units/ UI/ Combat/ World/ Diagnostics/
  Art/                      图片与美术源素材
    Mechs/HandDrawn/         保留原始手绘底盘、组件及导入设置
    World50/                当前 50 PPU 地块与组件适配副本
    Residents/              居民图集与选择圈材质
    Enemies/ Projectiles/ Effects/ Backgrounds/
    UI/                     UI 图标与主菜单原画
    World/Source/           地块与资源源图
    Prototypes/             原有占位/实验图片，保留引用，尚未判定为可删除
  Resources/                通过 Resources.Load 加载的运行时资产
  Audio/ Materials/ Shaders/ Settings/ Scenes/
  TextMesh Pro/             第三方内容，本轮保持位置

ArtSource/                  Unity 项目外保留的生成/绘制源稿
ArtReview/                  比例、旋转等美术审阅页面
Docs/                       设计、交付与项目说明
Tools/Regression/           隔离工程测试代码与结果，不编入正式游戏
Tools/ProjectOrganization/  本次迁移清单和执行记录
```

`Data` 保存配置实例，`Scripts` 保存类型与行为。例如建筑图纸放 `Data/Buildings`，`BuildingDataSO.cs` 放 `Scripts/Buildings`。具体手绘资源及业务配置的中文名称保留，目录去掉旧玩法编号和含混的层级名称。

`Resources` 是有意保留的例外：`Buildings`、`Residents`、`UI`、物理材质及升级标记有固定加载路径。仓库 Prefab、主题、居民图集配置、运行时加载的窗口仍放在这里，不复制第二份到普通 Prefabs 目录。

机甲定义旁的专属 ECA 配置继续随所属组件保存；共享动作配置放 `Data/ECA` 或 `SharedActions`。本轮只整理职责和位置，没有删除尚未明确废弃的战斗内容，没有拆改程序集、类名或业务 ID。

## Hierarchy

两个正式场景采用相同的顶层分组：

```text
跨场景音频、音乐、控制台等对象     继续作为独立根节点
01_Systems                     场景管理器、人口和资源等
02_World                       相机、光照、网格与生成地面
03_Buildings                   场景建筑与运行时新建建筑
04_Units                       居民、机甲及独立敌人
05_UI                          原有各 Canvas、窗口与 EventSystem
06_Debug                       已有测试台、调试对象
90_Editor                      美术/UI 升级完成标记
```

分组仅用于收纳，保持启用、位置 (0,0,0)、旋转 (0,0,0)、缩放 (1,1,1)，不要拿它们缩放世界或批量关闭系统。调试分组不会自动关闭原先启用的脚本，也不会改变调试快捷键。

原有 UI 内部布局、Canvas 排序、按钮回调、Prefab 连接、对象名称、启用状态和世界位置保持原样。音乐、音效、控制台等调用 `DontDestroyOnLoad` 的对象留在根层级，避免把整个场景分组一起带到下一个场景或破坏单例。

运行中新建且没有指定父节点的居民、建筑、机甲和敌人会进入相应分组；已经有父节点的实体保持其所有权。弹道、对象池、临时反馈及跨场景对象继续由各自系统管理，不额外逐帧扫描或重新挂接。

菜单 **Tools → Chimera → 项目整理 → 整理当前场景层级（可撤销）** 可以整理后来手动放入根层级的对象。它不重排窗口内部控件，也不保存场景；检查后自行保存即可。

## 移动与后续维护

- 本次通过 Unity `AssetDatabase.MoveAsset` 移动资源，保留 `.meta`、GUID、贴图 PPU、Pivot 及序列化引用；硬编码路径同步更新。
- `Tools/ProjectOrganization/migration.json` 记录原路径、新路径和原始资源哈希，便于定位旧文件及核对；`applied.json` 防止重复执行一次性迁移。
- 新增功能放入对应模块；纯编辑器代码放 `Assets/Editor`，避免运行时引用 UnityEditor。
- 新的场景分组操作可以撤销；目录迁移应通过版本控制整体回退，不能只删执行记录或只还原某个脚本路径。
- 当前已打开场景在编辑模式原地整理；若原来有未保存编辑，不自动保存这些修改。不会强制退出 Play、关闭编辑器或重新打开用户当前场景。

整理后的验证结果见 `Tools/Regression/Artifacts/ProjectOrganization/`。验证包括资源完整性、场景对象/布局/引用保持、迁移重复执行，以及装配、物流、存读档和世界显示回归。

本次实际迁移 589 个代码/资源文件，核对 826 份原始资产的导入元数据。隔离工程完成 154 项检查，并通过主菜单进入基地、返回、再次进入的冒烟检查；正式资源引用审计无错误。最终主项目的场景文件也已复制回隔离工程复验。第三方 TMP 示例的既有引用问题单独记录，不属于正式场景。
