# LastLight 技术架构

## 入口与目录

- M1 启动场景：`Assets/LastLight/Scenes/M1Prototype.unity`。
- 内容场景：`Assets/LastLight/M1Generated/Scenes/Home.unity`、`Workshop.unity`。
- 运行代码：`Assets/LastLight/Scripts/`；纯规则：`Assets/LastLight/Scripts/Core/`。
- 基础框架：`Assets/LastLight/Framework/`；编辑器工具：`Assets/LastLight/Editor/`。
- 生成内容：`Assets/LastLight/M1Generated/`；测试：`Assets/LastLight/Tests/`。
- 独立架构样例：`Assets/LastLight/ArchitectureSample/`。

LastLight 工具只覆盖其明确拥有的目录和专用 Addressables 分组，不修改 SnowVillage、默认 Build Settings 或其他场景。

## 生命周期与状态所有权

`GlobalManager` 与 `UIManager` 位于持久化根节点。系统分为三层：

| 层级 | 所有者 | 用途 |
| --- | --- | --- |
| Application | `GlobalManager.Systems` | 输入、暂停、资源、场景流程、UI |
| Session | `GlobalManager.Session` | 经济、建造、生存、叙事、威胁 |
| Scene | 当前 `ISceneState` | 场景引用、表现对象与临时资源 |

系统通过显式依赖初始化，按拓扑顺序启动并逆序关闭。长生命周期系统不得依赖短生命周期系统。关闭时继续清理其余系统并汇总错误。

`M1GameSession` 是场景适配层：接收角色、UI、节点和敌人的意图，调用对应会话系统验证并提交。表现层不得直接修改库存、建筑、剧情或袭击权威状态。资源消费、奖励、建造、维修和事件选择都遵循“全部条件通过后一次提交”，失败不产生部分扣费或部分状态。

## 场景流程

`SceneFlowSystem` 使用 Addressables Additive 加载内容场景：

1. 暂停世界并关闭玩法输入。
2. 加载目标场景，绑定并验证 `M1GameSession`。
3. 将目标设为活动场景并提交状态。
4. 退出并卸载旧场景。
5. 恢复输入和原暂停状态。

提交前失败会清理目标并保留旧场景；提交后旧场景清理失败则保留新状态并报告错误。一次只处理一个跳转，同场景请求不重复加载。

## 资源与对象池

`ResourceSystem` 是 Addressables、实例池和内容场景租约的唯一入口。玩法代码和 UI 不得直接持有原生 Addressables 句柄或自行释放池对象。

- 每个调用方持有独立 Lease；最后一个 Lease 释放时才释放底层资源。
- `ResourceScope` 统一拥有场景内资源，场景退出后拒绝新登记。
- 租出的实例默认未激活；调用方绑定完整后再激活。
- 归还先停用并重置，再缓存或销毁；重复归还必须报错。
- 取消表示取消调用意图；迟到的加载结果仍需安全释放。

## UI、输入与暂停

UI 分为 `BG < Window < Pop < Over` 四层，排序值为 0、100、200、300。Canvas 参考分辨率为 1920×1080，使用 Scale With Screen Size。

面板根节点必须全屏拉伸；内部 Content 由 Prefab 自己布局。`PanelCatalog` 定义地址、层级、模态、暂停、可关闭和生命周期。最上层模态面板独占焦点，Esc 关闭最上层可关闭面板。

`PauseSystem` 集中持有暂停令牌。背包、日志、剧情、暂停菜单和建造模式停止世界时间、角色、敌人、导航代理与袭击倒计时，但 UI 仍接收关闭操作。关闭 UI 的点击不得穿透为攻击。

输入在场景完成绑定后才启用。M1 复用现有 Input System 的 Move、Sprint、Attack、Interact；常驻输入系统处理暂停状态下的菜单关闭。

## AI 导航

项目统一使用 Unity 官方 `com.unity.ai.navigation` 2.0.14：

- 梁和枝角兽移动使用 `NavMeshAgent`。
- 家园与工坊各自配置 `NavMeshSurface`，仅收集该内容场景下的静态碰撞几何；场景激活时注册导航数据，退出时移除。
- 可建建筑使用 `NavMeshObstacle` carving。墙、门框、设施和路障完好时改变可行走区域；生命归零时关闭碰撞与导航障碍，维修后恢复。
- 梁通过 `NavMesh.CalculatePath` 比较维修目标周围的候选站位，只前往完整可达且能接触目标的位置。无路径时停止，并显示通路受阻。
- `M1Navigator` 仅封装代理初始化、目标提交、暂停和对象池复用，不实现寻路算法。
- 枝角兽的目标选择、冲撞预警、命中检测和攻击阻挡建筑属于战斗状态机；路径搜索与绕障仍由 `NavMeshAgent` 完成。
- 建造网格的占地、房屋封闭和保留通道检查属于规则校验，不驱动角色移动。

禁止新增自研 BFS、A*、转向避障或替代 NavMeshAgent 的角色寻路。若后续导航需求超出 AI Navigation 能力，先评估成熟插件并更新本架构，不在玩法代码内另造寻路框架。

## M1 玩法约束

- 背包、仓储、拾取、制作和奖励由经济系统提交；关键件独立保存且不可重复领取。
- 建筑使用 2 米网格；受损建筑不得移动或拆除，维修完成时一次扣料。
- 固定应急设施不参与普通拆除和袭击损坏；普通资源归零后仍有徒手恢复链。
- 梁从公共仓储取材维修；缺料、停机和堵路必须显示原因，玩家始终可手工维修。
- 教学袭击只在玩家阅读说明、具备对策并确认后开始；失败损失单次结算且有上限。
- 援助、交换、拒绝都能继续主线；交换的床位、口粮、奖励与承诺一次提交。
- M1 只保存当前运行内存状态；磁盘存档在 M2 实现。

## 生成与验证

`Tools > LastLight > M1 > Generate Prototype` 只重建 M1Generated、M1 启动场景及 `LastLight M1 Local` Addressables 分组。生成前必须退出 Play Mode、保存当前场景；修改生成结果时同步生成器。

当前自动验证入口：

- `LastLight.Tests.M1RulesTests`：库存事务、建造规则、奖励、选择、损失和恢复。
- `LastLight.Tests.M1PlayModeTests`：输入、暂停、场景往返、建造、动态导航、梁维修、袭击和三种结局路径。
- M1 当前结果：EditMode 21/21、PlayMode 5/5、四档 UI 16/16。

这些结果证明已覆盖的逻辑，不代表手感、内容时长、性能或 Windows 发布完成。
