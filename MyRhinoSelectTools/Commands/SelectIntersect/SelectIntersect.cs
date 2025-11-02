using Rhino;
using Rhino.Commands;

namespace MyRhinoSelectTools.Commands.SelectIntersect
{
    public class SelectIntersect : Command
    {
        public override string EnglishName => "SelectIntersect";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // 选择基准对象// get baseobjs
            var baseObjs = SelectionHelper.GetBaseObjects();
            if (baseObjs == null || baseObjs.Count == 0)
                return Result.Cancel;

            // 选择目标类型 // get targettype 并存在targettype中
            if (!SelectionHelper.GetTargetObjectTypeFromUser(doc, out var targetType))
                return Result.Cancel;

            // 选择计算对象
            var computeObjs = SelectionHelper.GetComputeObjects(doc, targetType, baseObjs);
            if (computeObjs == null || computeObjs.Count == 0)
                return Result.Cancel;

            // 计算相交
            var intersected = GeometryUtils.ComputeIntersections(doc, baseObjs, computeObjs);

            // 输出结果
            SelectionHelper.HighlightAndReport(doc, intersected);

            return Result.Success;
        }
    }
}
