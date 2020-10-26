<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Threading.Tasks.Parallel.dll</Reference>
  <NuGetReference>System.Threading.Tasks.Dataflow</NuGetReference>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Threading.Tasks.Dataflow</Namespace>
</Query>

/*
****Parallel Programming****
Many personal computers and workstations have multiple CPU cores that enable multiple threads to be executed simultaneously. 
To take advantage of the hardware, you can parallelize your code to distribute work across multiple processors.
.NET provides the Task Prallel Library (TPL) to simplify parallel programming. TPL is a set of public types and APIs in the System.Threading and System.Threading.Tasks namespaces
Source: https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/


****Asynchronous Programming****
Async programming is a key technique that makes it straightforward to handle blocking I/O and concurrent operations on multiple cores.
Async code has the following characteristics:
	-Handles more server requests by yielding threads to handle more requests while waiting for I/O requests to return.
    -Enables UIs to be more responsive by yielding threads to UI interaction while waiting for I/O requests and by transitioning long-running work to other CPU cores.
Source: https://docs.microsoft.com/en-us/dotnet/standard/async

****TPL Data parallelism****
Data parallelism refers to scenarios in which the same operation is performed concurrently (that is, in parallel) on elements in a source collection or array.
E.g. using Parallel.ForEach(....)


****TPL Dataflow****
TPL provides dataflow components to help increase the robustness of concurrency-enabled applications.
These dataflow components are collectively referred to as the TPL Dataflow Library. 
This dataflow model promotes actor-based programming by providing in-process message passing for coarse-grained dataflow and pipelining tasks
Source:https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library

When to use TPL Dataflow vs Asynchronous Concurrency (E.g. Tasks.WaitAll()).
-Use TPL when pipelining is needed. I.e. You need to pass a 'message' or data from one independent component to another.
-Use Asynchronous concurrency for onetime async calls/operations
*** In short, use asynchronous concurrency for the simplest of tasks than need concurrent execution


***Predefined TPL Dataflow Block Types****
1). Buffering Blocks: These blocks hold data for use by data consumers.
		-BufferBlock<T> (E.g. var bufferBlock = new BufferBlock<int>())
		-BroadcastBlock<T> (E.g. var broadcastBlock = new BroadcastBlock<double>(null))
		-WriteOnceBlock<T> (var writeOnceBlock = new WriteOnceBlock<string>(null))
		
2). Execution Blocks: These blocks call a user-provided delegate for each piece of received data.
		-ActionBlock<T> (E.g. var actionBlock = new ActionBlock<int>(n => // Do something with data n))). See code example below of ActionBlock<T>.
		-TransformBlock<TInput, TOutput) (E.g. var transformBlock = new TransformBlock<int, double>(n => Math.Sqrt(n)))
		-TransformManyBlock(TInput, TOutput)
		
3). Grouping Blocks: These blocks combine data from one or more sources and under various constraints.
		-BatchBlock<T>
		-JoinBlock<T1, T2, ...>
		-BatchedJoinBlock<T1, T2, ...>

*/


// Example of TPL ActionBlock
// Import: System.Threading.Tasks, System.Threading.Tasks.Dataflow
async Task Main()
{
	var actionBlock = new ActionBlock<int>(
	   async d =>
	   {
		   await Task.Delay(TimeSpan.FromSeconds(5));

		   Console.WriteLine($"Processing item {d}");

		   // DO busy work here
	   },
	   // Set degree of parallelism (MaxDegreeOfParallelism) and size of buffered messages (BoundedCapacity). 
	   new ExecutionDataflowBlockOptions
	   {
		   BoundedCapacity = 1000, // Buffer upto 1000 messages before starting processing
		   MaxDegreeOfParallelism = 40 // Process 40 messages in parallel at a time
	   });

	// Add load data into action-block, which will regulate parallel execution (MaxDegreeOfParallelism) and load buffer (BoundedCapacity)
	for (int i = 0; i < 1000; i++)
	{
		await actionBlock.SendAsync(i);
	}

	// Finalize action block execution
	actionBlock.Complete();
	await actionBlock.Completion;
}

// Define other methods and classes here