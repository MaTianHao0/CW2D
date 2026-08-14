using System;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace TitleBlockBattery
{
    /// <summary>
    /// 在 Grasshopper 顶部菜单添加：Title Block ▸ History Presets...
    /// </summary>
    public class TitleBlockMenu : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            // 注意：CanvasCreated 的委托只有 1 个参数（GH_Canvas）
            Instances.CanvasCreated += OnCanvasCreated;
            return GH_LoadingInstruction.Proceed;
        }

        private void OnCanvasCreated(GH_Canvas canvas)
        {
            try
            {
                var editor = Instances.DocumentEditor;
                if (editor == null || editor.MainMenuStrip == null) return;

                // 获取或创建“Title Block”根菜单
                ToolStripMenuItem root = null;
                foreach (ToolStripMenuItem item in editor.MainMenuStrip.Items)
                {
                    if (item.Text == "Title Block")
                    {
                        root = item;
                        break;
                    }
                }
                if (root == null)
                {
                    root = new ToolStripMenuItem("Title Block");
                    editor.MainMenuStrip.Items.Add(root);
                }

                // 避免重复添加“History Presets...”
                foreach (ToolStripItem sub in root.DropDownItems)
                {
                    if (sub is ToolStripMenuItem t && t.Text == "History Presets...")
                        return; // 已存在则直接返回
                }

                var miHistory = new ToolStripMenuItem("History Presets...");
                miHistory.Click += (sender, args) =>
                {
                    try
                    {
                        var mgr = new TitleBlockManager();
                        using (var dlg = new TitleBlockHistoryForm(mgr))
                        {
                            dlg.ShowDialog(editor);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Open history presets failed:\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                root.DropDownItems.Add(miHistory);
            }
            catch (Exception ex)
            {
                // 不抛出，避免影响 GH 启动
                MessageBox.Show($"Failed to create menu:\n{ex.Message}", "Title Block",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
