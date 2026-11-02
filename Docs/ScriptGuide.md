# 脚本职责与维护说明

更新：2026-10-27。本文描述当前目录；历史效果调试记录见[攻击特效记录](AttackVfxTiming.md)，本次清理依据及验证见[代码整理报告](CodeCleanupReport.md)。

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
| [GameOverUI.cs](../Assets/_Project/Scripts/Runtime/UI/GameOverUI.cs) | 死亡结算、最高分和本机存档。 |
| [HealthBarUI.cs](../Assets/_Project/Scripts/Runtime/UI/HealthBarUI.cs) | 订阅生命事件，更新图标设置并兼容旧血条对象。 |
| [HeartHealthGraphic.cs](../Assets/_Project/Scripts/Runtime/UI/HeartHealthGraphic.cs) | 实际绘制离散血量图标，不处理伤害。 |
| [NewRecordConfetti.cs](../Assets/_Project/Scripts/Runtime/UI/NewRecordConfetti.cs) | 新纪录结算的彩带序列动画，使用不受暂停影响的时间，播放后淡出；由 GameOverUI 触发。 |
| [SurvivalTimerUI.cs](../Assets/_Project/Scripts/Runtime/UI/SurvivalTimerUI.cs) | 显示时间与实际存活计分；支持时间轴区间测试。 |
| [BattleHudLayout.cs](../Assets/_Project/Scripts/Runtime/UI/BattleHudLayout.cs) | 顶部信息栏与固定 16:9 战斗视口；独立背景相机让 bg1 等比铺满屏幕。 |

`Level_Art` 的 bg1 使用 `DeskBackground` Layer，由 `Main Camera/Desk Background Camera` 单独绘制。主相机排除该层并只清除深度，叠加白纸、玩家和攻击；Canvas 最后绘制信息栏。调整桌面时保留这组相机与 Layer 引用。

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
