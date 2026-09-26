# 仓库与居民派遣回归检查

`WarehouseRegressionChecks.cs` 是 Unity 编辑器测试入口，放在 Tools 下，不进入游戏程序集。

运行时应将项目的 Assets、Packages、ProjectSettings 复制到隔离目录，再将此脚本复制到该副本的 Assets/Editor。不要在正在编辑的原项目上执行：检查会进入 Play Mode，并临时创建测试库存与居民。

使用与项目一致的 Unity 编辑器启动副本：

```text
Unity.exe -batchmode -projectPath <副本绝对路径> -executeMethod WarehouseRegressionChecks.Run -logFile <日志绝对路径>
```

不传 `-quit`，脚本在 Play Mode 检查结束后自行以 0（通过）或 1（失败）退出。需要图形设备生成 UI 截图，因此不传 `-nographics`。输出位于副本的 RegressionResults 文件夹。

检查覆盖：

- 路径平滑在全部后续线段受阻时终止；绕障、不可达目标、阻塞入口与寻路时间上限。
- 4 人竞争 3 岗位的预留、取消、再次派遣及到达登记。到达登记在测试中直接触发，不代替长期物理移动压测。
- 组件独立标识、组件逐件显示、底盘逐件显示、40 项分页、详情以及窗口重复开关。
- 下拉菜单透明点击拦截层不会被主题刷新涂成不透明背景。
- 1920 × 1080 仓库组件页及下拉菜单截图。

首次启动副本需要导入资产。离线机器可把原项目 Library/PackageCache 中的包复制为副本的嵌入包，目录名去掉 @版本号。
