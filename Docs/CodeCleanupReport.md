# 代码整理报告

日期：2026-10-27。范围为自有脚本、维护文档，以及一份确认没有引用的重复VFX导入。第三方TextMesh Pro示例、用户备用原画、效果对比预制体保留。

## 已完成

- 将45个已有脚本及各自meta按职责迁移，保留原GUID。目录从Attacks/AttackTesting/EmitterTesting/Player/UI改为Runtime、Editor、Testing三类；Runtime细分攻击、视觉、关卡、玩家和UI。
- TraceTriangleVisual改名TraceBulletOrientation；TraceExplosionVfx改名LandingExplosionVfx。脚本GUID、序列化字段和预制体引用不变，保留MovedFrom兼容声明。
- 提取SpriteAppearance：Bounce挤压、InkSlow飞行、粉笔旋转三个显示副本共用原来的九项属性复制；各自显隐、变形、计时、事件和资源释放不合并。
- 提取SpriteSheetUv：炮口、落地、激光共用取帧矩形、半像素内缩与混帧偏移；Ink1/Ink2的不等距切片仍按Sprite矩形取样。
- EndlessAttackLoop、AttackTimelineTimeView、IEmitterOrientation各放到与类型同名的文件；方法内容不变。拆分与提取后共有50个脚本，并非新增5种玩法组件。
- 移除TraceTrailVfx.EndFlight未使用的位置参数及唯一调用点的对应实参。
- 将7个采用旧中文编码的脚本转为UTF-8，恢复注释/界面文本的可读性，不更改其字段和方法逻辑。
- 原SparkBurst/spark-burst.png和json与SparkSheet内的文件完全一致，且原SparkBurst的三个资源GUID没有任何场景/预制体/材质/代码引用。已将整个SparkBurst连同license和meta移到Archive/UnusedAssets_2026-10-27；Unity继续使用SparkSheet。

## 为什么这些保留

| 看起来可能重复/弃用的部分 | 检查结论 |
| --- | --- |
| 没有C#调用的VFX组件 | 场景/预制体通过脚本GUID挂载，仍在运行；不是死代码 |
| Editor窗口和Inspector脚本 | 由MenuItem/CustomEditor自动调用，不依赖显式new或场景引用 |
| AttackSequence与AbsoluteAttackSchedule | 前者是相对冷却的单技能测试，后者是绝对时刻的正式编排；语义不同 |
| Trace、InkBurn、InkSlow的TraceBullet | 共用一个实现，通过预制体配置区分；已经在复用 |
| PatternBullet用于Fan和Impact碎屑 | 复用展开/飞行/命中；碎屑关闭准备阶段，不新增重复实现 |
| LaserGeometry与GroundAreaGeometry | 几何目标分别是激光裁剪和地面区域重叠扣除，不强行合并 |
| TraceBullet/ImpactBullet的无素材回退圈 | 素材缺失或VFX禁用时仍需反馈，不当作废弃动画删除 |
| 旧Track迁移、FormerlySerializedAs、血条兼容 | 负责读取旧资产，删除会影响现有序列化数据 |
| 原始/CrispPreview/Ink1/Ink2预制体 | 用于旧关卡和效果对比；不是字节相同的重复资源，保留用户参数 |
| 大型Timeline控制器/窗口和点位编辑器 | 当前职责较多，但本次不改状态机或深拆序列化嵌套类；先整理物理目录和独立纯计算类型 |

自有脚本中没有发现可直接删除的整份弃用脚本。没有为凑清理数量删除Inspector组件或兼容代码。

## 验证结果

- 原45份脚本meta逐字节一致；GUID无重复，50份脚本文件名与主类型名匹配。
- 除7个已归档重复资源文件外，492份原有非脚本资产/ProjectSettings文件的哈希保持一致，包括场景、预制体、动画、切片meta和攻击参数。
- 自有脚本引用全部解析；另16个外部脚本GUID在本地Unity PackageCache中解析成功，无新增Missing Script。
- 运行程序集和Editor程序集分别编译通过；未写入Unity实际运行的Library DLL。
- 668项离线回归断言通过：规则图集/不等距切片UV、行切换混帧、淡出、Laser持续循环、绝对时间跳过、区间索引以及P3跨边界连发。
- 三处Sprite外观复制与原九条赋值一致；非VFX的游戏逻辑除类型重命名、独立工具拆出及删除未使用实参外，逐文件内容比对一致。
- 未进行Unity Play Mode画面验收；仍需在编辑器中确认重导入后Console与各类攻击显示。

本次临时快照、移动清单和验证输出在Temp/CodeOrganization与Temp/CodeMaintenance。重复资源的可恢复归档在Archive中，独立于Unity的Temp目录。

## 后续怎么放文件

查[脚本维护说明](ScriptGuide.md)定位入口。继续使用原场景/预制体目录，不必重新挂组件。改效果先找Runtime/VFX，改速度和伤害找Runtime/Attacks，改关卡时刻找Runtime/Level，改编辑器操作找Editor。
