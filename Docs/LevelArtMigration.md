# Prototype 美术接入 Level_Art

已将 Prototype 当前保存的六种文具发射器外观和攻击视觉接入 `Assets/_Project/Scenes/Level_Art.unity`。Prototype 保留作为单技能调试场景。

## 场景挂接位置

| Level_Art 中的位置 | 内容 |
|---|---|
| Emitter/Emitter_Trace/Art、Muzzle | 铅笔与笔尖出弹位置，根物体附带 MuzzleSparkVfx |
| Emitter/Emitter_Bounce/Art、Muzzle | 钢笔与出弹位置，根物体附带 MuzzleSparkVfx |
| Emitter/Emitter_Fan/Art、Muzzle | 扇形文具发射器与出弹位置，根物体附带 MuzzleSparkVfx |
| Emitter/Emitter_InkBurn/Art、Muzzle | 红色颜料管与出口，根物体附带 MuzzleSparkVfx |
| Emitter/Emitter_InkSlow/Art、Muzzle | 蓝色颜料管与出口，根物体附带 MuzzleSparkVfx |
| Emitter/Emitter_Laser/Art、Muzzle | 荧光笔与激光出口；使用已有 LaserBeam_CrispPreview |
| Emitter/Impact_Test/Emitter_Impact | 引用 Bullet_Impact_CrispPreview，包含粉笔、烟尘、坑印、碎屑与柔边阴影 |

将目标场景的占位 Square 改为 Art，并复制源场景 Art 的图片、颜色、排序、缩放和局部位置；没有复制 Prototype 的 EmitterTestDriver。所有原有发射器组件 ID 保留，原来的 60 条时间轴轨道继续引用这些组件。

Emitter 父物体仍按原场景配置停用，作为时间轴模板容器。运行时 AttackTimelineController 会克隆对应发射器并启用，所以无需手动启用整组模板。修改 Art 调文具外观，修改 Muzzle 调出口；炮口特效参数位于发射器根物体的 MuzzleSparkVfx。

## 发射方向适配

Level_Art 的默认 Local Forward Direction、发射器根 Transform、时间轴 Rotation Z 均保留。Trace 的源场景默认向左，目标默认向下；Laser 的源场景默认向右，目标默认向上。这两种发射器只对复制来的 Art 和 Muzzle 额外做局部 +90° 旋转，并相应旋转炮口局部偏移。其余四种无需补偿。这样美术和出口对准 Level_Art 原有的发射方向，自定义位置和四边点位编排无需修改。

## 炮口定位修正（当前约定）

上述迁移完成后，用户明确点位应表示炮口位置。现已将 Level_Art 六种文具发射器的根支点改到各自 Muzzle，Muzzle 的 Local Position 统一为(0,0,0)，Art 反向补偿原炮口偏移；模板根位置同时补偿，使编辑场景中的笔身和炮口世界位置保持不变。根旋转、缩放、Local Forward Direction 不变。

由于根支点现在就是炮口，Fixed Point、Random Points 的选中坐标以及 Custom Position 的 Position 都直接定位炮口。瞄准玩家时，Trace/Bounce 绕这个支点旋转，炮口不会因转向绕笔身中心偏移。进退场仍由原时间轴控制，无需新增定位代码或修改攻击时序。Impact 使用落点而非文具炮口，保持原行为。

以后更换发射器图片时，保持 Muzzle 的 Local Position 为零，通过 Art 的 Position/Rotation/Scale 把笔尖对准原点。不要再通过移动 Muzzle 对齐笔尖，否则点位与出弹位置会重新出现偏差。这个支点调整仅应用于 Level_Art；Prototype 保留原单技能摆放。

调整前通过 Unity 保存了当前 Level_Art 的未保存点位修改。验证共18个 Transform 仅改变 Local Position：6个根、6个Art、6个Muzzle；美术的世界位置及坐标基保持一致，时间轴与当前点位库覆盖逐块对比未变。另验证多种角度和非均匀缩放下，Muzzle 始终与根坐标重合。

## 共享资源与保留内容

发射器 Art/Muzzle 是两个场景中各自保存的对象；后续修改其中一个场景的局部摆放，不会自动同步另一个场景。子弹和特效则直接引用 Prototype 正在使用的同一组预制体，修改共享预制体会同时影响两个场景。

当前目标场景接入了 Prototype 已调好的 Trace 三角手绘子弹与碎屑拖尾、Bounce 墨珠动画和碰壁反馈、InkSlow 墨团旋转曲线、InkBurn/InkSlow 落地冲击环、Laser 持续显示，以及 Impact 粉笔碎屑与阴影。炮口短闪采用源场景当前参数（Trace/Bounce Size=3，Fan/InkBurn/InkSlow Size=4）。子弹参数亦使用这组预制体的当前值，例如 Bounce Hit Radius=0.11，Ink 落地视觉1秒，Impact 烟尘0.6秒；没有覆盖成旧预制体的参数。

Level_Art 的完整 AttackTimelineController 序列化块、点位库实例与覆盖、全部进退场参数、攻击次数、间隔、随机种子、玩家、UI、背景、相机和活动边界均保留。背景原本已与 Prototype 使用相同图片及摆放，未重复添加。

## 验证

- 60 条轨道数据和点位库对象逐块对比未变；原发射器根位置、旋转、缩放、父级、默认朝向及启用状态未变。
- 检查六种 Art/Muzzle 的父子关系与炮口引用，炮口特效参数与源场景一致。
- 对 Trace/Laser 的朝向补偿进行多方向计算检查；没有新增缺失的场景引用、重复对象 ID 或缺失资源 GUID。
- 保留 Prototype 文件；未导入单技能测试驱动。迁移仅修改 Level_Art 场景，不修改运行时代码。
- 最终需要在 Unity 打开 Level_Art 运行确认，尤其检查多个文具同时进场时的遮挡、四边出弹位置和激光方向。

本次支点调整后已在 Unity 中启动 Level_Art 观察运行，并退出回编辑状态。该检查不代表完整60条轨道全程验收；坐标校验覆盖六种发射器、多个朝向及非均匀缩放。
