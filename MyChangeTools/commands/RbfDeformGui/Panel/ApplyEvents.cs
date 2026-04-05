using Eto.Drawing;
using Eto.Forms;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace MyChangeTools.commands.RbfDeformGui
{
    public partial class RbfDeformPanel : Panel
    {
        Button applyButton;
        Button undoButton;
        CancellationTokenSource _cts;

        Config Config => ConfigManager.Current;

        Control CreateProcessButtons()
        {
            applyButton = new Button
            {
                Text = "Apply",
                BackgroundColor = Colors.LightGreen,
                TextColor = Colors.Black,
                Font = new Eto.Drawing.Font(SystemFont.Bold)
            };

            undoButton = new Button
            {
                Text = "Undo",
                BackgroundColor = Colors.IndianRed,
                TextColor = Colors.White,
                Font = new Eto.Drawing.Font(SystemFont.Bold)
            };
            applyButton.Click += (s, e) => RunDeform();
            undoButton.Enabled = false; // 初始不可用
            undoButton.Click += (s, e) => CancelDeform();
            applyButton.Width = 80;
            undoButton.Width = 80;
            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = {
                    null,
                    applyButton,
                    undoButton,
                    null
                }
            };
        }


        void CancelDeform()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel(); // 通知后台线程停止
            }
        }

        void CheckRfs(out bool checkOk)
        {
            checkOk = false;
            if (Config.ObjRfs.Length == 0)
            {
                MessageBox.Show($"Please select deform objs");
                return;
            }
            foreach (var objf in Config.ObjRfs)
            {

                var geo = objf.Geometry();
                if (geo == null)
                {
                    MessageBox.Show($"Please select vaild deform objs");
                    return;
                }
            }

            foreach (var objf in Config.BaseObjRfs)
            {
                var geo = objf.Geometry();
                if (geo == null)
                {
                    MessageBox.Show($"Please select vaild base objs");
                    return;
                }
            }

            foreach (var objf in Config.TargetObjRfs)
            {
                var geo = objf.Geometry();
                if (geo == null)
                {
                    MessageBox.Show($"Please select vaild target objs");
                    return;
                }
            }

            foreach (var objf in Config.LimitedObjRfs)
            {
                var geo = objf.Geometry();
                if (geo == null)
                {
                    MessageBox.Show($"Please select vaild limit objs");
                    return;
                }
            }
            checkOk = true;
        }

        void RunDeform()
        {
            applyButton.Enabled = false;
            undoButton.Enabled = true;
            
            Config.ObjRfs = _objRfs.Select(f => new ObjRef(f.ObjectId)).ToArray();
            Config.BaseObjRfs = _baseRfs.Select(f => new ObjRef(f.ObjectId)).ToArray();
            Config.TargetObjRfs = _targetRfs.Select(f => new ObjRef(f.ObjectId)).ToArray();
            Config.LimitedObjRfs= _limitedRfs.Select(f => new ObjRef(f.ObjectId)).ToArray();

            CheckRfs(out bool checkok);
            if (!checkok) {
                //重新启用 if errors
                applyButton.Enabled = true;  // 处理完成后重新启用
                undoButton.Enabled = false;
                return;
            };

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() =>
            {
                var processor = new GeometryProcessor(
                    Config.Doc,
                    Config.ObjRfs,
                    Config.BaseObjRfs,
                    Config.TargetObjRfs,
                    Config.LimitedObjRfs,
                    Config.MoveVectors,
                    Config);
                return processor.Process(token);
            }).ContinueWith(task =>
            {
                // UI线程
                applyButton.Enabled = true;  // 处理完成后重新启用
                undoButton.Enabled = false;
                if (task.IsFaulted)
                {
                    MessageBox.Show($"错误：{task.Exception?.InnerException?.Message}");
                    return;
                }
                else if (task.IsCanceled)
                {
                    MessageBox.Show("操作已取消");
                    return;
                }
                else
                {  
                    ApplyResultToDoc(task.Result);
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void ApplyResultToDoc((List<MorphedGeom> successProcessObjs, List<ObjRef> failProcessObjRefs) result)
        {
            var successProcessObjs = result.successProcessObjs;
            var failProcessObjRefs = result.failProcessObjRefs;

            List<Guid> newIds = new List<Guid>(successProcessObjs.Count);

            // ================= UI线程 =================
            uint undo = Config.Doc.BeginUndoRecord("RbfDeform");

            try
            {
                // 添加新对象
                foreach (var mg in successProcessObjs)
                {
                    foreach (var g in mg.GeometryBases)
                    {
                        Guid id = Config.Doc.Objects.Add(g, mg.Attributes.Duplicate());

                        if (id != Guid.Empty)
                            newIds.Add(id);
                    }
                }

                // 选中新对象
                foreach (Guid id in newIds)
                    Config.Doc.Objects.Select(id);

                // 删除旧对象（如果不是复制模式）
                if (!Config.IsCopy)
                {
                    HashSet<Guid> failSet = new HashSet<Guid>(failProcessObjRefs.Select(f => f.ObjectId));

                    foreach (var ob in Config.ObjRfs)
                    {
                        if (!failSet.Contains(ob.ObjectId))
                            Config.Doc.Objects.Delete(ob.ObjectId, true);
                    }
                }

                // 保留失败对象选中
                foreach (var fo in failProcessObjRefs)
                    Config.Doc.Objects.Select(fo.ObjectId);
            }
            finally
            {
                Config.Doc.EndUndoRecord(undo);
            }

            Config.Doc.Views.Redraw();
        }



    }
}