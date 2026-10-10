using X20Ctl.Desktop.ViewModels;
namespace X20Ctl.Desktop.Views;
public interface IInputWorkspace
{
    WorkspaceModel Model { get; }
    void CycleInspector(int direction);
}
