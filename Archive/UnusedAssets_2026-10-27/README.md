# 未引用的重复资源

SparkBurst的PNG、JSON和license与Assets/_Project/Art/VFX/SparkSheet中的对应文件逐字节一致，原资源GUID没有引用，因此移出Unity的Assets导入目录。保留原文件和meta，便于恢复。

如确需恢复，将SparkBurst文件夹和SparkBurst.meta一起复制回Assets/_Project/Art/VFX。日常继续使用SparkSheet，不需要恢复重复导入。
