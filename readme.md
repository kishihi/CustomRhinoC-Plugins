## Some Custom Rhino C# Plugins for Rhino 7+

编译后的rhp位于 xx\bin\Release\netxx\ 

直接拖入Rhino使用

### MyRhinoSelecTools 

准备对rhino的选择工具做点拓展

1. SelectIntersect 选择相交的物体 (brep / point / mesh / subd / curve ) intersect (brep / point / mesh / subd / curve )
2. SelectAboveSurface 选择在曲面 above / below / on 的物体
3. SelectSimilarCurve 选择相似曲线 (similiar threshold , unseprojection in PlaneXZ/XY/ZY)


### MyChangeTools 

准备做一些变动变形工具

1. ProjectFlowEx2 将物体基于一个向量方向从曲面变换到另一个曲面
2. ExtendCurves 批量延伸曲线
3. AutoDistinctColor 自动异色物体,自动赋予物体一个与当前文档中隐藏的物体和锁定的物体颜色不同的物件颜色
4. ProjectAlowViewZ 沿着视口(摄像头)Z方向投影曲线到曲面上(想做一个可见即可得的投影的)
