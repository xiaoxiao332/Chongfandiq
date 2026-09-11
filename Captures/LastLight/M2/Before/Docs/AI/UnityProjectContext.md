# Unity 项目上下文

## 环境

- 项目根目录：`D:/BaiduSyncdisk/unity/Chongfandiq`；当前未初始化 Git。
- Unity 6000.3.21f1，URP 17.3.0。
- Input System 1.20.0，复用 `Assets/InputSystem_Actions.inputactions`。
- Addressables 2.9.1、AI Navigation 2.0.14、uGUI 2.0.0、Test Framework 1.6.0。
- Unity MCP：Coplay v10.2.0；每次操作前重新确认当前项目、编译和 Play Mode。
- Blender：`D:/BaiduSyncdisk/blender/blender.exe`，5.2.0 LTS；使用后台 Python → FBX，无 Blender MCP。

## 内容与入口

- LastLight M1：`Assets/LastLight/Scenes/M1Prototype.unity`；菜单 `Tools > LastLight > M1 > Open Prototype`。
- M1 生成内容：`Assets/LastLight/M1Generated/`；生成器 `Assets/LastLight/Editor/M1PrototypeBuilder.cs`。
- LastLight 架构样例：`Assets/LastLight/ArchitectureSample/Scenes/Bootstrap.unity`。
- SnowVillage 视觉样板：`Assets/SnowVillage/Scenes/SnowVillage.unity`，禁止由 LastLight 工具覆盖。
- LastLight 模型源：`ArtSource/LastLight/`；运行资产：`Assets/LastLight/`。

## 架构速查

- `GlobalManager` 持有 Application 系统与当前 Session；系统按 Application、Session、Scene 三种生命周期管理。
- `SceneFlowSystem` 通过 Addressables Additive 加载家园和工坊，提交后卸载旧内容场景。
- `ResourceSystem` 是 Addressables、实例池和场景租约的唯一入口。
- `UIManager` 使用 BG、Window、Pop、Over 四层；`PauseSystem` 集中管理暂停令牌。
- M1 权威状态分别由经济、建造、生存、叙事、威胁会话系统持有，`M1GameSession` 负责场景适配和命令提交。
- `LastLight.Core` 保持纯 C# 规则；Runtime、Framework、Editor 和测试程序集分离。

## 导航约束

- 所有角色寻路必须使用 Unity 官方 **AI Navigation + NavMeshAgent**，不得自创网格 BFS、A* 或局部绕障系统。
- 家园和工坊各有独立 `NavMeshSurface`，只收集本内容场景的静态碰撞几何。
- 可建造墙、门框、设备和路障通过 `NavMeshObstacle` carving 更新通行；建筑归零时关闭障碍，维修后恢复。
- 梁使用 `NavMesh.CalculatePath` 选择可达维修站位；`M1Navigator` 只适配初始化、暂停和对象池生命周期。
- 冲撞预警、命中扫描和攻击阻挡物属于战斗规则，不承担路径搜索。

## 当前状态与边界

M1 开发侧逻辑已完成，EditMode 21/21、PlayMode 5/5、四档 UI 16/16。当前等待用户试玩；不宣称手感、30–45 分钟节奏、性能或 Windows 发布完成。Build Settings 仍只启用 SampleScene。

继续工作时以 [项目进度](../Progress.md) 为唯一任务入口，技术规则见 [架构](../Technical/LastLightArchitecture.md)，操作见 [试玩说明](../M1PlayGuide.md)。
