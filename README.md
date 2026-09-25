# Chimera

Unity 2022.3.62f3c1，模拟经营与塔防方向的机甲项目。

正式启动顺序：`Scene_MainMenu` → 点击“进入基地” → `RTS_World_Master`。
Build Settings 只包含这两个场景。也可以直接打开基地场景运行。

基地保留建筑放置、居民、工厂生产、机甲装配、库存与独立战斗能力。
资源采集、敌袭波次和胜负条件尚未设计，本次清洗没有补充这些玩法。
尚无磁盘存档实现，主菜单不展示不可用的读档按钮。

网格宽、高、格子尺寸与原点独立配置；现有场景尺寸只是样例。
相机支持 WASD／方向键和鼠标中键二维移动。建筑通路检查同等对待四侧边界，
不定义敌人出生方向或路线数量。节点地图、爬塔推进及其场景、事件和 UI 已移除。

只读资源检查：`python Tools/validate_assets.py`。
Unity 批处理检查入口：`ProjectCleanupValidation.Run`，应在仅复制 Assets、Packages、
ProjectSettings 的临时项目中执行；它会检查资源和场景，并自动运行主菜单进入基地、
建筑注册、招募、暂停、返回与再次进入的冒烟测试。

详细范围、验证结果与完整文件清单见 `Docs/LegacyCleanupReport.md`。
