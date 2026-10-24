# 脚本职责与维护说明

本次整理：2026-10-24。以下路径均相对于 `Assets/_Project/Scripts`。

## 目录怎么分

保留现有五个主要文件夹，不再增加一层层分类。

| 文件夹 | 负责什么 | 通常什么时候改 |
| --- | --- | --- |
| `Attacks` | 攻击参数、发射器、子弹、激光、地面区域及共用清场 | 改单个技能的行为、伤害、预警和表现 |
| `AttackTesting` | 整关编排、绝对时间调度、阶段循环、点位库和选点 | 改关卡播放规则、片段测试、入场退场、随机位置 |
| `AttackTesting/Editor` | 编排面板、点位编辑、参数绘制、时间预估和 Excel 文本 | 改编辑器操作方式和显示 |
| `EmitterTesting` | 单发射器反复发射的测试驱动 | 在 Prototype 单独调试技能 |
| `EmitterTesting/Editor` | 单发射器测试窗口及 Inspector | 改单技能测试工具 |
| `Player` | 移动、活动范围、血量、无敌时间、受伤反馈 | 改玩家控制和受伤规则 |
| `UI` | 血量图标、计时、结算、最高分 | 改游戏界面 |

**`AttackTesting` 的名字虽然带 Testing，里面的运行脚本也在负责当前关卡。不能把整个文件夹当成可删除的测试工具。只有 `Editor` 子目录不进入游戏运行包。**

## 游戏逻辑怎么连起来

整关测试：

`AttackTimelineWindow（编辑设置） → AttackTimelineController（运行编排） → ITimelineEmitter.Fire → 各种子弹 / 激光 → PlayerHealth / GroundArea`

单技能测试：

`EmitterTestWindow（选测试对象） → EmitterTestDriver → AttackSequence（反复执行连发与冷却） → 同一套 ITimelineEmitter.Fire`

两条流程共用实际攻击脚本和 `AttackSettings`，但时间规则不同：

- 整关使用 `AbsoluteAttackSchedule`，按绝对秒数触发。遇到忙碌或错过时刻会跳过，后续攻击不顺延。
- 单技能使用 `AttackSequence`，根据准备、连发、冷却不断重复，部分攻击会等待当前攻击结束。
- 不应让两个驱动同时控制一个运行中的发射器；编排创建实例时会关闭实例上的单技能测试驱动。

## 每个脚本的作用

### Attacks

| 脚本 | 大致功能 |
| --- | --- |
| `AttackSettings.cs` | 共用的攻击配置：子弹数、扇形角度、瞄准、连发数和间隔、测试冷却、墨水持续时间、Impact 落点；负责配置复制及落点检查、读取。它不负责推进时间。 |
| `ITimelineEmitter.cs` | 发射器统一接口，供两种驱动调用；同文件的 `IEmitterOrientation` 提供发射器素材的默认朝向。 |
| `AttackCleanup.cs` | 统一识别需要清除的攻击对象。编排只清自己的场景；单技能窗口保留清理所有已加载场景的行为。 |
| `PatternEmitter.cs` | 生成一轮扇形子弹，并报告准备阶段是否结束。 |
| `PatternBullet.cs` | 扇形子弹的展开、蓄力、加速、飞行、命中和寿命；Impact 碎片也复用它。 |
| `BounceEmitter.cs` | 生成反弹弹，支持扇形分布和朝向玩家。 |
| `BounceBullet.cs` | 在玩家活动边界内反弹、沿移动路径检测伤害；命中后保留，没有普通寿命或反弹次数上限。死亡或测试清场仍会销毁。 |
| `TraceEmitter.cs` | 生成 Trace / InkSlow / InkBurn 使用的子弹，管理炮口朝向和发射前跟踪期间的占用。 |
| `TraceBullet.cs` | 落点预警、发射、飞行追踪、抵达或碰撞后爆炸、爆炸伤害，以及可选的地面区域生成。三种攻击靠不同预制体参数区分。 |
| `LaserEmitter.cs` | 生成并持有当前激光，报告忙碌状态；当前激光结束前不能再次发射。 |
| `LaserBeam.cs` | 激光的预警、蓄力、绘制、保持和淡出；生成激光几何与碰撞区域并处理伤害。 |
| `LaserGeometry.cs` | 激光形状与活动范围的多边形裁剪计算；不是另一套激光攻击。 |
| `ImpactEmitter.cs` | 按攻击序号读取配置的落点并生成 Impact。 |
| `ImpactBullet.cs` | 落点预警、下落、落地爆炸、范围伤害和碎片生成。 |
| `GroundArea.cs` | 地面减速/燃烧区域的登记、持续时间、效果查询和显示；同类重叠区域的视觉处理也在这里。 |
| `GroundAreaGeometry.cs` | 计算地面圆形区域被其他同类区域遮挡后的可见网格，避免透明色重复叠加。 |

`Bullet.prefab` 这个资产仍然存在，而且正在被使用；它挂的是 `PatternBullet`。它和本次删除的旧 `Bullet.cs` 不是同一件事。

### AttackTesting

| 脚本 | 大致功能 |
| --- | --- |
| `AttackTimelineController.cs` | 执行整关编排：验证轨道、创建发射器、出入场、调用攻击、点位占用、每次攻击重新入场、区间测试、无尽 P3、停止和清场。包含读取旧 Group 数据的迁移逻辑。 |
| `AbsoluteAttackSchedule.cs` | 将攻击时刻展开成连发序列，并按绝对时间消费每次发射；同文件的 `EndlessAttackLoop` 计算循环轮次、编排时间及重复攻击。 |
| `EmitterPointLibrary.cs` | 保存点位 ID、坐标、方向、边和区域，提供默认布局生成及世界坐标转换；不负责发射。 |
| `EmitterPointSelection.cs` | 随机选点、同组间距、跨轨道占用、排除上一轮点位、预留和释放；编辑器预览和运行编排共用它。 |

### AttackTesting/Editor

| 脚本 | 大致功能 |
| --- | --- |
| `AttackTimelineWindow.cs` | 编排窗口的总览与选中轨道详情、全局/P1/P2/P3 时间显示、范围测试和播放操作；同文件的 `AttackTimelineTimeView` 做时间显示换算。 |
| `AttackSettingsGUI.cs` | 按发射器类型显示适用的攻击参数；编排面板和单技能 Inspector 共用，避免各写一套设置界面。 |
| `AttackTimingPreview.cs` | 根据预制体参数估算准备、伤害和退场时间，提示重入冲突并导出 Excel 可用文本。只是预估，不驱动实际攻击。 |
| `EmitterPointLibraryEditor.cs` | Scene 中的点位与方向操作、布局生成、区域预览、随机选点预览及保存点位库预制体。 |

### EmitterTesting

| 脚本 | 大致功能 |
| --- | --- |
| `EmitterTestDriver.cs` | 单技能测试入口；找到指定发射器、玩家和参数，持续推进测试调度。 |
| `AttackSequence.cs` | 单技能测试的初始等待、连发、冷却和失败停止；从 `AttackSettings.cs` 中拆出，避免配置类夹带运行流程。 |
| `Editor/EmitterTestWindow.cs` | 独立测试窗口：启停发射器、只测一个、选择对象、清场。用 `ITimelineEmitter` 识别发射器。 |
| `Editor/EmitterTestDriverEditor.cs` | 测试驱动的 Inspector；调用共用的 `AttackSettingsGUI` 显示不同攻击参数。 |

### Player

| 脚本 | 大致功能 |
| --- | --- |
| `PlayerMovement.cs` | 读取移动输入，应用地面减速，将玩家限制在活动范围内。 |
| `PlayerMovementArea.cs` | 定义并验证活动区域四边形、约束移动、显示边界；Bounce 和 Laser 也使用这份边界。 |
| `PlayerHealth.cs` | 血量与死亡状态、伤害和无敌时间、燃烧伤害节奏、受伤闪烁/声音，通过事件通知 UI。 |

### UI

| 脚本 | 大致功能 |
| --- | --- |
| `HealthBarUI.cs` | 订阅血量事件，把当前/最大血量传给图标显示；提供 Icon Sprite、大小、间距和颜色设置，并兼容旧血条对象。名字保留，避免额外迁移。 |
| `HeartHealthGraphic.cs` | 实际绘制离散血量图标：默认圆点，指定 Sprite 后使用图片；不处理伤害规则。 |
| `SurvivalTimerUI.cs` | 显示游戏时间并记录实际存活时间。区间测试的显示时间跟随编排，分数使用实际存活时间。 |
| `GameOverUI.cs` | 玩家死亡后显示结算、分数和新纪录，保存本机最高分。 |

## 重叠功能的检查结论

| 项目 | 判断与处理 |
| --- | --- |
| 两个面板各列一份需要清理的攻击类型 | 真正重复。已统一到 `AttackCleanup`，保留调用方原来的清理范围。 |
| Trace 的预警圈和爆炸圈各写创建逻辑、各写圆形顶点计算 | 真正重复。已共用 `CreateRing` 和圆形位置更新，颜色、线宽、排序及实际落点保持原有规则。销毁也复用同一处资源释放代码。 |
| 单技能窗口硬编码所有发射器类型 | 已改为检查 `ITimelineEmitter`，新发射器实现接口后可被窗口发现。具体参数界面仍需要按新类型补充。 |
| 旧 `Bullet.cs` 与现有基础弹行为接近 | 当前场景/预制体/资产无脚本 GUID 引用，代码只剩清场名单引用。已删除旧脚本及其 `.meta`，保留 `Bullet.prefab`。 |
| `AttackSequence` 与 `AbsoluteAttackSchedule` 都负责调度 | 用途不同，不能直接合并。前者循环等待，后者严格跟随绝对秒数。已通过目录和注释明确区别。 |
| Trace、InkSlow、InkBurn 都用 Trace 脚本 | 已经是复用，不是多余。区别在预制体参数与 Ground Area Prefab。 |
| Pattern 与 Impact 碎片都用 PatternBullet | 已经是复用，保留；碎片通常关闭蓄力。 |
| LaserGeometry 与 GroundAreaGeometry 都处理多边形 | 目标不同：一个裁剪激光条带，一个扣除同类地面区域的重叠部分。本次不合并成难以理解的通用几何框架。 |
| 血量控制与图标绘制有两个脚本 | 分工不同，保留。后期换爱心、改变图标样式不需要改玩家伤害逻辑。 |
| 旧 Group 迁移、FormerlySerializedAs、旧血条兼容 | 有数据兼容用途，保留；不能只因当前 Inspector 不显示就删除。 |

## 本次整理的边界与验证

- 移动 `AttackSettings.cs`、`ITimelineEmitter.cs`、`EmitterPointLibraryEditor.cs` 时同步移动 `.meta`，保留 GUID。
- 没有在本次整理中重排攻击、改血量、改预制体参数或改场景；之前尚未提交的功能和场景修改继续保留。
- `TraceBullet` 的初始化、预警计时、飞行追踪、碰撞、爆炸伤害及淡出方法做了代码 token 比对，功能代码保持一致；主要变化是格式、注释和重复绘制逻辑合并。
- 运行代码与编辑器代码分别编译；循环、范围调度、单技能连发和清场对象分类做了离线逻辑检查。
- 离线检查不能代替 Unity Play Mode；需检查一次 Trace/Ink 的预警和爆炸圈、两个测试入口的清场、血量显示，以及 P3 循环衔接。

## 接下来做特效时，改哪里

| 想做的效果 | 优先查看 |
| --- | --- |
| 炮口出现、发射瞬间的特效 | 对应 `*Emitter.cs`；但有准备阶段的攻击，真正射出时间应以子弹的发射阶段为准 |
| Fan 展开、蓄力、射出、命中 | `PatternBullet.cs` |
| Trace / Ink 的落点预警、飞行拖尾、爆炸 | `TraceBullet.cs` |
| 激光预警、蓄力、绘制、淡出 | `LaserBeam.cs` |
| Impact 落点预警、下落、落地爆炸 | `ImpactBullet.cs` |
| 减速坑、燃烧坑的出现、持续、消失 | `GroundArea.cs` |
| 玩家受伤闪光和声音 | `PlayerHealth.cs` |
| 血量图标 | `HealthBarUI.cs` + `HeartHealthGraphic.cs` |

维护约定：

1. 编排负责“什么时候、在哪里、几个发射器”，攻击脚本负责“这次攻击怎么执行”。美术特效不应反过来修改编排时间。
2. 后续逐个技能加入特效时，再把该技能的表现抽成独立组件；不要提前给所有攻击新增一套空的特效框架。
3. 预警结束、真正射出、命中/落地、攻击结束是不同节点。挂特效前先确定对应哪个节点，避免把 `Attack Time` 误当成实际射出的时刻。
4. 激光形状、预警范围、地面区域显示与实际伤害范围存在关联，不能只改网格尺寸而不检查碰撞和判定。
5. 每个攻击负责自己创建的临时材质、网格、预警和特效的释放。跟随子弹销毁的效果可作为子物体；需要残留的效果要有独立寿命，并考虑测试清场。
6. 新增攻击类型后检查 `AttackCleanup`、`AttackSettingsGUI` 和 `AttackTimingPreview`。后两者仍按类型区分，接口识别不会自动生成它们。
7. 修改准备时间、持续时间、退场条件时，同时核对时间预估面板，避免实际表现和 Excel 预估不一致。
8. 当前控制器仍承担较多职责。若继续扩展关卡规则，优先单独整理运行状态与位置生命周期；不要为缩短文件而随意拆散现有序列化字段。
