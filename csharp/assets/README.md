# 素材目录

运行时素材统一放在本目录，对应引擎路径 `res://assets/`。`data/` 只保存内容定义，通过路径或 ID 引用这里的素材。

- `textures/`：贴图、图标和 UI 切图。
- `sprites/`：单位、建筑、卡牌立绘与帧动画。
- `audio/sfx/`：音效。
- `audio/music/`：背景音乐。
- `fonts/`：字体资源。当前是 `SystemFont` 包装（引擎默认字体不含中文字形），跨平台分发前需替换为随包的 OFL 授权中文字体。
- `shaders/`：`.gdshader` 着色器。
- `themes/`：UI 主题（`Theme` 资源）。
- `models/`：3D 模型及其材质。

## 约定

- 导入元数据必须入库：Godot 为每个素材生成的 `.import` 与 `.uid` 文件需要一起提交，否则其他机器会重新分配 UID 并使场景引用失效。导入缓存 `.godot/` 已在 `.gitignore` 中排除。
- 源工程文件（`.psd`、`.aseprite`、`.blend`、未压缩音频母带）放在仓库根的 `raw_assets/`，不要放入本目录：引擎会尝试导入它们。`raw_assets/` 带 `.gdignore` 且默认不入库。
- 全屏背景一类的大图在 `.import` 中开启 `mipmaps/generate` 并设置 `process/size_limit`，避免缩小采样产生摩尔纹，同时压低显存占用。
- 文件名使用小写加下划线，同一素材的变体保留统一前缀，便于场景中稳定引用。

## Git LFS

本目录下的图像（`png` / `jpg` / `jpeg` / `webp`）、音频（`wav` / `ogg` / `mp3`）和模型（`glb` / `fbx`）由 Git LFS 管理，规则见仓库根的 `.gitattributes`。

- 首次克隆后执行 `git lfs install` 和 `git lfs pull`，否则工作区里的素材只是文本指针文件。
- 新增其他二进制格式时同步更新 `.gitattributes`，不要在 LFS 之外提交大体积素材。
- `.import`、`.uid`、`.gdshader` 等文本文件不走 LFS，保持普通 git 跟踪以便 diff。
