# 回归验证入口

## 纯 .NET 自检

在项目根目录运行：

```powershell
dotnet build csharp.sln --no-restore
dotnet run --project tests/PlanetGeneration.Core.SelfTest --no-build -- --quick
dotnet run --project tests/PlanetGeneration.Core.SelfTest --no-build -- --full
dotnet run --project tests/PlanetGeneration.Core.SelfTest --no-build -- --list
dotnet run --project tests/PlanetGeneration.Core.SelfTest --no-build -- --suite compatibility
dotnet run --project tests/PlanetGeneration.Core.SelfTest --no-build -- --suite snapshot
dotnet run --project tests/PlanetGeneration.Core.SelfTest --no-build -- --suite persistence
```

- 不带参数等同于 `--full`，不会跳过原有测试。
- `--quick` 运行缓存、图层、旧入口对照和规划指纹；适合修改后立即反馈。
- 分组包括 `geometry`、`simulation`、`layers`、`cache`、`terrain`、`cartography`、`compatibility`、`snapshot`、`planning`、`persistence`。
- 退出码：0 全部通过，1 存在失败，2 参数错误或分组为空。
- 每项输出独立耗时；完整几何检查含 32768 档位，不应拿其耗时代表实际地图生成耗时。
- 各 `Program.*Tests.cs` 按领域组织，`Program.cs` 只负责注册、选择、运行和报告。
- 工程链接旧 Polygon 适配层和无 Godot 依赖的档案头读取器，以便在不启动引擎的情况下检查应用边界。Core 项目本身不反向依赖 scripts。

## Godot 应用边界回归

```powershell
godot --headless --path . scenes/Tests/RefactoringVerification.tscn
```

该场景退出时用进程退出码标识成功/失败，验证：

1. 地貌/树木资源目录与森林备用目录可以加载。
2. 空文本资源会继续尝试备用路径。
3. Main 的 Balanced/Legacy 参数正确传递给 Core。
4. 同地块数、不同几何的 A/B 世界不会共用像素归属缓存。
5. 生态/文明缓存失效不清理地理数据与不相关图层。
6. 新 LLM 请求替换旧请求时，旧请求结束不能覆盖新请求状态（不加载模型）。
7. 11 个程序化备用纹理的像素哈希与搬移前基线一致。
8. 使用真实 BaseFieldGeneratorAdapter 验证 A/B 两组各调用一次生成、单组只调用一次。
9. 从快照投影出的地形、水文、分类、城市、生态、文明、事件与 Core 数据一致。
10. A/B 完整快照存档往返后，底图像素一致；Main 磁盘缓存恢复同一套领域数据。
11. 时间轴更新发布新快照，原始内存缓存仍保留生成时的纪元；新会话不复用被修改的旧投影。
12. 旧 JSON 字典档案仍能导入，且不会伪装成缺失的权威快照。
13. 请求预先取消或 B 组生成失败时，不返回可发布的半成品会话。

纯 .NET `persistence` 分组还验证完整几何/字段/连续风场/制图指令序列化、错误列长度、CSR 越界、空间索引间距校验、损坏头部，以及保存失败时原档案不被覆盖。

缺少 `resources/textures/guohua/forest_atlas.json` 时，森林目录会记录警告并使用原有内置目录；警告不等于测试失败。

## 基线更新规则

- `Program.SnapshotTests.cs` 中的规划指纹覆盖固定种子、尺寸、地块数、蓝图下的布局计划和字段。
- `baselines/guohua-fallback-textures.json` 记录备用纹理的 RGBA 像素哈希。
- 这些基线在重构前的本地 .NET 8 / Godot 4.7.2 环境记录。升级运行时或有意调整算法时，必须检查差异并注明原因，不能仅为了让测试通过而刷新指纹。
- 无界面回归不替代交互式平移/缩放、完整国画组合画面的视觉检查或真实 LLM 模型推理测试。


## 重构提交与工作区的测试数量

2026-09-23 的独立重构提交包含 23 项完整自检，已在仅包含暂存区内容的独立目录通过构建和 Godot 回归。
工作区另外保留了未提交的 Map Effects 功能及其测试，所以完整工作区运行可显示 24 项。二者不是同一套提交内容；
新增制图功能的测试应随其实现一起提交，不应仅将测试注册项移入重构提交。
