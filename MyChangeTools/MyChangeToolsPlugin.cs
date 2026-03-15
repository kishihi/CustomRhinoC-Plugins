using System;
using Rhino;
using Rhino.UI;

namespace MyChangeTools
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class MyChangeToolsPlugin : Rhino.PlugIns.PlugIn
    {
        public MyChangeToolsPlugin()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the MyChangeToolsPlugin plug-in.</summary>
        public static MyChangeToolsPlugin Instance { get; private set; }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.

        // protected override Rhino.PlugIns.LoadReturnCode OnLoad(ref string errorMessage)
        // {

        //     //注册 Panel 必须在 PlugIn 的 OnLoad 里注册
        //     // 注册 Dock Panel
        //     Panels.RegisterPanel(this, typeof(HelloPlugin.HelloPanel), "HelloPanel", null);
        //     // Panels.RegisterPanel(this, typeof(MyPanel1), "工具 1", null);
        //     // Panels.RegisterPanel(this, typeof(MyPanel2), "工具 2", null);
        //     // Panels.RegisterPanel(this, typeof<MyPanel3>, "工具 3", null);
        //     return Rhino.PlugIns.LoadReturnCode.Success;
        // }


    }
}