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
- LastLight M2：`Assets/LastLight/Scenes/M2Chapter.unity`；菜单 `Tools > LastLight > M2 > Open Prototype`；Windows 样板为 `Builds/LastLight/M2/LastLight.exe`。
- M2 生成内容：`Assets/LastLight/M2Generated/`；生成器 `Assets/LastLight/Editor/M2ChapterBuilder.cs`；家园、旧工坊、温室使用独立 Addressables 场景标识。
- LastLight 架构样例：`Assets/LastLight/ArchitectureSample/Scenes/Bootstrap.unity`。
- SnowVillage 视觉样板：`Assets/SnowVillage/Scenes/SnowVillage.unity`，禁止由 LastLight 工具覆盖。
- LastLight 模型源：`ArtSource/LastLight/`；运行资产：`Assets/LastLight/`。

## 架构速查

- `GlobalManager` 持有 Application 系统与当前 Session；系统按 Application、Session、Scene 三种生命周期管理。
- `SceneFlowSystem` 通过 Addressables Additive 加载家园、工坊和温室，提交后卸载旧内容场景。
- `ResourceSystem` 是 Addressables、实例池和场景租约的唯一入口。
- `UIManager` 使用 BG、Window、Pop、Over 四层；`PauseSystem` 集中管理暂停令牌。
- M1/M2 权威状态分别由经济、建造、生存、叙事、威胁与 M2 世界系统持有，`M1GameSession` 负责场景适配和命令提交。
- M2 存档使用版本化稳定标识、SHA-256 校验、临时文件提交和每槽单份有效备份；恢复前完整校验，失败时保持当前会话。
- M2 统一世界时钟驱动春季、农业、燃料和睡眠结算；阅读、菜单、暂停与建造规划持有暂停令牌。
- `LastLight.Core` 保持纯 C# 规则；Runtime、Framework、Editor 和测试程序集分离。

## 导航约束

- 所有角色寻路必须使用 Unity 官方 **AI Navigation + NavMeshAgent**，不得自创网格 BFS、A* 或局部绕障系统。
- 家园和工坊各有独立 `NavMeshSurface`，只收集本内容场景的静态碰撞几何。
- 可建造墙、门框、设备和路障通过 `NavMeshObstacle` carving 更新通行；建筑归零时关闭障碍，维修后恢复。
- 梁使用 `NavMesh.CalculatePath` 选择可达维修站位；`M1Navigator` 只适配初始化、暂停和对象池生命周期。
- 冲撞预警、命中扫描和攻击阻挡物属于战斗规则，不承担路径搜索。

## 当前状态与边界

M2-01～05 及 M2-06 开发侧验证已完成：EditMode 42/42、PlayMode 10/10、双语最大字号布局 64/64，Windows x64 构建与跨进程存读档探针通过。M2-06 等待用户试玩确认首章循环、农业价值和第二季节体验；不宣称手感或一小时节奏通过。M1 的用户手感验收仍按未登记保留。

M2 内容停留在春季；芽、渡、第二章完整剧情、夏秋冬完整循环和结局属于 M3。继续工作时以 [项目进度](../Progress.md) 为唯一任务入口，技术规则见 [架构](../Technical/LastLightArchitecture.md)，操作见 [M2 试玩说明](../M2PlayGuide.md)。
