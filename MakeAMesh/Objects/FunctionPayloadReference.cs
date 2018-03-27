using System.Threading.Tasks;

namespace MakeAMesh.Objects
{
  public class FunctionPayloadReference
  {
    public string Json { get; set; }
    public string Uri { get; set; }
    public TaskCompletionSource<string> TaskCompletionSource { get; set; }
  }
}
