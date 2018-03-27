using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using MakeAMesh.Objects;
using Newtonsoft.Json;

namespace MakeAMesh.BulkRequest
{
  public class BulkRequestService
  {
    private readonly ConcurrentQueue<FunctionPayloadReference> _queue = new ConcurrentQueue<FunctionPayloadReference>();

    //this timer is an mvp feature, it would be better to have synchronization between the enqueing and a cancellation timeout (only use timer when enqueing has slowed)
    private readonly Timer _timer = new Timer(200);

    public BulkRequestService()
    {
      _timer.Elapsed += _timer_Elapsed;
      _timer.Start();
    }

    //redundant lock with ConcurrentQueue... this is why there are two separate locks for de- and en- because the queue will handle the locking
    //the TimerLock is to make sure the bundling dictionary is controlled
    private static readonly object AddLock = new object();
    public Task<string> AddItemToRequestService(string uri, string json)
    {
      var tcs = new TaskCompletionSource<string>();

      lock (AddLock)
      {
        _queue.Enqueue(new FunctionPayloadReference { Json = json, Uri = uri, TaskCompletionSource = tcs });
      }
     
      return tcs.Task;
    }

    public void StopTimer()
    {
      _timer.Stop();
      _timer.Dispose();
    }

    private static readonly object TimerElapsedLock = new object();
    private void _timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      lock (TimerElapsedLock)
      {
        var dictionaryJsonBundle = new Dictionary<string, List<FunctionPayloadReference>>();
        var count = _queue.Count;

        //if queue is empty continue
        if (count == 0) return;

        for (var i = 0; i < count; i++)
        {
          var success = _queue.TryDequeue(out FunctionPayloadReference functionPayloadReference);
          if (!success) throw new Exception("Dequeue unsuccessful");

          var key = functionPayloadReference.Uri;

          List<FunctionPayloadReference> jsonBundle;
          if (dictionaryJsonBundle.ContainsKey(key))
          {
            dictionaryJsonBundle.TryGetValue(key, out jsonBundle);
          }
          else
          {
            jsonBundle = new List<FunctionPayloadReference>();
            dictionaryJsonBundle.Add(key, jsonBundle);
          }
          jsonBundle?.Add(functionPayloadReference);
        }

        foreach (var keyValuePair in dictionaryJsonBundle)
        {
          var uri = keyValuePair.Key;
          var functionPayloadReferences = keyValuePair.Value;
          PostPacket(uri, functionPayloadReferences);
        }
      }     
    }

    private string ConstructArrayOfJsonObjects(IEnumerable<FunctionPayloadReference> functionPayloadReferences)
    {
      return "[" + string.Join(",", functionPayloadReferences.Select(x => x.Json)) + "]";
    }

    /// <summary>
    /// Builds a packet per uri from the elements in the queue
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="functionPayloadReferences"></param>
    private void PostPacket(string uri, IEnumerable<FunctionPayloadReference> functionPayloadReferences)
    {
      var functionPayloadReferenceList = functionPayloadReferences.ToList();

      if (functionPayloadReferenceList.Count == 1)
      {
        SingleRequest(uri, functionPayloadReferenceList);
      }
      else
      {
        MultipleRequest(uri, functionPayloadReferenceList);
      }
    }

    /// <summary>
    /// Single request sans querystring, uses TaskCompletionSource to trigger the completion of the task
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="functionPayloadReferenceList"></param>
    private static void SingleRequest(string uri, List<FunctionPayloadReference> functionPayloadReferenceList)
    {
      var request = WebRequest.Create(uri);
      request.ContentType = "application/json";
      request.Headers.Add("api_token", Secrets.ApiToken);
      request.Method = "POST";

      var functionPayloadReference = functionPayloadReferenceList.FirstOrDefault();

      using (var streamWriter = new StreamWriter(request.GetRequestStream()))
      {
        streamWriter.Write(functionPayloadReference?.Json);
        streamWriter.Flush();
      }
      var response = request.GetResponse();
      using (var streamReader = new StreamReader(response.GetResponseStream()))
      {
        var result = streamReader.ReadToEnd();
        functionPayloadReference?.TaskCompletionSource.SetResult(result);
      }
    }

    /// <summary>
    /// Multiple request with querystring, uses TaskCompletionSource to trigger the completion of the task
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="functionPayloadReferenceList"></param>
    private void MultipleRequest(string uri, List<FunctionPayloadReference> functionPayloadReferenceList)
    {
      var requestString = ConstructArrayOfJsonObjects(functionPayloadReferenceList);

      var request = WebRequest.Create(uri + "?multiple=true");
      request.ContentType = "application/json";
      request.Headers.Add("api_token", Secrets.ApiToken);
      request.Method = "POST";

      using (var streamWriter = new StreamWriter(request.GetRequestStream()))
      {
        streamWriter.Write(requestString);
        streamWriter.Flush();
      }
      var response = request.GetResponse();
      using (var streamReader = new StreamReader(response.GetResponseStream()))
      {
        var result = streamReader.ReadToEnd();
        UnbundleResponseAndSetTaskCompletionSourceResults(result, functionPayloadReferenceList);
      }
    }

    private void UnbundleResponseAndSetTaskCompletionSourceResults(string result, IEnumerable<FunctionPayloadReference> functionPayloadReferences)
    {
      var resultingObjects = JsonConvert.DeserializeObject<List<object>>(result);
      var count = 0;
      var functionPayloadReferenceList = functionPayloadReferences.ToList();

      foreach (var response in resultingObjects)
      {
        var functionPayloadReference = functionPayloadReferenceList[count];
        functionPayloadReference.TaskCompletionSource.SetResult(JsonConvert.SerializeObject(response));
        count++;
      }
    }
  }
}
