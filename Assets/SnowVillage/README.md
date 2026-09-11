# 雪地场景

项目文档入口：[文档索引](../../Docs/README.md) · [模型创建与导入](../../Docs/Technical/ModelPipeline.md) · [美术规格](../../Docs/Art/ArtSpecification.md) · [当前进度](../../Docs/Progress.md)。

打开 `Assets/SnowVillage/Scenes/SnowVillage.unity`，点击 Unity Play 即可运行。

## 操作

- 默认沿预设路线自动行走。
- WASD / 方向键：接管人物，按固定镜头方向移动。
- Tab：切换自动演示和手动模式。
- R：恢复起点与自动演示，清除动态脚印。
- 请先点击 Game 窗口，使键盘输入获得焦点。

## 已制作内容

冬装人物使用 16 根骨骼与蒙皮，包含 3 秒待机、1.2 秒雪地行走动画。场景包括积雪木屋、小木棚、带顶井、围栏、电线杆、电线、枯树、起伏雪地、旧脚印、落脚脚印、雪粉与飘雪。动态脚印使用 256 个对象的循环池。

Blender 源文件与生成脚本在项目根目录 `ArtSource/SnowVillage/`；FBX 位于本目录 Models；可复用资源位于 Prefabs。未安装新 MCP 或新增 Unity 包。

`Tools > Snow Village` 提供人物生成、场景重建与截图菜单。重建场景会重新生成本功能场景；手动调整场景前建议另存副本。代码中保留的验收工具仅供以后需要时使用，不随场景自动启动。

## 画质与交付

为固定远距离正交镜头配置了独立 SnowPipeline，阴影距离 110、主光阴影分辨率 4096，并将当前 PC 质量等级指向它；因此 `ProjectSettings/QualitySettings.asset` 有相应调整。运行时允许后台演示，退出场景时恢复原后台运行状态。

最终实际 Unity 截图在项目根目录 `Captures/SnowVillage/FinalScene.png` 与 `FinalCharacter.png`。

已进行 Unity 编译、模型导入、运行画面及动画检查。初次长时自动测试发现编辑器失去焦点会暂停运行，已增加后台运行处理。按用户要求停止后续测试和视觉调整；最终版本未完成全部自动验收，未生成独立 Windows 安装包，不宣称完整测试通过。
