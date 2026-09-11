# 项目协作指南

默认使用中文沟通和编写项目文档。

## 开始工作

先读 [文档索引](Docs/README.md)、[当前进度](Docs/Progress.md) 和 [项目上下文](Docs/AI/UnityProjectContext.md)。模型/动画工作另读 [技术流程](Docs/Technical/ModelPipeline.md)，视觉工作另读 [美术规格](Docs/Art/ArtSpecification.md)。

- 已建好 1 个制作场景：`Assets/SnowVillage/Scenes/SnowVillage.unity`。用户已确认效果并要求收尾；它是受保护的视觉基线，不因《留灯地球》开发重做或覆盖。
- 《留灯地球》使用独立 `LastLight` 目录，已有基础架构、独立运行样例与 M1 原型接线；原型整体验收状态以 [项目进度](Docs/Progress.md) 为准。架构入口见 [LastLight 架构](Docs/Technical/LastLightArchitecture.md)，操作见 [M1 试玩说明](Docs/M1PlayGuide.md)。
- Unity 6000.3.21f1、URP 17.3.0，使用现有 Input System。
- Blender：`D:/BaiduSyncdisk/blender/blender.exe`，本次使用 5.2.0 LTS。
- Unity 已安装 Coplay MCP v10.2.0，优先复用；操作前确认当前项目及 Play Mode 状态，不将历史连接记录当作当前连接状态。
- Blender 使用后台 Python → FBX，当前未为本项目配置 Blender MCP。

## 目录约定

| 目录 | 用途 |
| --- | --- |
| `ArtSource/SnowVillage/` | Blender 源文件、模型及动画生成脚本 |
| `Assets/SnowVillage/` | 模型、材质、Prefab、动画、场景与代码 |
| `Assets/SnowVillage/Editor/` | 构建、截图和可选验证工具 |
| `Captures/SnowVillage/` | 实际截图和历史检查输出 |
| `ArtSource/LastLight/` | 《留灯地球》Blender 源文件和模型生成脚本 |
| `Assets/LastLight/` | 《留灯地球》模型、场景、代码及其他运行资产 |
| `Assets/LastLight/Editor/` | 《留灯地球》场景构建和编辑器工具；不得写入 SnowVillage |
| `Assets/LastLight/Tests/` | 《留灯地球》EditMode、PlayMode 测试及测试程序集 |
| `Captures/LastLight/` | 《留灯地球》实际截图和验证输出；需要时创建 |
| `Docs/` | 技术、美术、进度与上下文；沿用大小写，不另建并列 `docs/` |

## 修改与维护

- 保留用户资产、场景修改和 `.meta` GUID；优先通过 Unity 编辑器 API 创建和修改场景/Prefab，不直接手改 YAML。
- 不手工修改 `Library/`、`Temp/` 等缓存。最近检查未初始化 Git，使用 Git 前重新确认。没有 Git 时通过独立目录、保留 `.meta`、限制生成器覆盖范围和进度执行记录降低误覆盖风险。
- 运行生成脚本或 Build Scene 前阅读技术文档的覆盖范围；它们会覆盖生成内容，不会自动合并手工美术调整。`ArtSource/LastLight/build_assets.py` 只能覆盖 `Assets/LastLight/Models/` 中对应模型。
- 修改生成结果时同步相应生成脚本，避免重建丢失变更；SnowVillage 与 LastLight 的生成器、输出目录和场景保持隔离。
- SnowVillage 运行时代码使用 `SnowVillage`；LastLight 正式架构与既有 M1 代码已统一为 `LastLight`。编辑器代码放对应 Editor 目录。
- 修改脚本后等待 Unity 编译和域重载完成并检查 Console；场景或 Prefab 修改后检查实际引用、保存状态和与改动相称的运行行为。编译通过不能代替功能验收。
- 不以空实现、吞异常、隐藏日志、关闭测试或虚假成功返回让任务显得完成；未接线、未运行或未验证的内容按实际状态记录。
- 后续功能修改必须完成与风险相称的逻辑验证；默认不替用户判断操作手感和流程节奏。开发侧验证通过后直接交付试玩，用户反馈后继续修正。纯文档整理不进入 Play Mode、不重建资产、不启动测试。
- `SnowTraveler.ResetTraveler()` 等运行时接口只能在 Play Mode 且初始化完成后调用。
- 当前完整自动验收未完成，不能描述为全部测试通过、性能达标或已完成 Windows 发布。Build Settings 仍仅启用 SampleScene，未来打包时再明确配置。
- `Docs/Design/GameDesign.md` 是玩法与范围的唯一设计来源，`Docs/Progress.md` 按其 `GD-xx` 章节拆分阶段任务。设计变化先更新主策划，再调整对应进度任务；进度只记录状态、完成条件和验证证据，不复制整份策划。技术流程变化更新架构文档，视觉基线变化更新美术规格；上下文保持简短并清理过期内容。
- 用户要求“按进度继续”时，以 `Docs/Progress.md` 的当前任务为唯一执行入口；开始和收尾均同步状态、验证证据、遗留事项与下一任务。

## 实施与验收分工

- 收到功能开发要求后，优先直接实现完整可运行结果，不停留在方案、空实现或仅接线状态。除非缺少会显著改变架构或产品行为的必要信息，否则按现有文档和项目约定自行作出保守决定。
- 开发侧负责逻辑稳定性：完成编译检查、Console 检查、规则测试和必要的 PlayMode 回归，覆盖状态切换、资源事务、失败恢复、场景往返、暂停、重复操作、空状态和边界条件。
- 自动测试用于验证逻辑，不替代玩家体验。移动、战斗、镜头、建造等主观手感，以及完整流程节奏，由用户试玩验收。
- 未收到明确要求时，不为调整手感反复进行长时间自动试玩或流程计时；完成逻辑验证后交付可玩入口、操作说明和已知限制。
- 用户反馈手感问题后，将反馈视为明确需求实施，并对受影响逻辑重新回归；不擅自扩大到未反馈的玩法重做。
- 发现逻辑缺陷时直接修复并验证。只有缺少关键产品选择、外部权限或必要资产时才暂停询问，并明确说明阻塞点。
- `Docs/Progress.md` 分别记录“开发侧逻辑验证”和“用户手感验收”；逻辑测试通过不得写成手感、节奏或完整体验已通过。
