using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace MyChangeTools.commands
{
    public class CmdOffsetPointsOnCurve : Command
    {
        public override string EnglishName => "OffsetPointsOnCurve";

        static double offsetdist = 0;
        static bool deleteInput = true;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 1️⃣ 选择曲线
            ObjRef crvRef;
            var rc = RhinoGet.GetOneObject("选择曲线", false, ObjectType.Curve, out crvRef);
            if (rc != Result.Success) return Result.Failure;

            NurbsCurve curve = crvRef.Curve().ToNurbsCurve();
            curve.Domain = new Interval(0, 100);
            if (curve == null) return Result.Failure;
            // 计算前后弧长
            double totalLength = curve.GetLength();

            // 2️⃣ 选择多个点
            GetObject go = new GetObject();
            go.SetCommandPrompt("选择点");
            go.GeometryFilter = ObjectType.Point;
            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return Result.Failure;

            // 3️⃣ 输入偏移距离
            
            rc = RhinoGet.GetNumber("输入沿曲线偏移距离", false, ref offsetdist);
            if (rc != Result.Success || offsetdist <= 0)
                return Result.Failure;
            
            rc = RhinoGet.GetBool("是否删除原始点", true, "NO", "YES", ref deleteInput);
            if (rc != Result.Success)
                return Result.Failure;


            // 4️⃣ 处理每个点
            for (int i = 0; i < go.ObjectCount; i++)
            {
                var ptf = go.Object(i);
                var ptObj = ptf.Point();
                if (ptObj == null) continue;

                var ptrhinoobj = ptf.Object();
                var attr = ptrhinoobj.Attributes.Duplicate();

                Point3d pt = ptObj.Location;

                // 找到曲线上最近参数
                if (!curve.ClosestPoint(pt, out double t))
                    continue;

                // 获取当前点在曲线上的弧长位置
                double lengthAtT;
                lengthAtT = curve.GetLength(new Interval(0, t));

                double forwardLength = lengthAtT + offsetdist;
                double backwardLength = lengthAtT - offsetdist;

                // clamp 防止越界
                if (forwardLength > totalLength) continue;
                if (backwardLength < 0) continue;

                // 弧长转参数
                if (!curve.LengthParameter(forwardLength, out double tForward))
                    continue;

                if (!curve.LengthParameter(backwardLength, out double tBackward))
                    continue;

                // 取点
                Point3d ptForward = curve.PointAt(tForward);
                Point3d ptBackward = curve.PointAt(tBackward);

                // 加入文档
                doc.Objects.AddPoint(ptForward, attr);
                doc.Objects.AddPoint(ptBackward, attr);

                if (deleteInput)
                {
                    doc.Objects.Delete(ptf, true);
                }
            }

            doc.Views.Redraw();
            return Result.Success;
        }
    }
}