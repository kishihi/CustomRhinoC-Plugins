using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Input;
using Rhino.Input.Custom;
using System.Linq;

namespace MyChangeTools.commands.RbfDeformGui
{
    public class Selection
    {
        public static Result SelectGeometries(
    RhinoDoc doc,
    string prompt,
    ObjectType filter,
    out ObjRef[] objRefs)
        {
            objRefs = null;

            var go = new GetObject();
            go.SetCommandPrompt(string.IsNullOrEmpty(prompt) ? "选择几何体" : prompt);

            go.EnablePreSelect(false, true);

            go.GroupSelect = true;

            go.GeometryFilter = filter;

            //关闭选择子物件
            go.SubObjectSelect = false;

            go.GetMultiple(1, 0);

            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            objRefs = go.Objects().ToArray();

            objRefs = objRefs
                .Where(o => o != null && o.Object() != null && o.Object().IsValid)
                .ToArray();

            var typeCounts = objRefs.GroupBy(o => o.Object().ObjectType).Select(g => new { Type = g.Key, Count = g.Count() });
            foreach (var typeCount in typeCounts)
            {
                RhinoApp.WriteLine($"You Selected {typeCount.Count} {typeCount.Type.ToString()}");
            }

            if (objRefs.Length == 0)
                return Result.Failure;

            doc.Objects.UnselectAll();

            return Result.Success;
        }


        public static Result SelectOneGeom(RhinoDoc doc, string prompt, out ObjRef objRef, ObjectType type)
        {
            objRef = null;
            var rc = RhinoGet.GetOneObject(prompt, false, type, out objRef);
            if (rc != Result.Success) return rc;
            doc.Objects.UnselectAll();
            return Result.Success;
        }

    }

}
