public class DoStuff{
	
	private static readonly object lockObject = new object();
	private static Dictionary<string, Task> resourceTasks = new Dictionary<string, Task>();
	
	public void DoThreadSafeStuff(string resourceId){
		lock (lockObject)
		{
			var _tokenSource = new CancellationTokenSource();

			var _task = Task.Factory.StartNew(() =>
			{
				
			}, _tokenSource.Token)
			.ContinueWith(t =>
			 {
				 if (t.Exception != null)
				 {
					 foreach (var ex in t.Exception.InnerExceptions)
					 {
						 // Log exceptions
					 }
				 }
			 });
			 
			 // If old resouce task is still running, cancel new one
			 if (resourceTasks.ContainsKey(resourceId))
			 	_tokenSource.Cancel();
			 else
			 	resourceTasks.Add(resourceId, _task);
		}
	}
}