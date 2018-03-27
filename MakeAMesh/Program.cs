using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MakeAMesh.BulkRequest;
using Rhino.Compute;
using Rhino.Geometry;

namespace MakeAMesh
{
    class Program
    {
        static void Main(string[] args)
        {
            var bulkRequestService = new BulkRequestService();
            ComputeServer.InjectDependencies(bulkRequestService);

            //SingleRequestTest();
            MultipleRequestTest();

            bulkRequestService.StopTimer();

            Console.ReadKey();
        }

        private static void SingleRequestTest()
        {
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

        private static void MultipleRequestTest()
        {
            var listOfDims = Enumerable.Range(1, 100).ToList();

            var aggregateStopwatch = new Stopwatch();
            var aggregateLog = new StringBuilder();

            aggregateStopwatch.Start();
            var collectionBlocking = new BlockingCollection<double[]>();

            Parallel.ForEach(listOfDims, new ParallelOptions() { MaxDegreeOfParallelism = 2000 }, dim =>
            {
                var pt0 = new Point3d(-1, -1, -1);
                var pt1 = new Point3d(dim, dim, -1);
                var plane = new Plane(new Point3d(0, 0, -1), new Vector3d(0, 0, 1));
                var rectangle3D = new Rectangle3d(plane, pt0, pt1);
                var brep = Extrusion.Create(rectangle3D.ToNurbsCurve(), dim, true).ToBrep();
                brep.GetVolume();

                var dimPlus = dim + 1;

                var linePt0 = new Point3d(-2, -2, -2);
                var linePt1 = new Point3d(dimPlus, dimPlus, dimPlus);

                var success = Rhino.Compute.Intersect.IntersectionCompute.CurveBrep(new LineCurve(linePt0, linePt1), brep, 0.0001, 0, out double[] intersections);
                if(!success) Console.WriteLine("Unsuccessful function");
                collectionBlocking.Add(intersections);
            });

            aggregateStopwatch.Stop();
            var elapsed = aggregateStopwatch.ElapsedMilliseconds;

            Console.WriteLine(aggregateLog.ToString());
            Console.WriteLine(collectionBlocking.Count + " collection count!");
            Console.WriteLine($" aggregate elapsed = {elapsed} ms");
        }
    }
}
