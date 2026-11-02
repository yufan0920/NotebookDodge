# 脚本职责与维护说明

更新：2026-11-02。本文描述当前目录；历史效果调试记录见[攻击特效记录](AttackVfxTiming.md)，本次清理依据及验证见[代码整理报告](CodeCleanupReport.md)。

## 目录怎么分

以下路径相对于 `Assets/_Project/Scripts`。

| 目录 | 内容 |
| --- | --- |
| `Runtime/Attacks/Shared` | 攻击配置、统一接口、清场 |
| `Runtime/Attacks/Fan` | Fan弹和可复用的基础碎片运动 |
| `Runtime/Attacks/Bounce` | 反弹弹 |
| `Runtime/Attacks/Trace` | Trace及两种Ink共用的运动、预警、伤害 |
| `Runtime/Attacks/Laser` | 激光行为与裁剪几何 |
| `Runtime/Attacks/Impact` | 粉笔下落与碎屑生成 |
| `Runtime/Attacks/Ground` | 地面燃烧、减速及区域几何 |
| `Runtime/VFX` | 动画、拖尾、形变、炮口、阴影及纯显示工具 |
| `Runtime/Level/Timeline` | 正式关卡编排、绝对时间调度、P3循环 |
| `Runtime/Level/Placement` | 炮口点位与随机选择、占用 |
| `Runtime/Player` | 玩家控制、活动区、生命 |
| `Runtime/UI` | 血量、计时、结算 |
| `Testing` | Prototype单技能运行测试驱动 |
| `Editor/Timeline` | 编排窗口、时间显示与预估 |
| `Editor/Placement` | 点位编辑器 |
| `Editor/EmitterTesting` | 单技能测试窗口与Inspector |
| `Editor/Shared` | 两种编辑入口共用的参数绘制 |

原 `AttackTesting` 已按职责拆到 `Runtime/Level` 与 `Editor`；原 `EmitterTesting` 拆到 `Testing` 与 `Editor/EmitterTesting`。`Testing` 是运行脚本目录，其组件仍用于Prototype，也会被时间轴识别并在克隆实例上停用；不能当成Editor目录或直接删除。只有 `Editor` 目录按Unity约定不进入运行包。本次没有引入asmdef、命名空间迁移或新的依赖包。

## 游戏逻辑怎么连起来

整关：`AttackTimelineWindow → AttackTimelineController → AbsoluteAttackSchedule → ITimelineEmitter.Fire → 子弹/激光 → PlayerHealth/GroundArea`。

单技能：`EmitterTestWindow → EmitterTestDriver → AttackSequence → 同一套ITimelineEmitter.Fire`。

整关遵守绝对时间：忙碌和错过不会顺延。单技能按准备、连发、冷却不断重复。两种调度不能合并，也不要同时控制同一个运行发射器。

攻击脚本决定“什么时候发生、判定多大、造成什么伤害”，VFX读取阶段或事件决定“如何显示”。例如碰壁反射仍在BounceBullet中，挤压和方片散射分别在对应VFX中。炮口闪光以真正出弹事件为准，不能以预警开始代替。

## 两个改名的组件

| 旧名称 | 当前文件/组件名称 | 原因 |
| --- | --- | --- |
| TraceTriangleVisual | TraceBulletOrientation | 已经不生成临时三角形，只负责手绘图片朝向 |
| TraceExplosionVfx | LandingExplosionVfx | 同时服务Trace、Ink和Impact的落地动画 |

原 `.meta` GUID和所有Inspector字段保留，且增加MovedFrom声明；已有预制体继续使用同一个脚本资源。Inspector中显示的新名称是预期变化，不需要重新挂组件。

## 每个脚本的作用

### Editor/EmitterTesting

| 脚本 | 功能 |
| --- | --- |
| [EmitterTestDriverEditor.cs](../Assets/_Project/Scripts/Editor/EmitterTesting/EmitterTestDriverEditor.cs) | 单技能测试驱动Inspector，使用共用参数界面。 |
| [EmitterTestWindow.cs](../Assets/_Project/Scripts/Editor/EmitterTesting/EmitterTestWindow.cs) | 单技能测试窗口：启停、只测一个、选择对象和清场。 |

### Editor/Placement

| 脚本 | 功能 |
| --- | --- |
| [EmitterPointLibraryEditor.cs](../Assets/_Project/Scripts/Editor/Placement/EmitterPointLibraryEditor.cs) | Scene点位/方向编辑、布局和随机位置预览、保存点位库。 |

### Editor/Shared

| 脚本 | 功能 |
| --- | --- |
| [AttackSettingsGUI.cs](../Assets/_Project/Scripts/Editor/Shared/AttackSettingsGUI.cs) | 编排面板和单技能面板共用的攻击参数界面。 |

### Editor/Timeline

| 脚本 | 功能 |
| --- | --- |
| [AttackTimelineTimeView.cs](../Assets/_Project/Scripts/Editor/Timeline/AttackTimelineTimeView.cs) | 编排面板Global/P1/P2/P3显示换算；不改轨道时间。 |
| [AttackTimelineWindow.cs](../Assets/_Project/Scripts/Editor/Timeline/AttackTimelineWindow.cs) | 整关编排窗口、轨道编辑、范围测试和日志；菜单名称保持不变。 |
| [AttackTimingPreview.cs](../Assets/_Project/Scripts/Editor/Timeline/AttackTimingPreview.cs) | 准备/伤害/退场时间预估和Excel文本；不驱动攻击。 |

### Runtime/Attacks/Bounce

| 脚本 | 功能 |
| --- | --- |
| [BounceBullet.cs](../Assets/_Project/Scripts/Runtime/Attacks/Bounce/BounceBullet.cs) | 活动区内反射、沿路径检查玩家命中、通知碰壁事件。 |
| [BounceEmitter.cs](../Assets/_Project/Scripts/Runtime/Attacks/Bounce/BounceEmitter.cs) | 反弹弹生成、扇形分布、朝向玩家及炮口闪光。 |

### Runtime/Attacks/Fan

| 脚本 | 功能 |
| --- | --- |
| [PatternBullet.cs](../Assets/_Project/Scripts/Runtime/Attacks/Fan/PatternBullet.cs) | Fan和Impact碎屑共用；展开、蓄力、预发射透明度、加速、飞行和伤害。 |
| [PatternEmitter.cs](../Assets/_Project/Scripts/Runtime/Attacks/Fan/PatternEmitter.cs) | Fan扇形生成与准备阶段占用；真正起飞时触发一次炮口闪光。 |

### Runtime/Attacks/Ground

| 脚本 | 功能 |
| --- | --- |
| [GroundArea.cs](../Assets/_Project/Scripts/Runtime/Attacks/Ground/GroundArea.cs) | 地面燃烧/减速的登记、查询、寿命和显示。 |
| [GroundAreaGeometry.cs](../Assets/_Project/Scripts/Runtime/Attacks/Ground/GroundAreaGeometry.cs) | 扣除同类地面区域的视觉重叠，避免透明色累积。 |

### Runtime/Attacks/Impact

| 脚本 | 功能 |
| --- | --- |
| [ImpactBullet.cs](../Assets/_Project/Scripts/Runtime/Attacks/Impact/ImpactBullet.cs) | 预警、粉笔下落、爆炸、坑印和碎屑生成。 |
| [ImpactEmitter.cs](../Assets/_Project/Scripts/Runtime/Attacks/Impact/ImpactEmitter.cs) | 按攻击序号读取配置的粉笔落点。 |

### Runtime/Attacks/Laser

| 脚本 | 功能 |
| --- | --- |
| [LaserBeam.cs](../Assets/_Project/Scripts/Runtime/Attacks/Laser/LaserBeam.cs) | 激光预警、蓄力、绘制、持续、淡出、判定和伤害；Beam Width控制外观，Damage Width独立控制核心伤害宽度。 |
| [LaserEmitter.cs](../Assets/_Project/Scripts/Runtime/Attacks/Laser/LaserEmitter.cs) | 生成激光并报告当前激光是否结束。 |
| [LaserGeometry.cs](../Assets/_Project/Scripts/Runtime/Attacks/Laser/LaserGeometry.cs) | 激光与玩家活动区的几何裁剪。 |

### Runtime/Attacks/Shared

| 脚本 | 功能 |
| --- | --- |
| [AttackCleanup.cs](../Assets/_Project/Scripts/Runtime/Attacks/Shared/AttackCleanup.cs) | 统一清理攻击对象；时间轴按场景清理，单技能窗口可清理所有已加载场景。 |
| [AttackSettings.cs](../Assets/_Project/Scripts/Runtime/Attacks/Shared/AttackSettings.cs) | 攻击配置与复制、Impact落点数据；不推进时间。 |
| [IEmitterOrientation.cs](../Assets/_Project/Scripts/Runtime/Attacks/Shared/IEmitterOrientation.cs) | 发射器图片的默认朝向接口；独立于发射时序。 |
| [ITimelineEmitter.cs](../Assets/_Project/Scripts/Runtime/Attacks/Shared/ITimelineEmitter.cs) | 发射接口；整关编排和单技能测试共用。 |

### Runtime/Attacks/Trace

| 脚本 | 功能 |
| --- | --- |
| [TraceBullet.cs](../Assets/_Project/Scripts/Runtime/Attacks/Trace/TraceBullet.cs) | 落点预警、飞行/追踪、碰撞或抵达后爆炸、伤害及可选地面区域。 |
| [TraceEmitter.cs](../Assets/_Project/Scripts/Runtime/Attacks/Trace/TraceEmitter.cs) | Trace/InkBurn/InkSlow共用发射器；管理准备期间瞄准和真正出弹。 |

### Runtime/Level/Placement

| 脚本 | 功能 |
| --- | --- |
| [EmitterPointLibrary.cs](../Assets/_Project/Scripts/Runtime/Level/Placement/EmitterPointLibrary.cs) | 点位ID、边、位置、方向及布局生成；点位代表炮口。 |
| [EmitterPointSelection.cs](../Assets/_Project/Scripts/Runtime/Level/Placement/EmitterPointSelection.cs) | 点位随机选择、最小间距、占用、上一轮排除和释放。 |

### Runtime/Level/Timeline

| 脚本 | 功能 |
| --- | --- |
| [AbsoluteAttackSchedule.cs](../Assets/_Project/Scripts/Runtime/Level/Timeline/AbsoluteAttackSchedule.cs) | 绝对秒数发射；忙碌或错过时刻会跳过，后续不顺延。 |
| [AttackTimelineController.cs](../Assets/_Project/Scripts/Runtime/Level/Timeline/AttackTimelineController.cs) | 整关编排入口；创建发射器、进退场、点位占用、区间测试和无尽循环。 |
| [EndlessAttackLoop.cs](../Assets/_Project/Scripts/Runtime/Level/Timeline/EndlessAttackLoop.cs) | P3区间循环的轮次、显示时间和完整连发计算。 |

### Runtime/Player

| 脚本 | 功能 |
| --- | --- |
| [PlayerHealth.cs](../Assets/_Project/Scripts/Runtime/Player/PlayerHealth.cs) | 生命、受伤、无敌、燃烧节奏、死亡及事件。 |
| [PlayerMovement.cs](../Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs) | 移动输入、地面减速与活动区限制。 |
| [PlayerMovementArea.cs](../Assets/_Project/Scripts/Runtime/Player/PlayerMovementArea.cs) | 活动四边形、移动约束及Scene边界；Bounce/Laser复用。 |

### Runtime/UI

| 脚本 | 功能 |
| --- | --- |
| [AudioPreferences.cs](../Assets/_Project/Scripts/Runtime/UI/AudioPreferences.cs) | 独立保存 BGM/SFX 音量百分比并广播变化；设置返回、切换场景或退出时落盘。 |
| [AudioVolumeChannel.cs](../Assets/_Project/Scripts/Runtime/UI/AudioVolumeChannel.cs) | 按 Music/Effects 类别将用户音量乘到 AudioSource 原始音量；启用后订阅设置，禁用时取消并恢复。 |
| [AudioSettingsMenu.cs](../Assets/_Project/Scripts/Runtime/UI/AudioSettingsMenu.cs) | 主菜单与暂停菜单共用音量设置基类，保留滑条及百分比的序列化字段和回调名称。 |
| [PauseMenuUI.cs](../Assets/_Project/Scripts/Runtime/UI/PauseMenuUI.cs) | 游玩中 Esc 暂停/继续；提供 RESUME、RESTART、SETTINGS，设置返回后仍保持暂停，死亡结算时不接受暂停输入。 |
| [UiTransition.cs](../Assets/_Project/Scripts/Runtime/UI/UiTransition.cs) | 不受暂停影响的界面淡入淡出；临时 CanvasGroup 控制面板，独立过场 Canvas 跨场景遮盖异步加载，不修改原始 Image 透明度或布局。 |
| [MainMenuUI.cs](../Assets/_Project/Scripts/Runtime/UI/MainMenuUI.cs) | 独立主菜单的 PLAY / SETTINGS / QUIT 与纸张右下角本机最高分；设置面板提供 BGM/SFX 滑条、百分比和返回，保存音量偏好，复用缎带反色交互。 |
| [SceneNavigation.cs](../Assets/_Project/Scripts/Runtime/UI/SceneNavigation.cs) | 共用场景切换与退出；保存偏好、恢复鼠标状态，按路径验证 Build Settings 中的目标场景；暂停重开时由 UiTransition 在新场景加载后恢复时间。 |
| [GameOverUI.cs](../Assets/_Project/Scripts/Runtime/UI/GameOverUI.cs) | 死亡结算、最高分和本机存档；TRY AGAIN 重载当前场景，QUIT 退出（编辑器中停止 Play）。结算缎带位于 GameOverPanel/Result Ribbons，MAIN MENU 返回独立主菜单场景。 |
| [HealthBarUI.cs](../Assets/_Project/Scripts/Runtime/UI/HealthBarUI.cs) | 订阅生命事件，更新图标设置并兼容旧血条对象。 |
| [HeartHealthGraphic.cs](../Assets/_Project/Scripts/Runtime/UI/HeartHealthGraphic.cs) | 实际绘制离散血量图标，不处理伤害。 |
| [RibbonButton.cs](../Assets/_Project/Scripts/Runtime/UI/RibbonButton.cs) | 基于 Button 状态切换缎带反色图片和文字颜色，悬停/选中放大、按下压缩；仅动画 Visual 子对象，点击范围固定。 |
| [NewRecordConfetti.cs](../Assets/_Project/Scripts/Runtime/UI/NewRecordConfetti.cs) | 新纪录结算的彩带序列动画，使用不受暂停影响的时间，播放后淡出；由 GameOverUI 触发。 |
| [SurvivalTimerUI.cs](../Assets/_Project/Scripts/Runtime/UI/SurvivalTimerUI.cs) | 显示时间与实际存活计分；支持时间轴区间测试。 |
| [BattleHudLayout.cs](../Assets/_Project/Scripts/Runtime/UI/BattleHudLayout.cs) | 顶部信息栏与铺满栏下区域的战斗视口；保留原 16:9 构图的大小并显示额外空间，避免两侧发射器被内部边缘裁切。独立背景相机让 bg1 等比铺满屏幕。 |

启动入口为 `Assets/_Project/Scenes/Main menu.unity`，PLAY 加载 `Level_Art`。主菜单复用完整三层缎带；Settings Panel 默认隐藏，与 Main Menu Panel 互斥显示，按钮和标签引用由 Canvas 的 MainMenuUI 保存。桌面打包默认采用可调整大小的窗口（fullscreenMode=3、resizableWindow=1），由系统标题栏管理最小化、最大化和关闭；不再读取旧全屏偏好。结算 TRY AGAIN 重载游玩场景，MAIN MENU 返回菜单，QUIT 退出（编辑器中停止 Play）。菜单标题在 `Main Menu Canvas/Main Menu Panel/Menu Title`，按钮在 `Menu Buttons` 下；菜单和结算使用独立场景对象，可分别排版。重命名场景时同步更新 SceneNavigation 的两个路径常量与 Build Settings。

`Level_Art` 的 bg1 使用 `DeskBackground` Layer，由 `Main Camera/Desk Background Camera` 单独绘制。主相机排除该层并只清除深度，叠加白纸、玩家和攻击；Canvas 最后绘制信息栏。调整桌面时保留这组相机与 Layer 引用。

结算缎带的 `Art/UI/ui1*.png` 和反色状态 `ui2-*.png` 保留共同的 1600×1000 画布，在 `Result Ribbons` 内按相同比例对齐。`Ribbon Back` 为装饰；按钮的 `Visual` 包含前景 `Ribbon Art` 与文字 `Label`。图片不接收射线，按钮根对象的透明 Image 仅覆盖该行的点击区域。整体移动或缩放请操作 `Result Ribbons`，避免单独缩放前景图片导致错位。Ribbon Button 的 Ribbon Feedback 可调整反色图片、选中文字颜色、放大和按压比例；MAIN MENU 已接入场景返回流程。

主菜单使用 `ui3.png` 作缎带背景，默认叠加 `ui3-1/2/3.png` 黄色按钮前景与深棕文字；选中时切换对应的 `ui4-1/2/3.png` 陶土红底、深棕边前景与米白文字。Main Menu Panel 的 Image 透明度保持 0，让开始界面保持明亮；不要复制结算遮罩的透明度。结算和设置页继续使用各自原有图片。BEST 当前显示于纸张右下角，使用深棕色。

`TitleText` 单独使用 `PatrickHand-GameOver-Outline.mat`：白色字芯，黑色零偏移 Underlay，Outline Width 和 Face Dilate 保持 0。外边粗细调 Underlay/Dilate，避免 Outline 向字芯内侧扩展。其余 Patrick Hand UI 使用 `PatrickHand-UI-Plain.mat`，不要在共用字体图集材质上调整标题效果。

### Runtime/VFX

| 脚本 | 功能 |
| --- | --- |
| [BounceSquashVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/BounceSquashVfx.cs) | 复制当前动画帧，只对显示子物体做碰壁挤压、拉伸和回弹。 |
| [BounceWallVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/BounceWallVfx.cs) | 实际碰壁时稀疏散射方片。 |
| [ChalkFragmentVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/ChalkFragmentVfx.cs) | 粉笔碎屑切换静态外形、错开初始角度并旋转显示子物体。 |
| [InkSlowFlightVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/InkSlowFlightVfx.cs) | 单张墨团的轻微伸缩、漂晃及按飞行进度积分旋转。 |
| [LandingExplosionVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/LandingExplosionVfx.cs) | Trace/Ink/Impact共用落地动画；支持规则图集或切片帧、换色、混帧及播放缓动。 |
| [LaserLineBlastVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/LaserLineBlastVfx.cs) | 沿激光长度平铺序列图，持续阶段循环清晰帧，跟随LaserBeam淡出。 |
| [MuzzleSparkVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/MuzzleSparkVfx.cs) | 实际出弹时播放一次spark-burst；保留各发射器独立参数。 |
| [SoftEllipseShadow.cs](../Assets/_Project/Scripts/Runtime/VFX/SoftEllipseShadow.cs) | 生成并跟随碎屑的渐变椭圆阴影；不参与碰撞。 |
| [SpriteAppearance.cs](../Assets/_Project/Scripts/Runtime/VFX/SpriteAppearance.cs) | 普通静态工具，不是组件；统一三种显示副本的九项Sprite属性同步。 |
| [SpriteSheetUv.cs](../Assets/_Project/Scripts/Runtime/VFX/SpriteSheetUv.cs) | 普通静态工具，不是组件；统一图集帧矩形、半像素内缩及混帧UV偏移。 |
| [TraceBulletOrientation.cs](../Assets/_Project/Scripts/Runtime/VFX/TraceBulletOrientation.cs) | 让Trace手绘子弹朝向飞行方向；不再生成临时三角形。 |
| [TraceTrailVfx.cs](../Assets/_Project/Scripts/Runtime/VFX/TraceTrailVfx.cs) | 间断生成三角石墨碎屑，限制数量、间距、寿命，爆炸时停止生成。 |

### Testing

| 脚本 | 功能 |
| --- | --- |
| [AttackSequence.cs](../Assets/_Project/Scripts/Testing/AttackSequence.cs) | 单技能的初始等待、连发与冷却循环；与绝对秒数编排用途不同。 |
| [EmitterTestDriver.cs](../Assets/_Project/Scripts/Testing/EmitterTestDriver.cs) | Prototype单发射器测试入口；寻找发射器和玩家，推进AttackSequence。 |

## 想调整什么，先找哪里

| 调整内容 | 入口 |
| --- | --- |
| 发射器位置、入场退场、攻击时刻 | Attack Timeline窗口；炮口点位在EmitterPointLibrary |
| Fan展开、蓄力和准备透明度 | 子弹预制体的Pattern Bullet → Prepare |
| 各类子弹速度、曲线、伤害、碰撞 | 对应Bullet组件/Collider；不要通过VFX大小改变命中判定 |
| 落地动画大小、颜色、混帧和播放缓动 | 子弹预制体的Landing Explosion Vfx |
| 落地动画总时长 | Trace Bullet / Impact Bullet 的Explosion Visual Duration |
| 墨滴循环速度 | Bounce_Ink Controller中的动画状态Speed |
| 碰壁挤压与方片 | Bounce Squash Vfx / Bounce Wall Vfx |
| 炮口短闪 | 发射器根物体上的Muzzle Spark Vfx |
| 正式关卡中的文具外观 | Level_Art的Emitter模板下Art；Muzzle本地位置保持0 |

场景/预制体/美术目录此次保持原位。正在使用的 `Bullet.prefab` 挂的是PatternBullet，不是已经删除的旧Bullet.cs。Ink1/Ink2及CrispPreview是效果试验版本，不是重复脚本；根据Drop Prefab引用选择，不能批量删除。

## 维护约定

1. 移动或改名脚本必须同时移动 `.meta`，不重建GUID；不要顺手重置场景或预制体参数。
2. 共用计算优先用短小静态工具；不同攻击的阶段、计时、资源释放保持各自负责，不增加空的通用攻击框架。
3. `FormerlySerializedAs`、旧Track数据迁移、旧血条兼容和无素材时的爆炸圈回退都有用途，保留。
4. 新攻击接入后检查AttackCleanup、AttackSettingsGUI、AttackTimingPreview；添加接口不代表编辑器参数会自动生成。
5. 改准备或持续时间时，同时核对编排预估；改美术尺寸不改伤害半径。
6. 临时材质、网格、粒子和显示子物体由创建它们的组件释放；禁用时恢复被隐藏的原Renderer。
7. 代码统一UTF-8；新增运行脚本放Runtime，编辑器脚本放Editor，不把UnityEditor依赖带到运行代码。

## 检查入口

运行 `python Tools/CodeMaintenance/validate_code.py --tests` 可检查脚本GUID、文件/类型名称、资源脚本引用和重复素材，并分别编译运行代码/编辑器代码，执行UV、播放与调度回归检查。说明见[维护工具](../Tools/CodeMaintenance/README.md)。输出在Temp，不写Unity的Library程序集。

离线检查不代替Unity导入和Play观感。重新打开项目后检查Console，再在Prototype/Level_Art确认Fan准备透明度、Trace落地、Bounce循环与碰壁、Laser持续显示和时间轴播放。

主菜单 Menu Title 单独使用 `PatrickHand-MenuTitle.mat`，字芯 #B77F68、零偏移外边 #655047。保持白色顶点颜色，避免重复染色；结算标题继续使用自己的黑白材质。

设置页位于 Main Menu Canvas/Settings Panel：BGM、SFX 两行分别由 Label、Volume Slider、Value 组成。滑条包含 Track、Fill Area/Fill、Handle Area/Handle，可分别换图片。BACK 返回并保存；显示模式切换项已移除。现有受伤 AudioSource 由 PlayerHealth 自动接入 Effects；后续 BGM 音源挂 AudioVolumeChannel，Channel 选 Music，即读取同一 BGM 音量并实时更新。当前未添加或自动播放 BGM 音乐素材。

界面过渡：主菜单与设置页各淡出/淡入 0.14 秒（MainMenuUI/Panel Fade Seconds），结算淡入 0.22 秒（GameOverUI/Result Fade Seconds）；场景切换使用独立 Screen Transition 深棕色遮罩，淡出 0.13 秒、加载完成后淡入 0.18 秒。切换期间关闭交互，结束后恢复焦点；时间使用 unscaledDeltaTime，暂停时仍能完成。面板的原始 Image Alpha（包括主菜单的 0）和 RectTransform 不被改写。

BGM/SFX 的手柄图片使用 `Art/UI/滑块.png`，位于 Volume Slider/Handle Area/Handle/Handle Art。原图保留 1600×1000 透明画布，Handle Art 按内容边界居中并显示为约 25×42 UI 单位；Slider 仍驱动原 Handle Rect，Target Graphic 指向 Handle Art 的 Image。调整美术尺寸可等比缩放 Handle Art，不要将整张大画布直接套到小 Handle Rect 上。

主菜单与暂停菜单的设置页 BACK 均使用 `ui5-3.png` 黄色默认图配深棕字、`ui5-2.png` 陶土红悬停/选中图配米白字，保留原始共用画布并在 Visual/Ribbon Art 中居中；保持现有按钮位置、比例和点击范围。

设置页 BACK 返回主菜单后清空 EventSystem 选中对象，避免 SETTINGS 保持选中配色；主菜单仅在鼠标悬停或用户开始键盘/手柄导航时高亮按钮。进入设置页仍为音量滑条提供键盘焦点。

`Level_Art` 的 Canvas 挂有 PauseMenuUI，管理默认隐藏的 Pause Panel 和 Pause Settings Panel。Pause Panel 下的 Pause Title 显示 PAUSED，Pause Buttons 复用三层缎带，默认陶土红配米白字，悬停/选中使用浅色前景配深棕字。整体排版在 Pause Buttons 根节点调整；主菜单的布局、颜色及透明度保持原设置。

游玩时按 Esc 将 Time.timeScale 设为 0，冻结物理子弹、玩家移动、攻击编排及存活计时，并暂停 AudioListener；Trace 预警协程在暂停期间跳过跟踪计算。暂停菜单中按 Esc 或 RESUME 等待淡出后恢复此前时间、音频和鼠标状态。SETTINGS 使用同一套 BGM/SFX 偏好；BACK 或 Esc 返回暂停菜单，只有继续游戏才解除暂停。RESTART 保持旧场景暂停直至加载完成，再以正常速度开始新一局。菜单过渡及按钮反馈使用非缩放时间，暂停期间仍可操作；不添加退出按钮。
