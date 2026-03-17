// using Rhino;
// using Rhino.Commands;
// using Rhino.DocObjects;
// using Rhino.Geometry;
// using Rhino.Input;
// using Rhino.Input.Custom;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;

// namespace MyChangeTools.commands.RbfDeformGui
// {
//     internal class Config
//     {
//         //是否复制对象
//         public bool IsCopy { get; set; }
//         //是否使用自定义的变形类MyGeomMorph
//         public bool UseCustomMorph { get; set; }
//         //基准对象
//         public ObjRef[] baseObjRfs { get; set; }
//         //目标对象
//         public ObjRef[] targetObjRfs { get; set; }
//         //受限对象
//         public ObjRef[] limitedObjRfs { get; set; }
//         //最终移动的限制方向
//         public List<Vector3d> MoveVectors { get; set; }

//         public SpaceMorphConfig SpaceMorphConfig { get; set; }

//         public MyGeomMorphConfig MyGeomMorphConfig { get; set; }

//         public SampleConfig SampleConfig { get; set; }

//         public RBFConfig RBFConfig { get; set; }
//         //容差
//         public double Tolerance { get; set; }


//     }
//     internal class SpaceMorphConfig
//     {
        
//         //是否保持结构
//         public bool PreserveStructure { get; set; }
//         //是否快速预览的变形
//         public bool QuickPreview { get; set; }
//     }
//     internal class MyGeomMorphConfig
//     {
//         //收缩曲面控制点到边界
//         public int ShrinkSurfaceToEdge { get; set; }
//         //重建曲面U方向控制点数量，0代表不重建
//         public int RebuildFaceUCount { get; set; }
//         //重建曲面V方向控制点数量，0代表不重建
//         public int RebuildFaceVCount { get; set; }
//         //重建曲线控制点数量，0代表不重建
//         public int RebuildCurveCount { get; set; }
//     }
//     internal class SampleConfig
//     {
//         //基准和目标曲线采样点以参数匹配
//         public bool CurveSampleByParameter { get; set; }
//         //网格点以网格坐标系匹配
//         public bool MatchMeshByCoordinate { get; set; }
//         //在曲线上的采样点距离
//         public double CurveSampleDistance { get; set; }
//     }
//     internal class RBFConfig
//     {
//         //RBF是否添加线性系统
//         public bool RBFAddLinearSystem { get; set; }
//         //影响范围半径，0代表不限制影响范围
//         public double InfectRadius { get; set; }
//         //径向基函数ID，0代表默认的Multiquadric
//         public int PhiFunctionID { get; set; }
//         //RBF计算的维度
//         public bool ComputeX { get; set; }
//         public bool ComputeY { get; set; }
//         public bool ComputeZ { get; set; }
//     }
// }