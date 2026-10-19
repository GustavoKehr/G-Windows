using System.Drawing;
using System.Windows.Forms;

namespace GWindows;

/// <summary>Tray menu in the fence palette (WinForms cannot round the corners without hacks; colors only).</summary>
sealed class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new Colors()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Color.White; base.OnRenderItemText(e); }

    sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(24, 26, 32);
        public override Color ImageMarginGradientBegin => Color.FromArgb(24, 26, 32);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(24, 26, 32);
        public override Color ImageMarginGradientEnd => Color.FromArgb(24, 26, 32);
        public override Color MenuBorder => Color.FromArgb(64, 255, 255, 255);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.FromArgb(52, 56, 66);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(52, 56, 66);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(52, 56, 66);
        public override Color SeparatorDark => Color.FromArgb(70, 255, 255, 255);
        public override Color CheckBackground => Color.FromArgb(70, 110, 200);
    }
}