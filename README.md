# Chimera

Unity 2022.3.62f3c1，殖民地居民管理、资源经营与模块化机甲塔防原型。

启动顺序：`Scene_MainMenu` → “进入基地” → `RTS_World_Master`。Build Settings 仅包含这两个正式场景，也可直接运行基地场景。

当前已接通居民工作、工厂并行生产、自动物流、独立品质组件、单仓库库存、机甲装配与存档恢复。正式资源采集、居民驾驶、敌袭波次和胜负闭环尚未接通。

## 阅读入口

- [项目目录与 Hierarchy](Docs/ProjectStructure.md)：当前文件位置、分组约定及整理菜单。
- [阶段进度](Docs/ProjectProgress-2026-09-27.md)：已完成范围、尚未实现内容和建议顺序。
- [50 PPU 美术规范](Docs/WorldPixel50.md)：世界素材、手绘原图、挂点和旋转规则。
- [UI 主题与美术替换](Docs/UI/主题与美术替换.md)：主题、字体、图片和九宫格配置。
- [自动物流](Tools/Regression/自动物流说明.md)：运输规则与当前边界。
- [存档基础](Docs/SaveSystem.md)：稳定 ID 与捕获/恢复约定；当前代码版本为 5。

`Assets/Scripts` 按功能组织；配置实例放 `Assets/Data`；编辑器工具集中在 `Assets/Editor`。运行时按路径加载的资源留在 `Assets/Resources`。项目外的 `ArtSource` 保留源稿，`ArtReview` 保存美术审阅页面。

当前基地地图为 64×64，可在 **Tools → Chimera → 地图 → 地图设置** 中调整矩形尺寸并更新编辑器预览。地图规格目前属于场景配置。

## 检查

只读引用检查：`python Tools/validate_assets.py`。

功能回归在隔离工程运行，说明见 [Tools/Regression/README.md](Tools/Regression/README.md)。测试会创建临时居民、库存或切换场景，不在正在编辑的主项目运行。最新目录整理记录位于 `Tools/Regression/Artifacts/ProjectOrganization`。

早期清洗与代码分析文档作为历史记录保留，其中的旧路径、旧存档版本和功能缺口不应直接作为当前状态依据。
