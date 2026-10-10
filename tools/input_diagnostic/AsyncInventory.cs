using System.Threading.Tasks;

namespace InputDiagnostic {
    public static class AsyncInventory {
        public static async Task<Inventory> Bound(Task<Inventory> work,int timeoutMs) {
            if(await Task.WhenAny(work,Task.Delay(timeoutMs))!=work) {
                var result=new Inventory();result.errors.Add("Windows enumeration exceeded its time limit. Live XInput testing remains available; full inventory is unavailable in this run.");return result;
            }
            return await work;
        }
    }
}
