# UI 可用性修整

保留现有布局与未实现功能入口，不增加经营或塔防玩法。

## 已调整

- 居民日志解除放逐回调；建筑升级、建筑拆除、居民日志、详情页机甲拆解均显示“尚未实现”。放逐按钮仍保留原有功能。
- 新增不拦截鼠标的屏幕提示，复用项目中文字体，按未缩放时间显示，因此暂停时也可使用。
- 生产货架悬停显示成本和生产时间；资源不足显示各资源缺口；装配校验失败始终提供屏幕提示。
- 距离组装厂过远时，改装/回收按钮保留禁用状态，悬停说明原因。
- Esc 优先结束文本输入/关闭展开下拉框，再按打开顺序关闭仓库、机甲详情、装配材料选择和装配车间；车间通过已有取消入口回滚。没有窗口时，取消建造或切换暂停。
- 修正“ESC退出游戏”等文案，暂停页改为当前操作说明；统一部分机甲术语与基础字号。资源/属性 28、按钮 24、名称 36，并限制自动缩放范围。
- Canvas 使用分明的 HUD、详情、特效、暂停和提示层级；主菜单与基地采用一致的缩放匹配值。未改变地图比例或游戏布局方向。
- 建造点击不再穿透 UI；资源栏销毁时解除事件订阅。

## 验证

Unity 2022.3.62f3c1 最终结果：`UI_INTERACTIONS_VALIDATED`、`CLEANUP_VALIDATION_COMPLETE errors=0`。日志：`C:\Users\20723\AppData\Local\Temp\chimera-validation-xj6qhncd\ui-validation-settings-refresh.log`。静态引用审计无错误，`git diff --check` 通过。

使用项目外隔离副本运行 Unity 编译及 ProjectCleanupValidation。新增检查覆盖实际日志按钮绑定、未实现功能提示、资源不足不扣费/不入队、窗口逐层返回及暂停时提示。并保留相机移动、震屏、测试敌人、基地进入/退出等既有回归检查。

批处理不等于视觉验收：未进行真实鼠标键盘操作、各分辨率截图对照或独立 Player 构建。复杂页面间距和最终美术仍需后续运行画面验收。

## 本轮文件清单

- `Assets/Editor/ProjectCleanupValidation.cs`
- `Assets/Scenes/RTS_World_Master.unity`
- `Assets/Scenes/Scene_MainMenu.unity`
- `Assets/Scripts/11_Save/PauseMenuUI.cs`
- `Assets/Scripts/8_UI_Frontend (前端交互表现层)/GlobalResourceHUD.cs`
- `Assets/Scripts/8_UI_Frontend (前端交互表现层)/机甲装配相关/AssemblyWorkshopUI.cs`
- `Assets/Scripts/8_UI_Frontend (前端交互表现层)/物品与仓库/GlobalWarehouseUI.cs`
- `Assets/Scripts/8_UI_Frontend (前端交互表现层)/物品与仓库/RightInventoryPanelUI.cs`
- `Assets/Scripts/8_UI_Frontend (前端交互表现层)/详情页UI/UnitDetailPanelUI.cs`
- `Assets/Scripts/建筑物/BuildingManager.cs`
- `Assets/Scripts/建筑物/FactoryBuilding.cs`
- `Assets/Scripts/新UI/FactoryUIModule.cs`
- `Assets/Scripts/新UI/SelectionContextHUD.cs`
- `Assets/Scripts/新UI/UIBackHandler.cs`
- `Assets/Scripts/新UI/UIBackHandler.cs.meta`
- `Assets/Scripts/新UI/UIFeedback.cs`
- `Assets/Scripts/新UI/UIFeedback.cs.meta`
- `Assets/Scripts/新UI/UIHoverHint.cs`
- `Assets/Scripts/新UI/UIHoverHint.cs.meta`
- `Docs/UIUsabilityFixes.md`
