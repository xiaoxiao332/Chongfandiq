# LastLight 架构最终验证

日期：2026-09-10。

- Unity：6000.3.21f1，StandaloneWindows64，URP 17.3.0，Addressables 2.9.1。
- 编译：Unity 导入与编译完成，无项目脚本错误。
- EditMode：`LastLight.EditModeTests` 9/9 通过，0 失败、0 跳过，1.181 秒。
- Packed PlayMode：`LastLight.PlayModeTests.CompleteArchitectureLifecycle` 1/1 通过，7.887 秒；内部执行十轮菜单/A/B/A/菜单，并覆盖 UI 焦点、暂停、池复用、租约、失败回退及最终清理。
- 分辨率矩阵：1920×1080、1920×1200、2560×1080、1280×720 全部通过；五个面板均铺满所属层，Content 保持在视口内。
- Domain Reload：历史连续两次关闭 Domain Reload 的运行记录均为 `RanToCompletion`；新增快速重入等待修复后另一次完整运行通过。用户随后要求停止测试，已取消额外重复轮次且不计入结论。
- Windows：x86_64 Development 构建成功，0 错误，大小 170,726,859 字节；`WindowsSmokeFinal.txt` 记录独立程序十轮冒烟通过。
- 隔离：SnowVillage 文件变化 0，既有 LastLight `.meta` 变化 0，默认 Build Settings 哈希不变。URP 构建产生的 SnowPipeline 预筛选字段已恢复，构建工具现会自动保护管线资产。
- Console：收尾编译后没有项目错误；观察到的 WebSocket 未初始化信息来自 Coplay MCP 传输层。

范围限制：这是架构 Development 样例验证，不是 M1 可玩原型、性能结论或正式发布构建。
