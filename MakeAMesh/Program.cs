using System;
using Rhino.Compute;

namespace MakeAMesh
{
    class Program
    {
        static void Main(string[] args)
        {
            ComputeServer.ApiToken = enter_your_email_address;
            var sphere = new Rhino.Geometry.Sphere(Rhino.Geometry.Point3d.Origin, 12);
            var sphereAsBrep = sphere.ToBrep();
            // above uses standard Rhino3dmIO methods locally
            // the following function calls compute.rhino3d.com to get access to something not
            // available in Rhino3dmIO
            var meshes = MeshCompute.CreateFromBrep(sphereAsBrep);

            // back to regular Rhino3dmIO local calls
            Console.WriteLine($"Got {meshes.Length} meshes");
            for (int i = 0; i < meshes.Length; i++)
            {
                Console.WriteLine($"  {i + 1} mesh has {meshes[i].Vertices.Count} vrtices");
            }

            Console.WriteLine("press any key to exit");
            Console.ReadKey();
        }
    }
}
