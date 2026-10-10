namespace X20Ctl.Simulation;
public sealed class PointerMode
{
    private double anchorX, anchorY;
    private bool active;
    public void EnterDemo(double x, double y) { anchorX = x; anchorY = y; active = true; }
    public bool Observe(double x, double y)
    {
        if (!active || Math.Pow(x - anchorX, 2) + Math.Pow(y - anchorY, 2) <= 36) return false;
        active = false; return true;
    }
}
