# 代码维护检查

需要Python 3.9+，不需要额外Python库。Unity项目至少打开过一次，以便Library/Bee生成编译响应文件。

从项目根目录运行：

```powershell
python Tools/CodeMaintenance/validate_code.py
python Tools/CodeMaintenance/validate_code.py --compile
python Tools/CodeMaintenance/validate_code.py --tests
```

第一条检查自有脚本meta和类型名、GUID重复、场景/预制体脚本引用、Runtime中的UnityEditor依赖，并报告字节相同的png/json/shader，不自动删除。引用检查会查询本地PackageCache；未恢复依赖包时需要先用Unity完成包恢复。

第二条分别编译运行与Editor代码。默认使用本机Unity 2021.3.45f1c2；不同安装路径可以附加 `--unity-editor "D:/Unity/Editor"`。编译从当前Assets重建源码列表，旧目录不依赖Bee是否已刷新；仅将DLL和响应文件写到Temp/CodeMaintenance。

第三条编译后运行CodeChecks.cs：核对图集与切片UV、相邻帧偏移、淡出和激光持续帧，以及绝对调度、区间测试索引和P3循环。测试不创建GameObject，不启动Play Mode，因此不验证Unity的原生渲染、动画观感或实际碰撞。

该目录在Assets以外，不会作为游戏组件导入。没有新增asmdef，也不改变项目的程序集边界。
