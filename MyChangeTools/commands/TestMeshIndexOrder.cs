using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using System;
using System.Linq;
using Rhino.DocObjects;

namespace MyChangeTools.commands
{
    public class TestMeshIndexOrder : Command
    {
        public TestMeshIndexOrder()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static TestMeshIndexOrder Instance { get; private set; }

        public override string EnglishName => "TestMeshIndexOrder";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            
            var go = new Rhino.Input.Custom.GetObject();
            go.GroupSelect = true;
            go.SubObjectSelect = false;
            go.EnablePreSelect(true, true);
            go.SetCommandPrompt("select been evaluted Meshs");
            go.GeometryFilter = Rhino.DocObjects.ObjectType.Mesh;
            var res = go.GetMultiple(1, 0);
            if (res != Rhino.Input.GetResult.Object) return Result.Failure;
            var rfs = go.Objects().Where(o => o != null && o.Object() != null).ToList();
            int vertexcount = rfs[0].Mesh().Vertices.Count();

            doc.Objects.UnselectAll();

            var prompt = "input base mesh";
            var rc = RhinoGet.GetOneObject(
                prompt, false, Rhino.DocObjects.ObjectType.Mesh,
                out ObjRef objRef);
            if (rc != Result.Success) return Result.Failure;

            Mesh baseMesh = objRef.Mesh();


            //base mesh
            for (int i = 1; i < vertexcount; i++)
            {
                var basemvp = baseMesh.Vertices[i];
                var textdot1 = new TextDot($"{i}", basemvp);
                doc.Objects.Add(textdot1);
            }

            //other
            foreach (var mf in rfs)
            {
                var m = mf.Mesh();
                for (int i = 1; i < vertexcount; i++)
                {
                    var basemvp = baseMesh.Vertices[i];
                    MeshPoint basemp = baseMesh.ClosestMeshPoint(basemvp, 0.0);
                    Point3d q2 = m.PointAt(basemp);
                    var textdot1 = new TextDot($"{i}", q2);
                    doc.Objects.Add(textdot1);
                }
            }

            doc.Views.Redraw();

            return Result.Success;
        }
    }
}