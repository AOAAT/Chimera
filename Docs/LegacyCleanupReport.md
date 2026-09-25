# 旧玩法清洗实施报告

日期：2026-09-26。项目：`F:/UnityGame/Chimera`。开始时 Git 工作区干净；未提交、更改历史或覆盖用户修改。未手工修改原项目的 Library、Temp、obj。Unity 验证在项目外的临时副本中完成。

正式启动链现在为：**Scene_MainMenu → 进入基地 → RTS_World_Master**。Build Settings 只包含这两个场景。原场景尺寸保留作样例，底层不固定地图长宽比、进攻方向或路线数量。

## 已删除

- 节点生成、路线连线、节点解锁/访问、楼层推进：MapManager、MapGenerator、MapVisualizer、MapLineDrawer、MapNodeData、MapNodeUI。
- 依赖层数和节点完成的叙事流程：EventDirector、EventNodeSO、EventPoolConfigSO、EventOptionUI、概率跳转、侦察地图、事件强化占位、终局返回菜单动作。
- 按剩余战斗场次计时的 GlobalProtocolRegistry；无人调用的战后自动修满方法；旧三选一掉落枚举。
- Scene_MainGame 及其全部旧地图、房间、事件、商店、结算、机库和失效脚本挂载。
- 旧事件/教程/终局配置、层数事件池、旧掉落池与商店池；地图节点/连线、旧机库槽和事件选项 Prefab；专属卷轴、问号、战斗节点、商店、事件面板图片。
- 基地场景内残留事件面板、开始战斗按钮、旧组件升级面板。RTSTestBench 曾被移除挂载，后按用户要求恢复，保留 E 键生成测试敌人的调试能力。
- 主菜单未实现的读档按钮及字段；地图选择音效枚举；音乐管理器的地图/商店/事件/结算流程切换接口。

## 解耦及入口修复

- CombatDirector 保留注册、独立战斗开关和退出清理，删除返回地图和导航 UI 控制；仓库只控制自身显示与音效，不再通知战斗管理器切换流程。
- 4 个通用事件条件/动作脚本移至 GameplayEvents，保留 CP 条件与修改能力；删除 SAN、MapDepth 等旧资源概念，移除对无关资源管理器的依赖。保留的序列化 CP 枚举数值不变。
- MusicManager 按场景加载选择主菜单/基地音乐；原地图音乐改名为 BGM_Base，GUID 不变。通用淡入淡出、战斗音乐和沉浸音效仍保留。
- RTSGridSystem 使用独立宽、高、CellSize、GridOrigin；取消沿 X 轴划分基地/敌区和随机资源带的规则。RTSMapVisuals 从格子风味读取颜色。
- RTSCameraMover 支持二维键盘与中键移动，边界来自网格，不再锁定 Y 或硬编码横向 MinX/MaxX。
- ConnectivityManager 从所有边界检查建筑通行，不指定任何敌人方向；建筑放置严格拒绝越界足迹，不将越界格夹回地图。
- 预放建筑与新建建筑采用相同注册入口，解决网格占用和住房容量漏登记；建筑销毁释放占地，住房销毁先注销再刷新人口。网格 Awake 显式先于建筑执行。
- 补齐总部建造面板初始化，使现有经营场景能独立操作。没有新增敌袭、资源采集、胜负或塔防路线玩法。

## 保留内容与资源迁移

- 保留机甲图纸、组件实例、装配解算、武器、伤害、Buff、ECA、敌人 AI、CP、库存、人口、生产、建筑、寻路、通用 UI、音效、特效与开发工具。这些能力无需节点进度即可存在。
- 敌人测试/精英敌人配置、尚未接入正式操作的主动技能 UI、通用音乐素材保留，未新增正式入口。
- 居民正在复用的事件图标改名为 ResidentMarker；库存正在复用的 LineDotPrefab 改为 Warehouse/SocketDot。所有迁移连同 .meta 保留原 GUID。
- 6 份原本丢失脚本的 Buff 配置迁移至现有 Action_ApplyBuffUniversal，保留 Buff 引用，单体模式与技能 PrimaryTarget 契约一致。
- 两份音效资产中失效的旧单音频字段更新为当前空音频池；原字段本就不被当前脚本读取，没有替换或猜测音效。

## 存档与未明确设计

项目没有磁盘存档实现，因此没有编造存档迁移器。SavedUnitProfile、库存和居民数据仍保留；旧节点状态类已删除，正式流程不再持有当前节点、层数或路线进度。

两份 Power 转伤害配置在清洗前已丢失实现，只被未入正式场景的原型引用。没有安全依据把旧 Power 映射为当前 EnginePower 或 CP，故移除失效资产与引用，并将原始 YAML、GUID、来源完整保存在 [ArchivedCombatConfigurations.md](ArchivedCombatConfigurations.md)。这是仍需设计背景才能恢复的内容，不会进入游戏。

## 验证结果

- Unity 2022.3.62f3c1 批处理在隔离副本完成导入、运行时与编辑器编译及资源检查。最终日志标记：`CLEANUP_VALIDATION_COMPLETE errors=0`。
- 实际执行主菜单按钮进入基地、4 座建筑注册/占地、总部建造 UI、居民招募、暂停/继续、返回主菜单及再次进入，全部通过。
- 3×11、11×3、7×7 网格的坐标往返、越界拒绝和右边界被阻断时的其他边界通行测试通过。
- 使用该 Unity 自带 Roslyn 编译器额外编译 Editor、运行时和 Standalone 条件分支，均无编译错误。原有未使用字段等警告未扩大清洗范围处理。
- 静态审计扫描 561 个文本资源/脚本，解析 11592 个资产与包 GUID，检查 129 个已删除 GUID；正式项目未解析 GUID、本地 fileID、遗留流程标记和已删资源引用均为 0。
- 额外对整个 Assets 解码中文 YAML 后搜索节点、楼层、房间、路线等内容；保留的 Floor 是地面物理/排序层，Node 是 Transform 或寻路节点，Stage 是 UI 挂载舞台；“下一层积木”是 ECA 递归注释，均非关卡推进。
- `git diff --check` 通过。删除前验证引用，删除时同时处理 .meta；迁移保留 GUID。
- 原有 TextMesh Pro 示例中仍有 39 条引用问题记录，均不在正式流程中，本次未修改第三方示例。清单见 [CleanupValidation.json](CleanupValidation.json)。
- 验证为无图形批处理，未进行画面/音频人工验收，未生成独立 Player 安装包。

复验：`python Tools/validate_assets.py`。Unity 使用隔离副本执行 `-batchmode -nographics -projectPath <copy> -executeMethod ProjectCleanupValidation.Run -logFile <log>`；不要加 `-quit`，验证器会在异步运行测试完成后退出。

## 全部实际文件变更

### 相机与测试敌人回归修复

- RTSCameraMover 改为移动 Camera_Root；ScreenEffectManager 继续控制子相机的局部震屏偏移，避免每帧将玩家移动复位。保留方向键/WASD、中键拖动与网格边界限制。
- RTS_World_Master 的 RTS_Logic 恢复 RTSTestBench 挂载及原敌人池、敌人 Prefab 配置，E 键在鼠标位置生成测试敌人。
- ProjectCleanupValidation 增加连续移动、鼠标位移换算、震屏后位置保持、边界限制与敌人生成/初始化检查。隔离副本 Unity 批处理通过，标记为 CAMERA_AND_TEST_ENEMY_VALIDATED 与 CLEANUP_VALIDATION_COMPLETE errors=0；静态引用审计无错误。测试调用输入对应的方法，未模拟真实键鼠事件。
- 本轮修改：Assets/Scripts/RTS/RTSCameraMover.cs、Assets/Scenes/RTS_World_Master.unity、Assets/Editor/ProjectCleanupValidation.cs，以及本报告和 CleanupValidation.json。

以下按 Git 实际状态逐项列出全部 294 个路径（包含 .meta 和迁移前后路径）。其中直接修改 30 个路径；真正删除 229 个文件；7 组资源/脚本连同 .meta 迁移；新增 7 个非迁移文件。未执行 git add 或 commit。

| 操作 | 完整路径 |
|---|---|
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Art/事件图标.png` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Art/事件图标.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/事件选项按钮.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/事件选项按钮.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/商店图标.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/商店图标.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图111.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图111.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图_12.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图_12.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图_15.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图_15.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图_20.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图_20.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图测试.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/地图测试.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/战斗图标.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/战斗图标.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/背景图/事件背景图.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/背景图/事件背景图.png.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/问号图标.png` |
| 删除 | `F:/UnityGame/Chimera/Assets/Art/问号图标.png.meta` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Audio/BGM/BGM_地图.mp3` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Audio/BGM/BGM_地图.mp3.meta` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/1_Blueprints (装备与底盘图纸)/2_Components/Core/CORE1_陷阵核心/ECA/ACT_施加攻速buff.asset` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/1_Blueprints (装备与底盘图纸)/2_Components/Weapon/WPN1.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/1_Blueprints (装备与底盘图纸)/2_Components/Weapon/WPN1_聚能电磁炮/ECA/L1_BoostDamageByPower.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/1_Blueprints (装备与底盘图纸)/2_Components/Weapon/WPN1_聚能电磁炮/ECA/L1_BoostDamageByPower.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线).meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables/LootSequence.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables/LootSequence.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables/中期战利品池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables/中期战利品池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables/前期战利品池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/LootTables/前期战利品池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/ShopPools.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/ShopPools/TestShopPool.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/3_Economy_Bazaar (经济与掉落管线)/ShopPools/TestShopPool.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件).meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/SAN值归零.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/SAN值归零/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/SAN值归零/ECA/Act_BackToMenu.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/SAN值归零/ECA/Act_BackToMenu.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/SAN值归零/失败.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/SAN值归零/失败.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/A_减少SAN值上限.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/A_减少SAN值上限.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/A_发放组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/A_发放组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/A_恢复至满SAN.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/A_恢复至满SAN.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/C_SAN值判定.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/ECA/C_SAN值判定.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/侵蚀.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/侵蚀/侵蚀.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第一层失败_扣SAN.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第一层失败_扣SAN.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第一层探索.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第一层探索.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第三层探索.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第三层探索.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第二层探索.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/ECA/A_第二层探索.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/前厅探索.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/前厅探索.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/厄舍府.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/厄舍府.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/庭院搜索.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/庭院搜索.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第一层探索失败.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第一层探索失败.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第一层探索成功.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第一层探索成功.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第二层探索失败.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第二层探索失败.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第二层探索成功.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/第二层探索成功.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/终极探索.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/厄舍府的倒塌/终极探索.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/底盘池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/底盘池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/战斗后资源掉落.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/战斗后资源掉落.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/教程战斗.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/教程战斗.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/新手战斗布局.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/新手战斗布局.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/核心池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/核心池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/武器池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/武器池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/移动池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/移动池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得底盘.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得底盘.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得核心组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得核心组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得武器组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得武器组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得移动组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得移动组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得辅助组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/获得辅助组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/辅助池.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/ECA/辅助池.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/什么是奇美拉？.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/什么是奇美拉？.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/出战.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/出战.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/尾声.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/尾声.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/废土新人.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/废土新人.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/怎么组装奇美拉？.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/怎么组装奇美拉？.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_核心组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_核心组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_武器组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_武器组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_移动组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_移动组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_辅助组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉_辅助组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉！.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/废土新人/组装奇美拉！.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/ECA/A_恢复SAN值.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/ECA/A_恢复SAN值.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/ECA/A_最大产电量增加.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/ECA/A_最大产电量增加.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/曙光.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/曙光/曙光.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/ECA/A_发放丑橘眼泪.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/ECA/A_发放丑橘眼泪.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/ECA/A_发放哈基之心.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/ECA/A_发放哈基之心.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/最可爱的基咪.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/最可爱的基咪.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/选择右边.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/选择右边.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/选择左边.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/最可爱的基咪/选择左边.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/终局.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/终局/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/终局/ECA/Act_BackToMenu.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/终局/ECA/Act_BackToMenu.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/终局/终局.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/终局/终局.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA/敌人布局.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA/敌人布局.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA/获得魔法组件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA/获得魔法组件.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA/进入战斗.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/ECA/进入战斗.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/聚集于此.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/聚集于此/聚集于此.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/增加魔力上限.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/增加魔力上限.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/恢复SAN值.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/恢复SAN值.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/揭示地图.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/揭示地图.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/科技盲盒.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/ECA/科技盲盒.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/麦田怪圈.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventDatas/麦田怪圈/麦田怪圈.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventPools.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventPools/EventPool1.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventPools/EventPool1.asset.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventPools/引导事件.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/5_Meta_Events (文字冒险事件)/EventPools/引导事件.asset.meta` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/BuffECA/Action_ApplyAcid.asset` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/BuffECA/ApplyBuff.asset` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/CombatECA/ApplyBuff.asset` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/CombatECA/ApplyBuff2.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/CombatECA/BoostDamageByPower.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/CombatECA/BoostDamageByPower.asset.meta` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/EffortECA/HitPlaySound.asset` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/EffortECA/ShootPlaySound.asset` |
| 修改 | `F:/UnityGame/Chimera/Assets/Data/6_ECA_Blocks (行为逻辑积木)/主动技能/ACT_ApplyOverclock.asset` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Hangar.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Hangar/HangarSlot.prefab` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Hangar/HangarSlot.prefab.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map.meta` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map/LineDotPrefab.prefab` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map/LineDotPrefab.prefab.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map/MapLinePrefab.prefab` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map/MapLinePrefab.prefab.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map/MapNodeUI.prefab` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Map/MapNodeUI.prefab.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/EventOptionPrefab.prefab` |
| 删除 | `F:/UnityGame/Chimera/Assets/Prefabs/EventOptionPrefab.prefab.meta` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scenes/RTS_World_Master.unity` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scenes/Scene_MainGame.unity` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scenes/Scene_MainGame.unity.meta` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scenes/Scene_MainMenu.unity` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/10_Effort/Audio/UISoundAtlasSO.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/10_Effort/Music/MusicManager.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/10_Effort/Music/MusicState.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/11_Save/MainMenuUI.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/1_ Core (全局核心枢纽)/GameCoreTypes.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/2_Entities (实体与行为底层)/MechUnit2D.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/4_ Combat_Director (战斗沙盘导演)/CombatDirector.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/8_UI_Frontend (前端交互表现层)/MapNodeUI.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/8_UI_Frontend (前端交互表现层)/MapNodeUI.cs.meta` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/8_UI_Frontend (前端交互表现层)/机甲装配相关/AssemblyWorkshopUI.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/8_UI_Frontend (前端交互表现层)/物品与仓库/GlobalWarehouseUI.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/RTS/RTSCameraMover.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/RTS/RTSGridSystem.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/RTS/RTSMapVisuals.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/建筑物/BuildingBase.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/建筑物/BuildingManager.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/建筑物/ConnectivityManager.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/建筑物/HousingBuilding.cs` |
| 修改 | `F:/UnityGame/Chimera/Assets/Scripts/新UI/SelectionContextHUD.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventDirector.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventDirector.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_BackToMenu.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_BackToMenu.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_OpenSpecificUpgrade.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_OpenSpecificUpgrade.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_ProbabilityNode.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_ProbabilityNode.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_ScoutMap.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_ScoutMap.cs.meta` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_UniversalModify.cs` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventAction_UniversalModify.cs.meta` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventCondition_Universal.cs` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventCondition_Universal.cs.meta` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventECATypes.cs` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventECATypes.cs.meta` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventECA_Core.cs` |
| 迁移来源 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventECA/EventECA_Core.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventNodeSO.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventNodeSO.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventOptionUI.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventOptionUI.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventPoolConfigSO.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/EventPoolConfigSO.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/GlobalProtocolRegistry.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_事件系统/GlobalProtocolRegistry.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapGenerator.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapGenerator.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapLineDrawer.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapLineDrawer.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapManager.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapManager.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapNodeData.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapNodeData.cs.meta` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapVisualizer.cs` |
| 删除 | `F:/UnityGame/Chimera/Assets/Scripts/遗老_节点地图系统/MapVisualizer.cs.meta` |
| 修改 | `F:/UnityGame/Chimera/ProjectSettings/EditorBuildSettings.asset` |
| 修改 | `F:/UnityGame/Chimera/README.md` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Art/ResidentMarker.png` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Art/ResidentMarker.png.meta` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Audio/BGM/BGM_Base.mp3` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Audio/BGM/BGM_Base.mp3.meta` |
| 新增 | `F:/UnityGame/Chimera/Assets/Editor/ProjectCleanupValidation.cs` |
| 新增 | `F:/UnityGame/Chimera/Assets/Editor/ProjectCleanupValidation.cs.meta` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Warehouse/SocketDot.prefab` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Prefabs/1_UI_Frontend(UI界面)/Warehouse/SocketDot.prefab.meta` |
| 新增 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents.meta` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventAction_UniversalModify.cs` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventAction_UniversalModify.cs.meta` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventCondition_Universal.cs` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventCondition_Universal.cs.meta` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventECATypes.cs` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventECATypes.cs.meta` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventECA_Core.cs` |
| 迁移目标 | `F:/UnityGame/Chimera/Assets/Scripts/GameplayEvents/EventECA_Core.cs.meta` |
| 新增 | `F:/UnityGame/Chimera/Docs/ArchivedCombatConfigurations.md` |
| 新增 | `F:/UnityGame/Chimera/Docs/CleanupValidation.json` |
| 新增 | `F:/UnityGame/Chimera/Docs/LegacyCleanupReport.md` |
| 新增 | `F:/UnityGame/Chimera/Tools/validate_assets.py` |
