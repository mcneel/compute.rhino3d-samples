using System;
using Rhino.Compute;

namespace MakeAMesh
{
    class Program
    {
        static void Main(string[] args)
        {
            ComputeServer.AuthToken = Rhino.Compute.AuthToken.Get ();

            // Use standard Rhino3dmIO methods locally
            var sphere = new Rhino.Geometry.Sphere(Rhino.Geometry.Point3d.Origin, 12);
            var sphereAsBrep = sphere.ToBrep();

            // The following function calls compute.rhino3d.com to get access to something not
            // available in Rhino3dmIO
            var meshes = MeshCompute.CreateFromBrep(sphereAsBrep);

            // Back to regular Rhino3dmIO local calls
            Console.WriteLine($"Got {meshes.Length} meshes");
            for (int i = 0; i < meshes.Length; i++)
            {
                Console.WriteLine($"  {i + 1} mesh has {meshes[i].Vertices.Count} vrtices");
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
