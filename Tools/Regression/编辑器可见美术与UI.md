# 编辑器可见的建筑和 UI

此前的主题控制器每 0.75 秒覆盖场景样式，仓库和物流窗口在运行时创建，建筑 Sprite 也是临时对象。现在将固定外观写入正式场景、预制体和图集子资源，运行时复用这些引用。

## 使用

- 直接打开 `Assets/Scenes/RTS_World_Master.unity`，现有建筑已经引用新图样。
- 建筑预制体在 `Assets/Prefabs/建筑物预制体`，仓库建筑在 `Assets/Resources/Buildings/Warehouse.prefab`。
- 仓库、物流与名册的可编辑预制体在 `Assets/Resources/UI`，名称分别为 `GlobalWarehousePanel`、`LogisticsPanel`、`ResidentRosterPanel`。场景中的窗口可能处于关闭状态；可以选中并启用，或使用 `Tools/Chimera/美术与UI/预览…（编辑模式）` 定位并显示。预览激活状态支持撤销。
- 地图中的仓库仍由物流系统按可达性安排初始位置；仓库的外观与结构已经是正式预制体，不再是临时绘制的箱子。
- 调整 SpriteRenderer、Image、RectTransform、TMP_Text 后照常保存场景或预制体。Play 模式不再周期覆盖配色，已保存的建筑缩放与居民信息区尺寸也会保留。
- 若主动想重置某个对象的主题，选中它并使用 `对选中对象重新应用统一样式`。该操作支持撤销，不会自动定时执行。

库存条目、居民列表、机甲拼装预览等依赖游戏数据的内容仍在运行时填充，这是内容更新，不是替换整个固定界面。

## 迁移与保护

`ChimeraVisualAuthoring` 负责一次性迁移；场景内的 `Chimera_AuthoredVisuals_v1` 表示已完成，重复同步不会重建窗口或覆盖后续手工调整。不要将这个空标记作为游戏对象删除。

已打开的旧场景在资源就绪后会原位迁移：原先没有未保存修改时自动保存；已有未保存修改时只标记脏状态，保留用户的保存决定。不强制重载编辑器场景。

建筑图集使用 Multiple Sprite 导入和稳定子资源引用，原有场景实例上的旧贴图覆盖也在迁移时处理。原有占地、交互和存档标识没有改变。运行时新建对象仍有兼容性兜底，但已有可编辑结构不会再次生成。

## 验证

`EditorVisualAssetChecks.Run` 在隔离 Unity 副本中验证：编辑模式重开资源、建筑 Sprite 引用持久化、窗口与血量标签不重复、颜色与布局跨 Play 模式保留、关闭/筛选/页签事件绑定、没有周期主题控制器。`LogisticsRegressionChecks.Run` 验证搬运和完整存取档流程。

测试结果和真正由编辑模式渲染的仓库截图保存于 `Tools/Regression/Artifacts/EditorVisuals`。

2026-09-27：28 项编辑器资源与交互验证通过，39 项物流回归通过。实际同步清单见测试目录的 `published-assets.json`。没有覆盖主项目玩家存档或字体资产。
