using System.IO;
using System.Windows;
using System.Windows.Controls;
using X20Ctl.Product;
using X20Ctl.Zone;

internal static class SetupIsolationChecks
{
    public static void Run()
    {
        string directory=Path.Combine(Path.GetTempPath(),"x20ctl-model-isolation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string legacy=Path.Combine(directory,"mixed.json");File.WriteAllText(legacy,"[{\"id\":\"x20\",\"name\":\"X20 only\",\"controllerId\":\"x20\"},{\"id\":\"pro\",\"name\":\"Pro only\",\"controllerId\":\"x20_pro\"}]");
        var store=new SetupStore(Path.Combine(directory,"library.json"));store.Load();store.CopyLegacy(legacy);
        var native=store.Create("Native X20",SetupSnapshot.Capture("x20",new("x20"),new("x20"),new("x20"),new("x20")));
        string nativeFile=Path.Combine(directory,"x20.json");File.WriteAllText(nativeFile,store.Export(native.Id));
        var catalog=new ControllerCatalog(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data/catalog.json")));
        var owner=new Grid();owner.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("pack://application:,,,/X20Ctl;component/Themes/StudioConsole.xaml")});var pro=new SetupsWorkbench(store,catalog,"x20_pro",owner,_=>{},()=>throw new Exception("Unexpected capture"),_=>throw new Exception("Unexpected load"),[],native.Id);
        if(pro.SetupCards.Count!=1 || pro.ActiveId!=null || store.Find(pro.SelectedId!).ModelId!="x20_pro")throw new Exception("Pro library leaks X20 setups");
        void Reject(Action action){try{action();}catch(Exception ex)when(ex is InvalidOperationException or InvalidDataException){return;}throw new Exception("Cross-model operation accepted");}
        Reject(()=>pro.Select(native.Id));byte[] before=File.ReadAllBytes(store.PathName);Reject(()=>pro.ImportPath(nativeFile));Reject(()=>pro.ImportPath(legacy));
        if(!before.SequenceEqual(File.ReadAllBytes(store.PathName)))throw new Exception("Rejected import modified library");
        var x20=new SetupsWorkbench(store,catalog,"x20",owner,_=>{},()=>SetupSnapshot.Capture("x20",new("x20"),new("x20"),new("x20"),new("x20")),_=>{},[],pro.SelectedId);
        if(x20.SetupCards.Count!=2 || x20.ActiveId!=null || store.Find(x20.SelectedId!).ModelId!="x20")throw new Exception("X20 library leaks Pro setups");
        string proFile=Path.Combine(directory,"pro.json");File.WriteAllText(proFile,"{\"name\":\"Foreign Pro\",\"controllerId\":\"x20_pro\"}");Reject(()=>x20.ImportPath(proFile));
        string matching=Path.Combine(directory,"matching.json");File.WriteAllText(matching,"{\"id\":\"new-pro\",\"name\":\"Own Pro\",\"controllerId\":\"x20_pro\"}");pro.ImportPath(matching);
        if(pro.SetupCards.Count!=2 || store.Items.Count!=4)throw new Exception("Matching import lost or changed other models");
        Console.WriteLine("PASS: X20 and X20 Pro libraries isolated in both directions; stale active selection cleared; foreign native/legacy imports rejected without modifying library; matching import works; existing entries preserved.");
    }
}
