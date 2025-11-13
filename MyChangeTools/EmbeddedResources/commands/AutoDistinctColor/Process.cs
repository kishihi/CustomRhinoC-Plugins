using Rhino;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MyChangeTools.commands.AutoDistinctColor
{
    internal class Process
    {
        // -------------------------
        // 从对象获得最终显示颜色
        // -------------------------
        private static Color GetObjectDisplayColor(RhinoObject obj)
        {
            var attr = obj.Attributes;
            try
            {
                switch (attr.ColorSource)
                {
                    case ObjectColorSource.ColorFromObject:
                        return attr.ObjectColor;

                    case ObjectColorSource.ColorFromLayer:
                        var li = attr.LayerIndex;
                        if (li >= 0 && li < RhinoDoc.ActiveDoc.Layers.Count)
                            return RhinoDoc.ActiveDoc.Layers[li].Color;
                        break;

                    case ObjectColorSource.ColorFromMaterial:
                        int mi = attr.MaterialIndex;
                        if (mi >= 0 && mi < RhinoDoc.ActiveDoc.Materials.Count)
                            return RhinoDoc.ActiveDoc.Materials[mi].DiffuseColor;
                        break;
                }
            }
            catch { }

            return Color.Gray;
        }

        // Color -> (r,g,b)
        private static  (double, double, double) RGB2Vec(Color c)
        {
            return (c.R / 255.0, c.G / 255.0, c.B / 255.0);
        }

        // 欧氏距离
        private static double ColorDist((double r, double g, double b) c1, (double r, double g, double b) c2)
        {
            double dr = c1.r - c2.r;
            double dg = c1.g - c2.g;
            double db = c1.b - c2.b;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        // HSV -> Color
        private static Color HsvToColor(double h, double s, double v)
        {
            if (s == 0)
            {
                int g = (int)(v * 255);
                return Color.FromArgb(g, g, g);
            }

            double hh = h / 60.0;
            int i = (int)Math.Floor(hh) % 6;
            double f = hh - Math.Floor(hh);

            double p = v * (1.0 - s);
            double q = v * (1.0 - s * f);
            double t = v * (1.0 - s * (1.0 - f));

            double r, g2, b;

            switch (i)
            {
                case 0: r = v; g2 = t; b = p; break;
                case 1: r = q; g2 = v; b = p; break;
                case 2: r = p; g2 = v; b = t; break;
                case 3: r = p; g2 = q; b = v; break;
                case 4: r = t; g2 = p; b = v; break;
                default: r = v; g2 = p; b = q; break;
            }

            return Color.FromArgb(
                (int)(255 * r),
                (int)(255 * g2),
                (int)(255 * b)
            );
        }

        // 获取需排除的系统颜色（选中对象/锁定对象颜色）
        private static List<(double, double, double)> GetExcludedColors()
        {
            var list = new List<(double, double, double)>();
            var selColor = Rhino.ApplicationSettings.AppearanceSettings.SelectedObjectColor;
            var lockColor = Rhino.ApplicationSettings.AppearanceSettings.LockedObjectColor;

            list.Add(RGB2Vec(selColor));
            list.Add(RGB2Vec(lockColor));
            return list;
        }

        private static List<RhinoObject> GetAllDocObjects(RhinoDoc doc)
        {
            var es = new ObjectEnumeratorSettings
            {
                HiddenObjects = true,
                LockedObjects = true,
                IncludeLights = false
            };

            // 一次性 ToList，避免多次枚举
            return doc.Objects.GetObjectList(es).ToList();
        }

        private static List<Color> GenerateCandidateColors1()
        {
            var candidates = new List<Color>();
            for (int h = 0; h < 360; h += 30)
            {
                foreach (double s in new[] { 1.0, 0.95, 0.85 })
                {
                    foreach (double v in new[] { 1.0, 0.9 })
                    {
                        candidates.Add(HsvToColor(h, s, v));
                    }
                }
            }
            return candidates;
        }

        private static List<Color> GenerateCandidateColors(int count = 60)
        {
            // 黄金角（度）
            const double goldenAngle = 137.508;

            var list = new List<Color>();
            double h = 0.0;

            for (int i = 0; i < count; i++)
            {
                h = i * goldenAngle % 360.0;

                // 固定高饱和高明度，非常醒目且差异明显
                double s = 0.90;
                double v = 0.95;

                list.Add(HsvToColor(h, s, v));
            }

            return list;
        }


        public static void AutoDistinctColor(RhinoDoc doc, List<RhinoObject> selObjs)
        {
            // 收集文档中现有颜色
             var otherColors = new List<(double, double, double)>();

            otherColors.AddRange(GetExcludedColors());

            var selIds = new HashSet<Guid>();

            foreach (var o in selObjs) selIds.Add(o.Id);

            var allObjs = GetAllDocObjects(doc);

            RhinoApp.WriteLine($"当前文档共 {allObjs.Count} 个对象");

            foreach (var o in allObjs)
            {
                if (!selIds.Contains(o.Id))
                    otherColors.Add(RGB2Vec(GetObjectDisplayColor(o)));
            }


            if (otherColors.Count == 0)
                otherColors.Add(RGB2Vec(Color.Gray));

            // 生成候选 HSV 色板
            var candidates = GenerateCandidateColors();
            

            Color bestColor = Color.White;
            double bestScore = double.MinValue;

            foreach (var c in candidates)
            {
                var cv = RGB2Vec(c);
                double minDist = double.MaxValue;
                foreach (var oc in otherColors)
                    minDist = Math.Min(minDist, ColorDist(cv, oc));

                if (minDist > bestScore)
                {
                    bestScore = minDist;
                    bestColor = c;
                }
            }

            // 应用颜色到选中对象
            foreach (var o in selObjs)
            {
                var attr = o.Attributes;
                attr.ObjectColor = bestColor;
                attr.ColorSource = ObjectColorSource.ColorFromObject;
                o.CommitChanges();
            }

            RhinoApp.WriteLine($"已应用异色：RGB({bestColor.R},{bestColor.G},{bestColor.B}), 最小距离 {bestScore:0.000}");
        }


    }
}
