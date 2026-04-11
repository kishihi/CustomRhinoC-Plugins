## Reference

MathNet.Numerics

## Some Custom Rhino C# Plugins for Rhino 7+

测试中

编译后的rhp位于 xx\bin

直接拖入Rhino使用

### MyRhinoSelecTools 

准备对rhino的选择工具做点拓展

1. SelectIntersect 选择相交的物体 (brep / point / mesh / subd / curve ) intersect (brep / point / mesh / subd / curve )
2. SelectAboveSurface 选择在曲面 above / below / on 的物体 (基于物体质心)
3. SelectAboveSurface2 选择在曲面 above / below / on 的物体 (基于网格点数)
4. SelectSimilarCurve 选择相似曲线 (similiar threshold , unseprojection in PlaneXZ/XY/ZY), 待改进..


### MyChangeTools 

准备做一些变动变形工具

1. ProjectFlowEx2 将物体基于一个向量方向从曲面变换到另一个曲面,待改进..
2. ExtendCurves 批量延伸曲线 , 保持物体原来属性
3. AutoDistinctColor 自动异色物体,自动赋予物体一个与当前文档中显示和隐藏的物体和锁定的物体颜色不同的物件颜色
4. ProjectAlowViewZ 沿着视口(摄像头)Z方向投影曲线到曲面上(想做一个可见即可得的投影的,发现Rhino自带)
5. DualSurfaceMapping 将在面A1和面B1的物体,沿着投影方向变换到面A2和B2之间,待改进..
6. RbfDeform 选择多组基准曲线和目标曲线,可选限制线. 基于RBF把多个物体平滑变换过去.待改进..
7. FlowAlongMesh 网格面数和顶点数相同的两个网格, 能把物体从一个网格流动到另一个网格.待改进..
8. RemoveCurveOvrelap 去除多条曲线之间的以及自相交重叠线段部分.待改进..
9. OffsetPointsOnCurve 左右偏移曲线上面的点
