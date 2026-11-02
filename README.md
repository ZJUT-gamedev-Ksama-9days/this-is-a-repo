# this-is-a-repo · dev-phys

`dev-phys` 是项目的 **2D 物理与角色交互原型分支**，用于验证「键盘控制主角推球 + 鼠标拖动局部重力方框」的核心玩法。

物理计算基于 Unity `Rigidbody2D` / `Collider2D`，不是重新实现一套碰撞引擎。

## 分支功能

### 可拖动的局部重力方框

- 游戏画面内的方框区域，不是独立的系统窗口。
- 鼠标左键按住框内拖动；保持点击偏移，并将整个方框限制在相机画面内。
- 小球的世界空间质心进入方框后，使用框内重力；离开后恢复世界重力。
- 默认区域 `4 × 4`，框内向上加速度 `18 m/s²`；方向、强度、尺寸和优先级可在 Inspector 调整。
- 切换重力时保留现有速度，不重复叠加默认重力；不同质量物体获得相同加速度。
- 方框没有实体碰撞边框；每个物理步重新判断区域，可唤醒被方框覆盖的休眠刚体。
- 只有挂载 `GravityBody2D` 的动态刚体参与局部重力。多区域重叠按优先级选择，不叠加力。

### 主角移动与推球

- A/D 或左右方向键移动，空格/W/上方向键跳跃。
- 支持加速、刹车、空中控制、短暂离地容错、跳跃输入缓冲和斜坡行走；不支持二段跳。
- 通过真实刚体接触推球，不直接修改小球速度。
- 主角保持正常向下重力，不受重力方框影响。
- R 键同时重置主角、小球和方框，并清空速度及待执行的跳跃输入。

### 原型生成与测试

- Editor 菜单可用代码生成试验场、占位角色、小球、重力框、碰撞材质和 Prefab。
- 提供 PlayMode 自动化测试，覆盖局部重力、碰撞、拖动坐标、角色移动、跳跃、推球、斜坡与重置。

## 提交范围

本分支只提交：

- `Assets/GravityPrototype/Scripts/`：运行时代码与程序集配置。
- `Assets/GravityPrototype/Editor/`：测试场景和占位资产生成工具。
- `Assets/GravityPrototype/Tests/PlayMode/`：自动化测试与测试程序集配置。
- 上述文件及目录对应的 `.meta`，保留代码 GUID。
- 本 README；沿用仓库已有的 Unity `.gitignore`。

**不包含**完整 Unity 工程配置、预生成场景、Prefab、美术资产、`Library`、`Logs`、构建产物或本地工具数据。因此不能直接把这个代码仓库当作已配置完成的 Unity 工程启动；需要先接入 Unity 工程并生成试验场。

## 环境与快速运行

1. 使用 **Unity 6000.3.27f1** 创建或打开一个 **2D / Built-in Render Pipeline** 工程。
2. 将本分支的 `Assets/GravityPrototype` 目录及同级 `GravityPrototype.meta` 放入工程的 `Assets`，保持目录结构和 `.meta`。
3. 在 Package Manager 安装 **Test Framework**（本地验证版本为 `com.unity.test-framework` **1.1.33**），供测试程序集编译。
4. `Project Settings > Player > Active Input Handling` 设为 **Input Manager (Old)** 或 **Both**。本分支使用旧版 `UnityEngine.Input`，不依赖新 Input System。
5. 保持 `Physics 2D` 世界重力为 `(0, -9.81)`；等待脚本编译完成。
6. 在 **Edit Mode** 执行 `Tools > Gravity Prototype > Open or Create Physics Sandbox`。
7. 新生成的场景位于 `Assets/GravityPrototype/Scenes/PhysicsSandbox.unity`，点击 Play 后先点击 Game 窗口获取键盘焦点。

> 生成器会在目标工程的 `Assets/GravityPrototype/` 下创建场景、Art 与 Prefabs，设置 2D 编辑模式、屏幕尺寸和 `0.02 s` 物理步长，并将试验场加入 Build Settings；建议先在空白或独立的原型工程中运行。已有的同路径场景不会被覆盖。如果已有试验场缺少主角，可在打开该场景后执行 `Tools > Gravity Prototype > Add Player to Current Sandbox`，然后保存场景。

### 操作

| 操作 | 按键 |
| --- | --- |
| 主角左右移动 | A/D、左右方向键 |
| 跳跃 | 空格、W、上方向键 |
| 推球 | 控制主角走向小球 |
| 拖动重力方框 | 鼠标左键按住框内拖动 |
| 全部重置 | R |

推荐验证顺序：推球 → 拖框覆盖小球 → 观察向上加速 → 移开方框观察恢复向下加速 → R 重开。

## 代码结构

| 文件 | 职责 |
| --- | --- |
| `Scripts/GravityRegion2D.cs` | 区域尺寸、世界重力方向、加速度、优先级及质心包含判定 |
| `Scripts/GravityBody2D.cs` | 物理步选择生效区域，以重力差值施力、保留动量、唤醒休眠刚体 |
| `Scripts/GravityFrameDrag2D.cs` | 鼠标坐标映射、拖动偏移、画面约束和失焦取消拖动 |
| `Scripts/GravityFrameView.cs` | 方框边线、填色及重力方向箭头 |
| `Scripts/PlayerController2D.cs` | 主角水平驱动、接触落地判定、跳跃、朝向和出生点重置 |
| `Scripts/PhysicsSandboxController.cs` | 原型输入提示、主角/小球/方框同步重置 |
| `Editor/PhysicsSandboxBuilder.cs` | 试验场、占位资产和 Prefab 生成；场景检查与预览 |
| `Tests/PlayMode/` | 物理与真实场景集成回归测试 |

上表路径均相对于 `Assets/GravityPrototype/`。

## 调参和接入约定

- `GravityRegion2D.direction` 使用世界坐标：`(0, 1)` 向上，`(1, 0)` 向右，`(0, 0)` 失重；方向向量会归一化。
- `Rigidbody2D.gravityScale` 同时缩放世界重力和区域重力，设为 0 时两者均不生效。
- 仅碰撞体边缘接触方框不算进入；质心在边界上算框内。瞬间在两个物理步之间扫过并离开的方框不会额外施加冲量。
- 多区域优先级相同时，后启用的区域生效。
- 方框画面约束按无旋转的正交相机实现；多相机工程建议显式指定 `inputCamera`。
- 主角默认最大移动速度 `5.5 m/s`，起跳速度 `8.5 m/s`；可在 `PlayerController2D` 调整。
- Play Mode 中的场景调参不会自动持久化；需要保留时请在 Edit Mode 修改。

## 验证状态

已用本分支的纯代码副本在全新的 **Unity 6000.3.27f1** 工程中重新生成试验场并运行回归：**26/26 项 PlayMode 测试通过**，编译和测试日志中无脚本编译错误或运行异常。原工作工程的实际 Game 视图也已检查。

在接入的工程中复现测试：

1. 按上述步骤生成试验场，确认它已加入 Build Settings。
2. 打开 `Window > General > Test Runner` 的 **PlayMode** 页。
3. 运行程序集 `GravityPrototype.PlayModeTests`。

其中区域力学测试使用隔离的 `PhysicsScene2D`；场景集成和角色测试会加载生成的试验场。移动、跳跃和鼠标拖动的主观手感仍需试玩调整。

## 暂未实现

目标点、死亡/通关流程、五个正式关卡、杠杆、运行时重力方向切换操作、存档、音效和正式美术动画。当前分支聚焦核心物理与角色交互验证。
