# 模型创建与 Unity 导入流程

SnowVillage 与 LastLight 使用独立源文件、生成器和输出目录。生成脚本会覆盖其负责的 Blender 源文件与 FBX；手工修改必须同步回生成脚本或保存到明确的非生成路径。

## 工具与目录

| 项目 | 配置 |
| --- | --- |
| Blender | D:/BaiduSyncdisk/blender/blender.exe，5.2.0 LTS |
| Unity | 6000.3.21f1，URP 17.3.0 |
| Unity 自动化 | Coplay MCP v10.2.0 |
| Blender 自动化 | 后台 Python，无 Blender MCP |
| SnowVillage 源 / 输出 | ArtSource/SnowVillage / Assets/SnowVillage |
| LastLight 源 / 输出 | ArtSource/LastLight / Assets/LastLight/Models |

标准流程：生成脚本 → 可编辑 blend 与显式 FBX → Unity ModelImporter → 材质与 Animator → Prefab → 场景。blend 保持在 Assets 外，Unity 不直接导入源文件。

## SnowVillage

build_character.py 生成 16 骨骼 Generic 人物以及 Idle、Walk；build_environment.py 生成 Woodshed、Cabin、CoveredWell、FenceSection、UtilityPole 和 BareTree。

人物契约：

- Generic Rig，Idle 3 秒、Walk 1.2 秒，均循环。
- Animator 关闭 Root Motion，使用 Speed 一维 BlendTree，Idle=0、Walk=1.05。
- 行走片段动画事件归一化时间为 0.02 / 0.52，调用 Footstep，参数 0 / 1 对应左右脚。
- SnowTraveler 使用 CharacterController 和现有 Player/Move；运行时接口只能在 Play Mode 且初始化后调用。
- 场景变换施加在导入模型外层包装节点，保留 FBX 根节点的坐标、旋转和单位换算。

环境契约：

- 固定随机种子 42；地形、脚印网格、电线和粒子由 Unity 构建工具生成。
- FBX 使用 -Z Forward、Y Up；环境不导出动画。
- FBX 材质名是 Unity 材质映射键；最终颜色由 Unity 生成器配置。
- 动态脚印射线贴地并沿法线旋转，上限 256；每次有效落脚发射 5 粒雪粉。

生成命令：

```powershell
Set-Location -LiteralPath 'D:/BaiduSyncdisk/unity/Chongfandiq'
$blenderExe = 'D:/BaiduSyncdisk/blender/blender.exe'
& $blenderExe --background --factory-startup --python-exit-code 1 --python 'ArtSource/SnowVillage/build_character.py'
if ($LASTEXITCODE -ne 0) { throw '人物生成失败。' }
& $blenderExe --background --factory-startup --python-exit-code 1 --python 'ArtSource/SnowVillage/build_environment.py'
if ($LASTEXITCODE -ne 0) { throw '环境生成失败。' }
```

Unity 组装入口是 Assets/SnowVillage/Editor/SnowVillageBuilder.cs。Build Character 配置人物资源；Build Scene 会新建并覆盖 SnowVillage.unity。该场景是受保护视觉基线，除非用户明确要求，不运行生成器。

## LastLight

ArtSource/LastLight/build_assets.py 生成 LastLightAssets.blend，并覆盖 Assets/LastLight/Models 中它负责的 23 个同名 FBX。

```powershell
Set-Location -LiteralPath 'D:/BaiduSyncdisk/unity/Chongfandiq'
$lastLightBlender = 'D:/BaiduSyncdisk/blender/blender.exe'
& $lastLightBlender --background --factory-startup --python-exit-code 1 --python 'ArtSource/LastLight/build_assets.py'
if ($LASTEXITCODE -ne 0) { throw 'LastLight 模型生成失败。' }
```

LastLight 场景和 Prefab 由 Assets/LastLight/Editor 中的工具组装，只能写入 Assets/LastLight。无需改变模型时不要重复运行 Blender；修改生成模型时同步脚本。

## 操作约束

- 生成前退出 Play Mode，并确认交互式 Blender 没有未保存内容。
- 不直接修改导入 FBX 的根变换来修正朝向或比例。
- 保留 meta GUID；不要手工修改 Library 或 Temp。
- 场景或 Prefab 修改优先使用 Unity 编辑器 API；生成后等待导入和编译结束，再检查实际引用。
- 模型存在不等于材质、碰撞、Prefab、场景和玩法已接线；完成状态以 [项目进度](../Progress.md) 为准。
- Unity 操作前确认 MCP 当前实例和 Play Mode；代码、文档任务不需要重建模型。
