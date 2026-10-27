# 攻击阶段与特效接入核对

> 2026-10-27代码分类说明：落地组件现名LandingExplosionVfx，子弹朝向组件现名TraceBulletOrientation；原GUID保留。本文按时间保留调试记录，早期参数/引用可能已被后续章节或手动调参覆盖；当前目录见ScriptGuide.md。

核对日期：2026-10-25。依据 `unity` 对话中的攻击设计及当前脚本、预制体。下列数字是磁盘上已保存的基础预制体参数；场景覆盖、Track 设置优先，不能拿旧对话中的测试数值覆盖当前设置。

## 时间口径

- `Attack Times` 是一次攻击流程开始的时刻，不一定是实际射出时刻。
- `Burst Count` 是连续启动几次攻击，`Bullet Count` 是每次同时生成几颗，`Emitter Count` 是几个发射器。
- 整关编排按绝对时间执行：第 n 发启动时间 = Attack Time + n × Shot Interval（n 从 0 开始）。忙碌或错过时刻会跳过，不向后顺延。
- Prototype 使用 `AttackSequence` 循环测试，计时会受准备、忙碌和冷却影响：Fan 会额外等待展开与蓄力；带跟踪预警的 Trace/Ink 在准备期间暂停间隔计时；Laser 要等待当前激光结束。不能把这里的间隔直接套成整关的绝对时刻。
- 发射器生成子弹、子弹真正射出、落地、危险消失是不同节点。炮口释放闪光通常跟随真正射出；准备效果跟随准备阶段。
- 特效帧率只改变图片播放速度，不能改变伤害时长、飞行速度、连发间隔或发射器退场时机。

## 各类攻击

| 类型 | 当前阶段与机制 | 特效必须遵守的节点 |
| --- | --- | --- |
| Fan / PatternBullet | 沿各自固定方向展开 1 单位 / 1 秒 → 停住蓄力 3 秒 → 用 0.3 秒加速到正常速度 5 → 直线飞行。飞行寿命 6 秒从发射阶段算起。准备阶段无伤害。 | 展开要看清数量和方向；发射闪光不能在创建瞬间提前播放。准备后沿原预告方向释放，不随炮口后续旋转。 |
| Bounce | 每次发射可瞄准当时玩家位置；射出后速度 3，按玩家活动范围反弹。命中不销毁，不设普通寿命或反弹次数上限。 | 炮口闪光在射出时；反弹效果在真实碰边位置；不能因为发射器退场或碰到玩家而播放销毁效果。 |
| Laser | 细线预警 → 短线缓缓前伸蓄力 → 按速度曲线加速伸长 → 完整线条保持 → 淡出。方向由发射器朝向确定，起点来自 Muzzle。 | 蓄力短线持续可见；已画出的整条线在 Drawing 和 Hold 都持续可见；只有 Fade 才淡出。预警及蓄力无伤害，Drawing/Hold 有伤害，Fade 不再检查伤害。 |
| Trace | 当前普通 Trace 的圈固定在攻击开始时的玩家位置，预警 0.5 秒 → 发射，先快后慢 → 抵达或飞行中碰玩家后爆炸。无飞行追踪，无地面残留。 | 预警、实际发射、飞行、爆炸分开；提前碰撞时爆炸位置是实际碰撞处；爆炸视觉 0.5 秒不代表持续爆炸伤害。 |
| InkSlow | 预警跟随玩家 1 秒，平滑时间 0.05 → 发射后落点固定 → 抵达落点时伤害 1 并留减速区。飞行途中不因碰玩家提前爆炸。 | 预警到飞行衔接不能添加停顿；地面效果从落地开始，独立于墨水弹对象。基础区域半径 1.5，速度倍率 0.3，基础寿命 150 秒，可由 Track 覆盖。 |
| InkBurn | 预警跟随 1 秒 → 发射后最多继续追踪 2 秒，转向速度 720 度/秒 → 锁定最终落点并抵达 → 留灼烧区。飞行途中不碰撞引爆，落地爆炸伤害 0。 | 追踪期圈和子弹继续移动，不能提前锁死；2 秒是追踪上限，不是固定飞行时间。基础区域半径 1、寿命 90 秒，可由 Track 覆盖。区域伤害 1 / 间隔 1 秒，受玩家无敌保护，同类重叠不累加伤害。 |
| Impact | 按列表选固定落点 → 预警 3.5 秒 → 从高度 6 下落 0.5 秒 → 落地一次范围伤害，半径 0.4，同时生成 6 颗 Fragment。 | 下落途中不判命中；碎裂/尘土在落地时触发；爆炸圈的 0.25 秒是视觉淡出，不是持续爆炸伤害。 |
| Fragment | 复用 PatternBullet，当前 Use Charge 关闭，生成后以速度 3 直接向外飞，飞行寿命 6 秒。 | 不再重复 Fan 的展开蓄力；与 Impact 落地同步生成，逐颗命中/销毁与整体碎裂效果区分。 |

## 曲线不能混为一谈

- Fan：X 是加速阶段的时间进度，Y 是速度倍率。加速结束后倍率固定为 1。
- Laser：X 是 `Acceleration Duration` 内的时间进度，Y 是伸长速度倍率；实际伸长速度 = Draw Speed × 倍率。蓄力前伸另外使用平滑位置插值。
- Trace / Ink：X 是按初始距离与剩余距离算出的飞行进度，Y 是速度倍率；有最低速度倍率限制。追踪会改变剩余距离，所以不能把它当作固定秒数的动画。
- Impact：`Fall Curve` 的 X 是下落时间进度，Y 是已经下落的距离比例，不是速度倍率。

## Laser 当前预览

对象：`Assets/_Project/Prefabs/effect/LaserBeam_CrispPreview.prefab`。两个脚本都在根物体；运行时 `Crisp Juice Line Blast` 子物体负责绘制素材。

当前参数：Warning 2 秒；Charge 1.5 秒；初始长度 0.6，蓄力额外前伸 0.3；Acceleration 0.12 秒；Draw Speed 100；最终长度 20，宽度 0.5；Hold 1.5 秒；Fade 0.2 秒。速度曲线关键点为 (0, 0.02)、(0.65, 0.05)、(1, 1)。Hold 从线条画满时开始计时，不能从动画开始或攻击开始时算。

`line_blast_h_yellow` 是 16 帧的一次性素材。第 16 帧中心 alpha 只有 1/255；停在末帧会让仍有伤害的激光几乎不可见。

当前处理：蓄力使用第 4 帧；Drawing/Hold 在第 4、5、6、5 帧之间循环；Fade 使用相同素材并跟随 `LaserBeam.VisibleAlpha` 淡出。帧号以 1 开始。长度每帧读取 `VisibleLength`，保留原有蓄力前伸和加速曲线。预警线继续由 LaserBeam 显示，旧的纯色色块由预览预制体上的 `Hide Built In Beam Visual` 隐藏。

### Unity 中验证

1. 退出 Play，等待脚本编译；选中 effect 文件夹里的预览预制体。
2. 检查 `Hide Built In Beam Visual` 已勾选，`Sustain First Frame = 4`、`Sustain Last Frame = 6`。
3. 在 Prototype 的单发射器面板只测试 Laser。
4. 观察完整预警、蓄力短线缓缓前伸、突然射满、保持完整线条、最后淡出。
5. 临时把 Hold Duration 改成 5，确认画满后连续可见 5 秒；改 Frames Per Second 只应改变闪动速度，不应改变保持时间。测试后恢复原值。

本轮离线检查能确认选帧范围、素材透明度和编译结果，实际构图与运行显示仍需 Unity Play 验证。

## Trace 爆炸预览（Glow Pulse）

预置体：`Assets/_Project/Prefabs/effect/Bullet_Trace_CrispPreview.prefab`。Prototype 中 `TraceEmitter` 的 Drop Prefab 引用它；原始 Bullet_Trace、墨水预制体及关卡引用保持原有配置。

根物体保留 Sprite Renderer、Circle Collider 2D、TraceBullet，增加 LandingExplosionVfx。爆炸时自动创建子物体 `Crisp Juice Trace Explosion`，与当前子弹一起销毁。只使用 `glow_pulse_yellow_loop.png`，PNG 保持原文件内容；`Art/VFX/TraceEffectTint.shader` 在显示时将颜色替换成紫色，保留透明度。

1. 退出 Play，在 Project 中选中上述预置体。
2. Trace Explosion Vfx 的 Sheet 已引用光晕素材，无需选择 Style。Trace 的 Use Sheet Color 关闭，保留原有改色设置。
3. Match Bullet Color 勾选时，使用同一根物体 Sprite Renderer 的颜色；取消后使用 Tint。Opacity 只控制爆炸透明度。
4. 在 Prototype 单发射器面板中只测试 Trace；比较落点爆炸和途中碰撞爆炸，两者都应在真实爆炸处显示。
5. 每次退出 Play 后修改预制体，再重新播放检查。

素材为 24 帧、8 列、每格 192 像素。24 帧按原有 Explosion Visual Duration（当前 0.5 秒）完整播放一次，相当于 48 帧/秒；不会无限循环或重复判伤。Fade Out Fraction 默认 0.25，仅在最后四分之一时间淡出。尺寸以主要亮环的最大半径对应 Explosion Radius；Glow Pulse 的柔和外晕可超出该半径，属于装饰。子弹的 0.3 缩放会被特效子物体抵消，避免爆炸范围跟着缩成 0.3 倍。

爆炸材质或图片引用缺失时退回原来的爆炸圈。预警、速度曲线、飞行追踪和伤害时机均由 TraceBullet 原有流程处理，尚未添加炮口闪光。

### Trace 飞行碎屑（替代连续拖尾）

预览预置体根物体继续使用 TraceTrailVfx；运行时创建 Trace Graphite Fragments 子物体及世界坐标粒子系统。程序生成不规则三角形网格，每次只发射一片，朝向随机、左右错开，与子弹之间留空隙。没有碰撞器或额外伤害。保留原 BeginFlight / EndFlight 接口，预警时不生成，落地停止生成，已有碎屑继续缩小淡出；清场随子弹清理。

- Piece Lifetime：0.2 秒。
- Max Visible Pieces：最多3片。
- Piece Size：0.045～0.075世界单位。
- Emission Interval：0.065～0.09秒随机间隔；每次画面更新最多生成一片，不补发成团。
- Min Spacing：至少前进0.12世界单位才生成下一片，防止低速堆叠。
- Attachment Offset：最小后移0.17世界单位；按子弹可见尺寸进一步留空隙。
- Side Offset：左右偏移0.025～0.06世界单位。
- Opacity：0.65；Match Bullet Color 开启，Color Lightening=0.18使碎屑比子弹稍浅。

只在Prototype的Trace预览使用。碎屑用内置白纹理和三角网格，不需要动画素材。测试发射前无碎屑、飞行中稀疏且留在经过的位置、抵达/提前碰撞后停止新增，以及重新运行无残留。

## InkBurn 落地冲击环预览

预置体：`Assets/_Project/Prefabs/effect/Bullet_InkBurn_CrispPreview.prefab`。由原始 Bullet_InkBurn 复制，原有全部参数保留，仅增加 LandingExplosionVfx（InkBurn 本来就使用 TraceBullet）。Prototype 中 Emitter_InkBurn 的 Drop Prefab 改为该预览；其他关卡继续使用原预制体。

- Sheet：ring_shockwave_pink，16 帧、8 列、192px；Explosion Visual Duration 为 1 秒，播放一次。Playback Ease Out 为 1：动画进度采用 1-(1-t)²，前快后慢；Fade Out Fraction 为 0.7，最后 0.7 秒按实际时间连续淡出。Trace 默认 Playback Ease Out 为 0，保持匀速。
- Use Sheet Color：开启，保留素材粉色和白色亮环。此时 Match Bullet Color / Tint 不参与改色。
- Opacity：1；Fade Out Fraction：0.1，素材自带消散，只在最后 10% 再收尾。
- Source Radius：86 像素，使主要扩散环的最大范围约对应 Explosion Radius（当前 1）；外晕属于装饰。
- 运行时效果在预置体根物体下生成，显示在子弹同一 Sorting Layer 的前一层。

落点追踪和转弯 → 抵达 → 粉色环爆开，同时生成 GroundArea_Burn → 环消失但灼烧区继续存在。落地爆炸伤害仍为 0，后续伤害由灼烧区负责。没有给 InkBurn 添加拖尾。

退出 Play 后选中该预览预置体调整，使用单发射器测试面板只开启 Emitter_InkBurn。检查环中心与最终落点一致、环消失后灼烧区仍在，以及发射间隔和追踪时长沿用原设置。

## InkSlow 落地冲击环预览

预置体：`Assets/_Project/Prefabs/effect/Bullet_InkSlow_CrispPreview.prefab`。复制原 InkSlow，在根物体挂 LandingExplosionVfx；Prototype 的 Emitter_InkSlow 使用该预览。

使用 ring_shockwave_blue（16 帧、8 列、192px），沿用 InkBurn 的播放节奏：Explosion Visual Duration = 1 秒，Playback Ease Out = 1，Fade Out Fraction = 0.7。Match Bullet Color 开启，跟随 InkSlow 子弹颜色。环最大主要半径对应原 Explosion Radius = 1.5。原预警、速度曲线、追踪、落地伤害和减速区配置保留，冲击环消失后减速区继续存在。未添加拖尾。

## Impact 落地尘土预览

Bullet_Impact_CrispPreview 根物体增加共享 LandingExplosionVfx，支持 ImpactBullet 的落地进度。Prototype 的 Emitter_Impact 引用此预览。dust_land 为14帧、8列、192px；原色播放0.5秒，匀速，最后25%淡出。Sheet Pivot=(0.5,0.23)，使素材下部尘土中心对齐落点。成功播放时隐藏旧白圈；缺失/禁用组件或素材无效时保留旧白圈。原预警3.5秒、下落0.5秒、下落曲线、伤害、6颗碎片与发射角均保留。

## Bounce 碰壁方片预览

Bullet_Bounce_CrispPreview 根物体挂 BounceWallVfx，Prototype 的 Emitter_Bounce 引用此预览。反弹分支在真实碰壁点手动发射4~6个实心小方片，首次入场不触发；使用世界坐标，不跟随子弹。向场内散开，角落处过滤朝外方向；0.25秒内减速、旋转、缩小并淡出。颜色默认跟随子弹，无碰撞器和伤害。每颗子弹复用一个粒子系统，最多128个粒子，销毁子弹时一起清理。原预制体与反弹/伤害规则保持原样。

## Trace 炮口短闪预览

Prototype 的 TraceEmitter 根物体新增 MuzzleSparkVfx；运行时在现有 Muzzle 下创建 Muzzle Burst Flash，使用实际发射位置并复用一个视觉物体。Fan、Bounce、InkBurn、InkSlow 的 Prototype 发射器也已接入，见下节。

素材为 Art/VFX/SparkSheet/spark-burst.png，512×128、16帧、8列、每格64px，原始JSON与许可一并保存。预览将原速约1.33秒压到Duration=0.5秒；Size=0.55世界单位（完整格子），Opacity=0.9，当前关闭Match Bullet Color，保留用户选择的浅蓝灰Tint，帧间混合开启，Emitter层Order=10。

TraceEmitter 在 Initialize 前订阅子弹的 Launched 事件；TraceBullet 在完成预警、设置飞行方向并开启飞行后通知一次，发射器处理后立即退订。零预警也不会漏事件。取消预警/玩家死亡导致未发射时不触发短闪。原预警、锁定、追踪、速度曲线、连发间隔均保留。短闪只处理显示，不参与伤害。

测试：Prototype仅开启Trace，检查预警期间无短闪，每次实际出弹才在笔尖闪一次，四连发对应四次短闪；落地不再闪，重启测试无残留。当前连发间隔0.5秒大于短闪0.5秒；未来间隔短于Duration时，新的短闪会重播同一视觉物体。

## 其他发射器的炮口短闪（Prototype）

Emitter_Fan、Emitter_Fan (1)、Emitter_Bounce、Emitter_InkBurn、Emitter_InkSlow 根物体均新增 MuzzleSparkVfx，复制当前 Trace 的 spark-burst 参数：Duration=0.5秒、Size=3、Opacity=0.9、Match Bullet Color关闭，Tint=(0.9103774,1,0.9569842,1)，帧间混合开启。每个发射器可独立调整。

这五个发射器各创建并绑定 Muzzle 子物体，初始本地位置为零，保持原出弹位置；以后放置文具美术时，将 Muzzle 移至出口即可。PatternEmitter / BounceEmitter 新增可选 Muzzle 字段；其他场景为空时回退根物体，与原行为一致。

- Bounce：生成一轮子弹后，炮口闪一次。多颗同发只闪一次。
- Fan：PatternBullet 在展开、蓄力结束并开启飞行碰撞的实际节点通知 Launched；发射器只监听每组的一颗代表子弹，整组只闪一次，预告展开与蓄力时不闪。弹道、准备时间与速度曲线保持原样。
- InkBurn / InkSlow：复用 Trace 的 Launched 通知，预警结束且实际射出时才闪，落地与持续地面区域不触发炮口特效。

运行时在各自 Muzzle 下复用 Muzzle Burst Flash；禁用/清场会隐藏或销毁视觉。当前改动仅配置 Prototype，未替换其他关卡引用。验收时逐个启用发射器，检查上述阶段与每轮次数；Fan另检查两台测试发射器均生效。

## Bounce 手绘墨珠循环动画

- 源图为 `Assets/_Project/Art/Attack/bullet_Bounce.png`，保留用户 PNG 原始像素和透明背景。Sprite Mode = Multiple；5 帧，每帧 48×40px，从 Unity 左下坐标 (4,436) 开始，X 间距 50px；Pivot 均为 Center。PPU = 28，无纹理压缩，Bilinear，无 Mipmap。
- `Assets/_Project/Art/Attack/Animations/Bounce_InkLoop.anim`：只记录根物体 Sprite Renderer 的 Sprite，不记录位移、旋转、缩放或颜色。帧顺序 0→1→2→3→4→0；6fps，单轮 5/6 秒，Loop Time 开启，末尾重复首帧闭合循环。
- `Bounce_Ink.controller`：单一默认状态 Bounce_InkLoop，用户当前 Speed = 1.5。打开 Animator 后选中该状态，可调整 Speed 播放倍率；碰壁形变使用自己的时间，不受这个倍率影响。
- `Assets/_Project/Prefabs/effect/Bullet_Bounce_CrispPreview.prefab` 根物体有 Animator，绑定上述 Controller；Sprite Renderer 指定第 0 帧，Color 为白色以显示原画颜色。用户当前 Scale=(0.3,0.3,1)，本次形变保留此设置。
- Prototype 的 Emitter_Bounce 已引用此预制体，因此导入完成后可直接测试。BounceBullet 速度、命中半径、反弹算法、墙面散射参数保持原设置。五帧循环与下面的碰壁形变同时播放。
- 验证：确认全部非透明像素都完整落入五个切片且没有重复覆盖；PNG 哈希不变；原预制体 Transform、BounceBullet、BounceWallVfx 数据逐块比对一致。实际 Unity 导入与 Play 观感需要在编辑器中确认。

## Bounce 碰壁挤压与回弹

`Bullet_Bounce_CrispPreview` 根物体新增 `BounceSquashVfx`。`BounceBullet.WallBounced` 只在实际反射并完成墙角方向修正后通知，携带墙面向内法线和最终反弹方向；首次飞入活动区不触发。原来的方片散射仍在原节点播放。

运行时层级为根物体 → `Bounce Deformation` → `Art`。Animator 仍对根 Sprite Renderer 换帧，但根 Renderer 仅关闭渲染输出，保留属性供现有脚本读取。Art 每个 LateUpdate 复制当前帧、颜色、材质与排序；形变父物体旋转到受力方向，Art 反向旋转抵消图片自身转动。沿轴比例为 k，垂直比例为 1/k，面积保持不变。根 Transform、伤害范围和移动路径不参与形变。

初始参数：Squash Scale=0.7，Stretch Scale=1.18；Squash Duration=0.04 秒，Rebound Duration=0.08 秒，Settle Duration=0.12 秒，总计 0.24 秒。先压扁，再恢复到原形切换至反弹方向，拉长后平滑收回。连续碰壁会从当前形状开始，不叠加缩放；结束和禁用时恢复原形。显示副本复用已有材质和 Sprite，不产生逐帧资源分配。关闭组件时恢复根 Renderer，方便对比效果。

验收：逐个检查左右墙、上下墙、斜射和墙角反弹；飞入边界不应形变，碰壁后应恢复原尺寸，高光仍连续播放。编译、原参数与引用检查可在外部完成，最终画面及手感需在 Unity Play 中确认。

## InkSlow 手绘墨团与飞行晃动

- 源图 `Assets/_Project/Art/Attack/bullet_InkSlow.png` 与用户提供文件一致，保留透明像素。Multiple 模式下切出 `bullet_InkSlow_0`，矩形为 Unity 左下坐标 (84,416)，83×73px，中心 Pivot，PPU=75，无压缩。实际有色主体为75×65px，配合原根 Scale=(0.8,0.8,1)，宽度约0.8世界单位。
- `Bullet_InkSlow_CrispPreview` 根 Sprite Renderer 指定此图，Color=白色显示原色。原冲击环从 Match Bullet Color 改为独立 Tint，保留之前有效的蓝色 (0.2,0.29803923,0.8,1)，避免根 Renderer 白色导致爆炸变白。原冲击环时长、播放缓动、地面减速区均保留。
- 根物体新增 `InkSlowFlightVfx`；运行时创建并复用子物体 `InkSlow Blob Art`。根 Renderer 保留给 TraceBullet 控制显隐及供其他脚本读取，关闭其直接渲染，子物体同步图片、颜色、材质、排序。动画仅在 IsFlying 且根 Renderer 启用时显示，预警阶段隐藏，落地立即隐藏，禁用组件恢复根 Renderer。
- 当前调整为以旋转和小幅漂晃为主：Wobble Amount=0.02（宽度约±2%，高度取倒数，保持面积）；Wobble Period=1.1秒；用户已将 Tilt Angle 调为0；Rotation Speed=18度/秒；Sway Amount=0.018世界单位。漂晃方向垂直于 FlightDirection，并抵消父物体缩放，使振幅稳定。伸缩与漂晃在发射后前0.15秒平滑进入；落地或禁用恢复显示子物体的零位移、原形和初始角度。根 Transform、碰撞、预警1秒、飞行速度曲线、落点和爆炸伤害不参与动画。
- Rotation Speed Curve：X使用 TraceBullet 新增的只读 FlightProgress，与原 speedCurve 相同，按起点到当前目标的距离计算（0发射、1抵达）；Y为转速倍率。默认点为 (0,0.15)、(0.45,2.5)、(1,0.2)，各点切线为0，平滑实现前慢、中段快、落点前慢。基础18度/秒时分别约为2.7、45、3.6度/秒。曲线下降只降低后续转速，不让角度倒退；每帧以相邻转速的平均值累加角度。空曲线回退匀速，负倍率按0处理。原飞行速度曲线为前快后慢，未改成旋转的慢—快—慢形状。
- Prototype 的 Emitter_InkSlow 已引用此预览预制体，可直接测试。编译与静态保留检查通过后，最终动画观感仍需在 Unity Play 中确认。

## Fan 手绘子弹朝向

`Bullet.prefab` 使用笔尖朝上的 bullet_Fan_0。PatternEmitter 原本以 Quaternion.identity 生成子弹，移动方向是独立的世界向量，图片不会自动跟着转；原圆形占位图看不出这个问题。PatternBullet 现在新增 Artwork Orientation：Face Flight Direction 为开关，Sprite Forward Direction 表示原图尖端方向。Fan 的 Bullet.prefab 开启并设为(0,1)，Initialize 时以原图朝向到本颗 heading 的有符号夹角设置 Rigidbody2D.rotation，所以展开、蓄力及飞行都朝向对应扇形弹道。方向仍在创建时锁定，炮口后续转动不影响当前一轮。未开启此设置的其他 PatternBullet（包括 Fragment）保留原行为；速度、蓄力和伤害算法不变。Fan 当前使用居中的竖直胶囊体，胶囊与根物体一起朝向弹道；粉笔碎屑仍使用原来的圆形碰撞体。

## Impact 手绘粉笔与碎屑

- 原图 `Assets/_Project/Art/Attack/bullet_Impact.png` 和 `bullet_Fragment.png` 保留用户 PNG 像素与透明背景；Multiple 切片，无压缩、Bilinear、无 Mipmap。
- 粉笔切片 `bullet_Impact_0`：Unity 左下坐标 (103,306)，50×111px，Custom Pivot=(0.5,0)，PPU=100。挂在 `Bullet_Impact_CrispPreview/Visual` 的 Sprite Renderer，Color 保持白色；Visual Scale 从占位图的(0.5,0.8,1)改为(1,1,1)，按原画比例显示，宽0.5、高1.11世界单位。底部支点保证下落结束时粉笔底端而不是中心抵达落点。
- 碎屑切片 `bullet_Fragment_0/1/2`：各32×28px，X分别21、113、203，Y=442，中心支点，PPU=20。新增 `Assets/_Project/Prefabs/effect/Bullet_Fragment_ChalkPreview.prefab`，复制原 Bullet_Fragment，保留根 Scale=(0.15,0.15,0.15)、CircleCollider2D、速度3、寿命6秒与伤害1。实际外形最大边约0.13～0.18世界单位。
- 碎屑预览根物体新增 ChalkFragmentVfx：Variants 是三种静态外形，Rotation Speed 默认80度/秒，Alternate Spin 开启。Impact 生成碎屑后、Initialize 前调用 Configure(i)，按0/1/2/0/1/2轮换图片，初始角度错开137.5度，相邻碎屑交替旋转，不消耗 Unity 游戏随机序列。
- 运行时碎屑根 Renderer 保留属性给 PatternBullet 读取、写入透明度；其渲染输出隐藏，生成 `Chalk Fragment Art` 子物体同步外观并旋转，根刚体、圆形碰撞体和弹道不随外观转动。关闭组件恢复根 Renderer，销毁时清理子物体，无逐帧资源创建。
- 当前 Impact 预览的 Fragment Prefab 改为上述碎屑预览，Prototype 的 Impact 测试可直接使用。原 Bullet_Fragment.prefab 未改；原 Impact 预制体未改。预警3.5秒、下落0.5秒、fallCurve、落地伤害、6枚碎屑、startAngle、烟尘0.6秒、坑印0.5秒及其颜色保留。
- 验证切片覆盖全部非透明像素、PNG哈希与源文件一致；原攻击/碰撞块比对一致；新增外观逻辑编译通过。实际下落接触点、粉笔在白纸上的辨识度、碎屑旋转与命中观感仍需 Unity Play 验收。

### 重新导出原画后的恢复

用户自行将粉笔与碎屑调白，重新导出后清空了切片；已重新切分 bullet_Impact、bullet_Fragment，同时恢复误删的 bullet_Fan 切片。粉笔新导入 GUID 为6e786e551e0a0044f9a1951cae041471，已修复当前 Impact 预览的 Sprite 引用。三个预制体均使用普通 Sprites-Default 材质、白色 Renderer Color，直接显示原画；不使用提白 Shader 或专用材质。粉笔仍为底部中心、PPU100，碎屑3片仍为PPU20。Fan 恢复 bullet_Fan_0（20×41px，Unity矩形47,455,20,41，中心支点，PPU100，internalID1990202230），保留Face Flight Direction开启以及原图朝上(0,1)的设定。既有尺寸、碰撞、旋转、时序等设置保留。

## 粉笔碎屑移动阴影

`Bullet_Fragment_ChalkPreview` 根物体新增 SoftEllipseShadow，运行时生成同级于美术显示子物体的 `Fragment Soft Shadow`。使用普通 Sprites/Default、白色纹理和同心环顶点透明度生成渐变：中心不透明度最高，向外按(1-r²)²降到0，没有新增手绘图或自定义Shader。32段、8层同心环，每颗碎屑仅创建一次网格与材质，LateUpdate只更新位置、尺寸、颜色和排序，禁用隐藏，销毁释放。

初始参数：Shadow Size=(0.28,0.12)世界单位，Shadow Offset=(0,-0.055)世界单位，Shadow Color=(0.18,0.17,0.16,1)，Opacity=0.32。阴影颜色的Alpha与Opacity相乘，并跟随根Sprite透明度。阴影放在同一Sorting Layer、Order低1层；保持水平，不随碎屑显示Art旋转；补偿父级缩放，阴影大小使用独立的世界单位。用户当前碎屑根Scale=0.2保持不动，调整阴影不改变圆形碰撞、速度、弹道与伤害。每颗碎屑拥有自己的阴影，碎屑销毁阴影同步消失；本次没有给下落粉笔主体增加阴影。Unity Play中检查贴地感、柔边和可读性，Inspector可直接调参或取消组件勾选作对比。


## Fan 胶囊碰撞体

Pattern Bullet → Prepare新增Preparation Opacity（0～1），控制展开和蓄力阶段相对于Sprite原始Alpha的透明度倍率。默认0.7，保留添加条目前用户刚调整的值；正式发射恢复originalColor，不改变准备时长、碰撞启用和飞行逻辑。调整子弹预制体上的该值，下一次生成的子弹使用新值；运行时代码编译通过。

用户已在 Bullet.prefab 加入 CapsuleCollider2D，Size=(0.2,0.41)、Offset=(0,0)、Direction=Vertical；本次保留该组件与尺寸，开启 Is Trigger，并删除原 Radius=0.5 的 CircleCollider2D。根Scale仍为0.8，对应胶囊世界宽约0.16、高约0.328。后续直接在 Capsule Collider 2D 的 Size X/Y 调宽和高，Offset调判定中心。

PatternBullet 从强制要求 CircleCollider2D 改为只要求 SpriteRenderer/Rigidbody2D，运行时读取 Collider2D 基类；未配置碰撞体时报明确错误并销毁该颗子弹。仍在 Initialize 关闭碰撞、展开与蓄力结束时开启同一个碰撞体并触发 Launched；速度曲线、方向、伤害、寿命逻辑未改。原 Bullet_Fragment 与粉笔碎屑预览保留圆形，无需替换。每颗只保留一个用于伤害判定的 Collider2D。

验证：运行时代码编译通过；Fan预制体仅删除圆形和开启胶囊Trigger，其他参数逐块保留；扫描所有PatternBullet预制体均有且仅有一个支持的碰撞体。实际擦弹与命中手感仍需运行Fan测试确认。

## Trace 多边形碰撞体

`Bullet_Trace_CrispPreview` 删除原CircleCollider2D，保留用户已添加的PolygonCollider2D及原有9个顶点，开启Is Trigger。TraceBullet移除对CircleCollider2D的强制依赖，读取Collider2D基类；缺少碰撞体时报明确错误并销毁该颗子弹。预警阶段关闭，发射时按Explode On Player Contact启用，落地或命中时关闭，现有命中爆炸与爆炸范围伤害流程保留。多边形随子弹根朝向一同旋转。

调整轮廓：退出Play，打开该预制体，在Polygon Collider 2D点击Edit Collider拖动顶点；Offset只调整判定中心。该修改控制飞行接触判定，不改变Explosion Radius。原Trace/InkBurn/InkSlow其他预制体保留各自圆形碰撞体；扫描全部TraceBullet预制体均只有一个支持的Collider2D。运行时代码编译及数据保留检查通过，擦弹与命中观感需在Unity测试。

## Prototype 的 Ink1 / Ink2 落地对比

素材保存在 `Assets/_Project/Art/VFX/InkLandingTests`，PNG与用户桌面源文件逐字节一致。Ink1为1200×800、6帧；每帧单独对齐到动画中心，切成360×360的方形，Source Radius=132。Ink2为800×800、9帧，切成264×264方形，Source Radius=120。逐帧顺序均为从左到右、从上到下。所有非透明像素均被切片覆盖，中心支点、无压缩、无Mipmap、Bilinear，不加入SpriteAtlas。

`LandingExplosionVfx` 增加可选 `Frame Sprites` 数组，用切片矩形取样；数组非空时覆盖Sheet/Frame Count/Columns/Cell Size。这允许帧中心不严格等距的图集使用现有落地播放代码。要求各帧来自同一张未打包纹理、相同正方形尺寸和中心Pivot；不满足时保留原有爆炸圈回退。数组为空时原规则图集播放方式不变，UV和相邻帧偏移与旧公式完全一致。继续使用原有Tint Shader进行换色及Alpha加权的帧间混合，不生成新的伤害或地面区。

测试预制体在 `Assets/_Project/Prefabs/effect/InkLandingTests`：

| 预制体 | 颜色 | Prototype默认 |
| --- | --- | --- |
| Bullet_InkBurn_Ink1_Test | 红色 | Emitter_InkBurn的Drop Prefab |
| Bullet_InkBurn_Ink2_Test | 红色 | 可手动替换作对比 |
| Bullet_InkSlow_Ink1_Test | 蓝色 | 可手动替换作对比 |
| Bullet_InkSlow_Ink2_Test | 蓝色 | Emitter_InkSlow的Drop Prefab |

四份均复制当前对应CrispPreview，保留飞行本体、动画、曲线、预警、追踪、碰撞、伤害、地面区及其寿命。落地视觉总时长仍为1秒，Playback Ease Out=1、Fade Out Fraction=0.7，Blend Frames开启，Match Bullet Color关闭，使用独立红/蓝Tint。帧间混合只是淡化过渡，不是生成新的形变中间帧；最终自然程度需要Play中比较。

操作：退出Play，选Prototype中的Emitter_InkBurn或Emitter_InkSlow，将对应攻击类型的另一个Test预制体拖入Trace Emitter组件的Drop Prefab，再运行。调大小用测试预制体Trace Explosion Vfx的Visual Scale（缺省1）；调时长用Trace Bullet的Explosion Visual Duration。不要通过更改Explosion Radius调纯视觉尺寸。

本次Prototype仅替换上述两处预制体GUID；Level_Art场景和原CrispPreview预制体字节不变。运行时代码编译、切片覆盖及参数保留检查通过，尚未进行Unity Play观感验收。备份和验证记录在Temp/InkLandingChecks。

### Trace 的 Ink2 测试（Level_Art）

新增 `Assets/_Project/Prefabs/effect/InkLandingTests/Bullet_Trace_Ink2_Test.prefab`，复制当前 `Bullet_Trace_CrispPreview`，落地效果改为同一套Ink2九帧切片，Source Radius=120，Blend Frames开启。保留Trace原绿色Tint、Opacity=0.8、Visual Scale=1、均匀播放、Fade Out Fraction=0.25、Explosion Visual Duration=0.5秒。子弹美术、多边形碰撞体、石墨拖尾、预警、速度曲线和伤害等组件逐块保持一致。

Level_Art的TraceEmitter → Trace Emitter → Drop Prefab已引用该测试预制体；场景只替换这一个引用。Prototype及原Trace预览预制体不变。回退时将Drop Prefab改回Bullet_Trace_CrispPreview。未改运行时代码，数据与引用保留检查通过，Unity实际播放观感待确认。备份/验证在Temp/TraceInk2Checks。

### Bounce 墨滴换为 bullet_Bounce1

`Bullet_Bounce_CrispPreview` 的首帧已由用户指定为 `bullet_Bounce1_0`，本次将 `Bounce_InkLoop.anim` 的全部五帧及末尾重复首帧同步换成 `bullet_Bounce1.png` 的切片。按切片X坐标从左到右播放；当前切片名称的_1与_2顺序和空间顺序相反，因此动画实际引用顺序为_0→_2→_1→_3→_4→_0，保留各Sprite的现有ID和名称。

PNG与桌面bullet_Bounce1.png逐字节相同。切片48×40、PPU28、中心支点不变，统一显式Pivot=(0.5,0.5)，导入压缩关闭以保持清晰。五帧覆盖全部非透明像素。动画原6fps、时长5/6秒、Animator Speed=1.5以及所有碰壁形变、散射、速度和Hit Radius均保留。预制体、Controller和两场景文件字节不变；共用该预制体的场景自动使用新动画。仅资源引用检查，尚未进行Play观感验收；备份和验证在Temp/BounceArt1Checks。

### 粉笔主体与碎屑换为描边版

`Bullet_Impact_CrispPreview/Visual` 改用已导入的 `Art/Attack/bullet_Impact 1.png`（GUID881bd71e621a31d41b2b97dd67823567，Sprite ID1885787132）。保留用户切片(100,306,56,113)、PPU100，支点恢复为底部中心(0.5,0)，描边后的底端继续对齐落点。

`Bullet_Fragment_ChalkPreview` 的初始Sprite与ChalkFragmentVfx三项Variants均换成 `bullet_Fragment 1.png`（GUID931ea65a8c9fc3a40976548e6a452571）。按空间从左到右使用现有Sprite ID1441178232、-1163120511、1625728762；名称和ID保留。新图原PPU100恢复为既有碎屑的20，避免直接替换后缩小五倍。切片使用原先32×28矩形、X=21/113/203、Y=442，中心Pivot，完整包住新增描边；原中间25×25切片漏掉了最左一列描边。两张图均关闭导入压缩，PNG原像素不变。

两场景都继续通过既有Impact预览引用共享碎屑预览。只改上述美术引用和新图导入设置；当前根缩放、碰撞、碎屑转动、渐变阴影、预警、烟尘、坑印、数量和伤害保持原值。切片Alpha覆盖与数据保留检查通过，Play观感待确认。备份/验证在Temp/ChalkOutlineChecks。

描边版接入后，按用户要求移除Bullet_Fragment_ChalkPreview上的SoftEllipseShadow组件及组件列表引用，停止为碎屑生成渐变椭圆阴影。只移除此组件，保留碎屑旋转、移动、碰撞、原画和Impact落地烟尘/坑印；Prototype与Level_Art共用该碎屑预制体。SoftEllipseShadow脚本暂留作可选工具，当前无场景或预制体挂载。备份在Temp/ChalkShadowRemoval。


### Impact坑印椭圆测试

ImpactBullet新增Crater Scale视觉参数，缺省(1,1)保留圆形兼容；Impact预览设为(1,0.55)，Crater Color Alpha从0.5882353降至0.25。半径0.4对应世界宽0.8、高0.44，持续0.5秒保持。预警圈和伤害判定仍为原圆形，烟尘、碎屑及下落时序不变。坑印只是落地装饰，不用于表达伤害边界。运行时代码编译及参数保留检查通过；备份在Temp/ImpactCraterEllipse。


### Trace拖尾绿色显示修正

实际Play中复现预制体Tint为绿但碎屑偏灰。TraceTrailVfx改用独立材质_Color统一控制RGB与Opacity，EmitParams使用白色；显式开启Position/Color/UV粒子顶点流，Color Over Lifetime仍只负责原渐隐。BeginFlight与LateUpdate更新材质颜色。Trace的CrispPreview和Ink2测试版均关闭Match Bullet Color，Tint=(0.2376402,0.38679248,0.18792278,1)，Color Lightening=0以取消浅灰混合。尺寸、寿命、发射间距及运动不变。代码编译通过；画面验证状态见对话。备份在Temp/TraceTrailColorFix。


### Trace三角拖尾材质最终修正

仅修改标准Sprites/Default材质及粒子颜色流未解决实际灰色问题。最终使用TraceGraphiteTrail.shader（NotebookDodge/Trace Graphite Trail）：片元RGB只读取专用_TrailTint，粒子Color流仅用于寿命Alpha渐隐；TraceTrailVfx增加Trail Shader引用，并在两份Trace预览预制体上显式绑定，BeginFlight与LateUpdate使用Material.SetColor更新_TrailTint。保留Inspector的Match Bullet Color、Tint、Opacity和Color Lightening。原有移动、散射、缩放和寿命不变。用户已在Unity中确认现在可通过Tint改变三角碎屑颜色。未执行此前提出的离屏像素自动测试，不以该测试作为验证依据。


### Trace恢复内置Sprites/Default（当前实现）

已切回Sprites/Default，使用白色纹理和白色材质，由粒子startColor提供Tint/Opacity。原三角Mesh显式写入mesh.colors的COLOR通道与当前粒子颜色流组合会导致异常输出；保持Mesh无颜色通道后，内置Shader正常。粒子保持Position/Color/UV顶点流，关闭GPU实例化；运行时修改颜色会更新已有粒子startColor，不改变运动或寿命曲线。

实际Unity渲染对照和正式组件发射检查已完成：同一粒子从绿色改为紫红色，RGB读回与Tint一致；Opacity降到0.25后实际像素Alpha约0.251。专用Shader、两份预制体中的trailShader字段和临时Editor验证脚本均已从活动项目移除，记录在Temp/TraceTrailRenderCheck。之后按用户约定只修改和检查代码，Unity交互运行与观感测试交给用户。


### Laser白色核心独立伤害宽度

LaserBeam新增Damage分组的Damage Width（世界单位总宽度，沿中心线对称），Initialize时限制到0.01～Beam Width。Beam Width/VisibleWidth继续控制原外观，伤害多边形改用activeDamageWidth进行活动区裁剪。绘制/保持期间沿用同一判定，预警、蓄力、速度曲线、Hold/Fade、Damage Interval和OverlapCollider流程均不变。

LaserBeam_CrispPreview的Beam Width保持0.5，Damage Width初始0.1；按当前line_blast素材持续帧测量，白芯宽约0.12～0.13，判定略窄于白芯。原LaserBeam预制体显式保持Damage Width=0.5以保留旧关卡行为。Prototype与Level_Art继续共用CrispPreview。此参数不按PNG像素自动识别白色区域；调整视觉宽度后按观感重新校准Damage Width。

调整入口：Project中打开LaserBeam_CrispPreview → Laser Beam → Damage → Damage Width。更改预制体后重新生成激光生效。用户测试：玩家碰撞体仅接触光晕时不扣血，进入白芯时按原伤害间隔扣血；绘制和保持阶段一致。代码/Editor编译及708项离线检查通过，包含多朝向核心/光晕边界、已绘制长度和活动区裁剪；不操作Unity运行。备份在Temp/LaserDamageWidthChecks。
